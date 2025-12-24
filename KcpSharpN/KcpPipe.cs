using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using KcpSharpN.Native;

namespace KcpSharpN
{
    public abstract class KcpPipe : IDisposable
    {
        private unsafe readonly KcpContext* _context;

        private TaskCompletionSource<bool>? _waitingInputTaskSource;
        private byte[]? _buffer;
        private int _bufferStart, _bufferEnd;
        private bool _disposed;

        public unsafe KcpContext* Context => _context;
        public unsafe KcpPipeOption Option => _context->ToPipeOption();
        public bool IsDisposed => _disposed;

        public unsafe KcpPipe(in KcpPipeOption option)
        {
            KcpContext* context = Kcp.ikcp_create(option.ConversationId, (void*)GCHandle.ToIntPtr(GCHandle.Alloc(this, GCHandleType.Normal)));
            if (context is null)
                throw new InvalidOperationException("Failed to create KCP context.");
            Kcp.ikcp_setmtu(context, (int)option.Mtu);
            Kcp.ikcp_interval(context, (int)option.Interval);
            Kcp.ikcp_wndsize(context, (int)option.SendWindow, (int)option.ReceiveWindow);
            Kcp.ikcp_nodelay(context, (int)option.NoDelay, (int)option.Interval, option.FastResend, option.CongestionControl);
            Kcp.ikcp_setoutput(context, &HandleOutputPacket);
            _context = context;
        }

        public unsafe KcpPipe(KcpContext* context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            Kcp.ikcp_setoutput(_context, &HandleOutputPacket);
            _context = context;
        }

        public async void StartUpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                uint interval;
                unsafe
                {
                    KcpContext* context = _context;
                    interval = GetTimestampAsUInt32();
                    interval = Math.Min(Kcp.ikcp_check(context, interval) - interval, int.MaxValue);
                }
                if (interval > 0)
                    await Task.Delay((int)interval, cancellationToken);
                unsafe
                {
                    Kcp.ikcp_update(_context, GetTimestampAsUInt32());
                }
            }
        }

        public async ValueTask<T?> ReceiveAsync<T>() where T : unmanaged
        {
            T result = default;

            byte[]? localBuffer = _buffer;
            int bufferStart = _bufferStart, bufferEnd = _bufferEnd;
            int destinationLength, bytesWritten;
            unsafe
            {
                destinationLength = sizeof(T);

                if (ConsumeBuffer(localBuffer, ref bufferStart, bufferEnd, CreateBytesSpanFromLocalVariable(ref result), out bool outOfBuffer, out bytesWritten))
                {
                    if (outOfBuffer)
                    {
                        _buffer = null;
                        _bufferStart = 0;
                        _bufferEnd = 0;
                        ArrayPool<byte>.Shared.Return(localBuffer);
                    }
                    else
                    {
                        _bufferStart = bufferStart;
                    }
                }
                if (bytesWritten >= destinationLength)
                    return result;
            }

            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

        FetchNewData:
            (localBuffer, int fetchedBytes) = await FetchDataIntoNewBufferAsync(pool, blocked: true);
            if (localBuffer is null)
                return null;

            int bytesToCopy = Math.Min(fetchedBytes, destinationLength - bytesWritten);
            localBuffer.AsSpan(0, bytesToCopy).CopyTo(CreateBytesSpanFromLocalVariable(ref result).Slice(bytesWritten));
            bytesWritten += bytesToCopy;
            if (fetchedBytes > bytesToCopy)
            {
                _buffer = localBuffer;
                _bufferStart = bytesToCopy;
                _bufferEnd = fetchedBytes;
            }
            else
            {
                pool.Return(localBuffer);
                if (bytesWritten < destinationLength)
                    goto FetchNewData;
            }
            return result;
        }

        public async ValueTask<int?> ReceiveAsync(Memory<byte> buffer, bool fillAll)
        {
            byte[]? localBuffer = _buffer;
            int destinationLength = buffer.Length, bufferStart = _bufferStart, bufferEnd = _bufferEnd;

            if (ConsumeBuffer(localBuffer, ref bufferStart, bufferEnd, buffer.Span, out bool outOfBuffer, out int bytesWritten))
            {
                if (outOfBuffer)
                {
                    _buffer = null;
                    _bufferStart = 0;
                    _bufferEnd = 0;
                    ArrayPool<byte>.Shared.Return(localBuffer);
                }
                else
                {
                    _bufferStart = bufferStart;
                }
            }
            if (bytesWritten >= destinationLength)
                return destinationLength;

            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

        FetchNewData:
            (localBuffer, int fetchedBytes) = await FetchDataIntoNewBufferAsync(pool, blocked: fillAll || bytesWritten == 0);
            if (localBuffer is null)
                return bytesWritten;

            int bytesToCopy = Math.Min(fetchedBytes, destinationLength - bytesWritten);
            localBuffer.AsSpan(0, bytesToCopy).CopyTo(buffer.Span.Slice(bytesWritten));
            bytesWritten += bytesToCopy;
            if (fetchedBytes > bytesToCopy)
            {
                _buffer = localBuffer;
                _bufferStart = bytesToCopy;
                _bufferEnd = fetchedBytes;
            }
            else
            {
                pool.Return(localBuffer);
                if (fillAll && bytesWritten < destinationLength)
                    goto FetchNewData;
            }
            return bytesWritten;
        }

        public unsafe void Flush()
        {
            Kcp.ikcp_flush(_context);
        }

        public unsafe void Send<T>(T value) where T : unmanaged
        {
            Kcp.ikcp_send(_context, (byte*)&value, sizeof(T));
        }

        public unsafe void Send(ReadOnlySpan<byte> data)
        {
            fixed (byte* ptr = data)
                Kcp.ikcp_send(_context, ptr, data.Length);
        }

        public unsafe void Input(ReadOnlySpan<byte> data)
        {
            fixed (byte* ptr = data)
                Kcp.ikcp_input(_context, ptr, data.Length);
            Interlocked.Exchange(ref _waitingInputTaskSource, null)?.TrySetResult(true);
        }

        public Task<bool> WaitForDataReceived()
        {
            TaskCompletionSource<bool> completionSource = Interlocked.CompareExchange(ref _waitingInputTaskSource, null, null) ??
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return (Interlocked.CompareExchange(ref _waitingInputTaskSource, completionSource, null) ?? completionSource).Task;
        }

        public Task<bool> WaitForDataReceived(int timeout)
        {
            Task<bool> work = WaitForDataReceived();
            if (timeout == Timeout.Infinite)
                return work;
            return WithTimeout(work, timeout);
        }

        private static async Task<bool> WithTimeout(Task<bool> task, int timeout)
        {
            Task delayTask = Task.Delay(timeout);
            Task finishedTask = await Task.WhenAny(task, delayTask);
            if (ReferenceEquals(delayTask, finishedTask))
                return false;
            return task.Result;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe int HandleOutputPacket(byte* buffer, int length, KcpContext* context, void* user)
        {
            if (user == null)
                goto Failed;
            GCHandle handle = GCHandle.FromIntPtr((nint)user);
            if (handle.Target is not KcpPipe pipe)
                goto Failed;
            pipe.HandleOutputPacket(new ReadOnlySpan<byte>(buffer, length));
            return 0;
        Failed:
            return -1;
        }

        protected abstract void HandleOutputPacket(ReadOnlySpan<byte> packet);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetTimestampAsUInt32()
            => (uint)(ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> CreateBytesSpanFromLocalVariable<T>(scoped ref T reference) where T : unmanaged
            => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref reference, 1));

        private static bool ConsumeBuffer([NotNullWhen(true)] byte[]? buffer, ref int start, int end, Span<byte> destination, out bool outOfBuffer, out int bytesWritten)
        {
            if (buffer is null)
                goto Failed;
            int length = end - start;
            if (length <= 0)
                goto Failed;

            length = Math.Min(length, destination.Length);
            buffer.AsSpan(start, length).CopyTo(destination);
            start += length;

            outOfBuffer = start >= end;
            bytesWritten = length;
            return true;

        Failed:
            outOfBuffer = false;
            bytesWritten = 0;
            return false;
        }

        private async ValueTask<(byte[]? buffer, int bytesWritten)> FetchDataIntoNewBufferAsync(ArrayPool<byte> pool, bool blocked)
        {
        Head:
            unsafe
            {
                KcpContext* context = _context;
                int bytesWritten = Kcp.ikcp_peeksize(context);
                if (bytesWritten < 0)
                    goto TryWait;
                byte[]? buffer = pool.Rent(bytesWritten);
                try
                {
                    fixed (byte* ptr = buffer)
                        bytesWritten = Kcp.ikcp_recv(context, ptr, buffer.Length);
                    if (bytesWritten >= 0)
                    {
                        byte[]? result = buffer;
                        buffer = null;
                        return (result, bytesWritten);
                    }
                    switch (bytesWritten)
                    {
                        case -1:
                        case -2:
                            goto TryWait;
                        case -3:
                            goto Head;
                    }
                    goto Failed;
                }
                finally
                {
                    if (buffer is not null)
                        pool.Return(buffer);
                }
            }

        TryWait:
            if (blocked)
            {
                await WaitForDataReceived();
                goto Head;
            }
            goto Failed;

        Failed:
            return (null, 0);
        }

        ~KcpPipe() => Dispose(disposing: false);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;

            DisposeCore(disposing);
        }

        protected virtual unsafe void DisposeCore(bool disposing)
        {
            Kcp.ikcp_release(_context);
            Interlocked.Exchange(ref _waitingInputTaskSource, null)?.TrySetCanceled();
            byte[]? buffer = _buffer;
            _buffer = null;
            _bufferStart = 0;
            _bufferEnd = 0;
            if (buffer is not null)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
