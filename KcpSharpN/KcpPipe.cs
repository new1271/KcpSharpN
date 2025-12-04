using System;
using System.Buffers;
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
            Kcp.ikcp_setmtu(context, (int)option.Mtu);
            Kcp.ikcp_interval(context, (int)option.Interval);
            Kcp.ikcp_wndsize(context, (int)option.SendWindow, (int)option.ReceiveWindow);
            Kcp.ikcp_nodelay(context, (int)option.NoDelay, (int)option.Interval, option.FastResend, option.CongestionControl);
            Kcp.ikcp_setoutput(context, &HandleOutputPacket);
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

        public unsafe bool TryReceive<T>(out T result) where T : unmanaged
        {
            Unsafe.SkipInit(out result);
            
            KcpContext* context = _context;
            Span<byte> resultSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));
            if (!TryReceive(resultSpan, out int bytesWritten))
                return false;
            if (bytesWritten < sizeof(T))
            {
                byte[]? buffer = _buffer;
                if (buffer is null)
                {
                    buffer = ArrayPool<byte>.Shared.Rent(bytesWritten);
                    resultSpan[..bytesWritten].CopyTo(buffer.AsSpan()[..bytesWritten]);
                }
                else
                {
                    _bufferStart -= bytesWritten;
                }
                return false;
            }
            return true;
        }

        public unsafe bool TryReceive(Span<byte> buffer, out int bytesWritten)
        {
            int bufferLength = buffer.Length;
            byte[]? oldBuffer = _buffer;
            int bufferStart = _bufferStart, bufferEnd = _bufferEnd;
            if (oldBuffer is null || bufferStart >= bufferEnd)
            {
                bytesWritten = 0;
            }
            else
            {
                ReadOnlySpan<byte> oldBufferSpan = oldBuffer.AsSpan()[bufferStart..bufferEnd];
                int oldBufferLength = oldBufferSpan.Length;
                if (oldBufferLength > bufferLength)
                {
                    oldBufferSpan[..bufferLength].CopyTo(buffer);
                    bytesWritten = bufferLength;
                    _bufferStart += bufferLength;
                    return true;
                }
                oldBufferSpan.CopyTo(buffer);
                bytesWritten = oldBufferLength;
                _bufferStart = 0;
                _bufferEnd = 0;
                _buffer = null;
                ArrayPool<byte>.Shared.Return(oldBuffer);
            }
            KcpContext* context = _context;
            int status;
            fixed (byte* ptr = buffer)
            {
                status = Kcp.ikcp_recv(context, ptr, bufferLength - bytesWritten);
                if (status >= 0)
                {
                    bytesWritten += status;
                    return true;
                }
            }
            if (status != -3)
                return bytesWritten > 0;
            int size;
            while ((size = Kcp.ikcp_peeksize(context)) > 0)
            {
                oldBuffer = ArrayPool<byte>.Shared.Rent(size);
                fixed (byte* ptr = oldBuffer)
                {
                    status = Kcp.ikcp_recv(context, ptr, size);
                    if (status < 0)
                    {
                        if (status == -3)
                            continue;
                        break;
                    }
                }
                int available = bufferLength - bytesWritten;
                if (status > available)
                {
                    ReadOnlySpan<byte> bufferSpan = oldBuffer.AsSpan()[..available];
                    bufferSpan.CopyTo(buffer);
                    bytesWritten = bufferLength;
                    _buffer = oldBuffer;
                    _bufferStart = available;
                    _bufferEnd = status;
                }
                else
                {
                    ReadOnlySpan<byte> bufferSpan = oldBuffer.AsSpan()[..status];
                    bufferSpan.CopyTo(buffer);
                    bytesWritten += status;
                    ArrayPool<byte>.Shared.Return(oldBuffer);
                }
                return true;
            }
            return bytesWritten > 0;
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

        public  Task<bool> WaitForDataReceived()
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
