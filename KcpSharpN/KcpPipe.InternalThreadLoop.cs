using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using KcpSharpN.Native;

namespace KcpSharpN
{
    partial class KcpPipe
    {
        private sealed class InternalThreadLoop : IDisposable
        {
            private static ulong _idCounter = 0;

            private readonly ArrayPool<byte> _pool;
            private readonly ConcurrentQueue<ArraySegment<byte>> _inputQueue;
            private readonly ConcurrentQueue<ArraySegment<byte>> _sendQueue, _receiveQueue;
            private readonly ConcurrentQueue<TaskCompletionSource<bool>> _receiveAwaiterQueue;
            private readonly SemaphoreSlim _receiveBarrier;
            private readonly Thread _thread;
            private readonly unsafe KcpContext* _context;

            private ArraySegment<byte> _currentSegment;
            private ulong _disposed = 0UL;

            public unsafe InternalThreadLoop(KcpContext* context)
            {
                _context = context;
                _pool = ArrayPool<byte>.Shared;
                _inputQueue = new ConcurrentQueue<ArraySegment<byte>>();
                _sendQueue = new ConcurrentQueue<ArraySegment<byte>>();
                _receiveQueue = new ConcurrentQueue<ArraySegment<byte>>();
                _receiveAwaiterQueue = new ConcurrentQueue<TaskCompletionSource<bool>>();
                _receiveBarrier = new SemaphoreSlim(1, 1);
                _thread = new Thread(DoThreadLoop)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                    Name = $"KCP Protocol Thread Loop #{Interlocked.Increment(ref _idCounter):D}"
                };
                _thread.Start();
            }

            public void Input(ReadOnlySpan<byte> data)
            {
                int length = data.Length;
                byte[] buffer = _pool.Rent(length);
                data.CopyTo(buffer.AsSpan());
                _inputQueue.Enqueue(new ArraySegment<byte>(buffer, 0, length));
            }

            public void Send(ReadOnlySpan<byte> data)
            {
                int length = data.Length;
                byte[] buffer = _pool.Rent(length);
                data.CopyTo(buffer.AsSpan());
                _sendQueue.Enqueue(new ArraySegment<byte>(buffer, 0, length));
            }

            public bool TryReceive(Span<byte> destination, out int bytesWritten)
            {
                SemaphoreSlim barrier = _receiveBarrier;
                ConcurrentQueue<ArraySegment<byte>> receiveQueue = _receiveQueue;
                ArrayPool<byte> pool = _pool;
                barrier.Wait();
                try
                {
                    Thread.MemoryBarrier();
                    ArraySegment<byte> segment = _currentSegment;
                    byte[]? array = segment.Array;
                    if (array is null)
                    {
                        bytesWritten = 0;
                    }
                    else
                    {
                        Span<byte> span = segment.AsSpan();
                        int length = span.Length;
                        bytesWritten = Math.Min(length, destination.Length);
                        span.Slice(0, bytesWritten).CopyTo(destination);
                        if (bytesWritten < length)
                        {
                            _currentSegment = segment.Slice(bytesWritten);
                            return true;
                        }
                        pool.Return(array);
                        _currentSegment = default;
                        if (bytesWritten == length)
                            return true;
                        destination = destination.Slice(bytesWritten);
                    }
                    while (receiveQueue.TryDequeue(out segment))
                    {
                        Thread.MemoryBarrier();
                        array = segment.Array;
                        if (array is null || segment.Count <= 0)
                            continue;
                        Span<byte> span = segment.AsSpan();
                        int length = span.Length;
                        int bytesToWrite = Math.Min(length, destination.Length);
                        span.Slice(0, bytesToWrite).CopyTo(destination);
                        bytesWritten += bytesToWrite;
                        if (bytesToWrite < length)
                        {
                            _currentSegment = segment.Slice(bytesToWrite);

                            return true;
                        }
                        pool.Return(array);
                        if (bytesWritten == length)
                            return true;
                        destination = destination.Slice(bytesToWrite);
                    }
                }
                finally
                {
                    barrier.Release();
                }
                return bytesWritten > 0;
            }

            public async ValueTask<int?> TryReceiveAsync(Memory<byte> destination, CancellationToken cancellationToken)
            {
                SemaphoreSlim barrier = _receiveBarrier;
                ConcurrentQueue<ArraySegment<byte>> receiveQueue = _receiveQueue;
                ArrayPool<byte> pool = _pool;
                await barrier.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                int bytesWritten = 0;
                try
                {
                    Thread.MemoryBarrier();
                    ArraySegment<byte> segment = _currentSegment;
                    byte[]? array = segment.Array;
                    if (array is not null)
                    {
                        Span<byte> span = segment.AsSpan();
                        int length = span.Length;
                        bytesWritten = Math.Min(length, destination.Length);
                        span.Slice(0, bytesWritten).CopyTo(destination.Span);
                        if (bytesWritten < length)
                        {
                            _currentSegment = segment.Slice(bytesWritten);
                            return bytesWritten;
                        }
                        pool.Return(array);
                        _currentSegment = default;
                        if (bytesWritten == length)
                            return bytesWritten;
                        destination = destination.Slice(bytesWritten);
                    }
                    while (receiveQueue.TryDequeue(out segment))
                    {
                        Thread.MemoryBarrier();
                        array = segment.Array;
                        if (array is null || segment.Count <= 0)
                            continue;
                        Span<byte> span = segment.AsSpan();
                        int length = span.Length;
                        int bytesToWrite = Math.Min(length, destination.Length);
                        span.Slice(0, bytesToWrite).CopyTo(destination.Span);
                        bytesWritten += bytesToWrite;
                        if (bytesToWrite < length)
                        {
                            _currentSegment = segment.Slice(bytesToWrite);
                            return bytesWritten;
                        }
                        pool.Return(array);
                        if (bytesWritten >= length)
                            return bytesWritten;
                        destination = destination.Slice(bytesToWrite);
                    }
                }
                finally
                {
                    barrier.Release();
                }
                return bytesWritten > 0 ? bytesWritten : null;
            }

            public bool Receive(Span<byte> destination, out int bytesWritten)
            {
                int length = destination.Length;
                if (length <= 0)
                {
                    bytesWritten = length;
                    return true;
                }
                bytesWritten = 0;
                while (Interlocked.Read(ref _disposed) == 0UL)
                {
                    if (TryReceive(destination, out int bytesWrittenInOneTime))
                    {
                        bytesWritten += bytesWrittenInOneTime;
                        if (bytesWritten >= length)
                            return true;
                        destination = destination.Slice(bytesWrittenInOneTime);
                        continue;
                    }
                    WaitToReceive();
                }
                return false;
            }

            public async ValueTask<int?> ReceiveAsync(Memory<byte> destination, CancellationToken cancellationToken)
            {
                int length = destination.Length;
                if (length <= 0)
                {
                    return length;
                }
                int bytesWritten = 0;
                while (Interlocked.Read(ref _disposed) == 0UL)
                {
                    int? bytesWrittenInOneTimeOptional = await TryReceiveAsync(destination, cancellationToken).ConfigureAwait(false);
                    if (bytesWrittenInOneTimeOptional is not null)
                    {
                        int bytesWrittenInOneTime = bytesWrittenInOneTimeOptional.Value;
                        bytesWritten += bytesWrittenInOneTime;
                        if (bytesWritten >= length)
                            return bytesWritten;
                        destination = destination.Slice(bytesWrittenInOneTime);
                        continue;
                    }
                    await WaitToReceiveAsync(cancellationToken).ConfigureAwait(false);
                }
                return null;
            }

            public bool WaitToReceive() => WaitToReceiveAsync().GetAwaiter().GetResult();

            public Task<bool> WaitToReceiveAsync()
            {
                TaskCompletionSource<bool> awaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _receiveAwaiterQueue.Enqueue(awaiter);
                return awaiter.Task;
            }

            public async ValueTask<bool> WaitToReceiveAsync(CancellationToken cancellationToken)
            {
                TaskCompletionSource<bool> awaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _receiveAwaiterQueue.Enqueue(awaiter);
                CancellationTokenRegistration registration = cancellationToken.Register(() => awaiter.TrySetCanceled(cancellationToken), useSynchronizationContext: false);
                try
                {
                    return await awaiter.Task;
                }
                finally
                {
                    registration.Dispose();
                }
            }

            private unsafe void DoThreadLoop()
            {
                ArrayPool<byte> pool = _pool;
                ConcurrentQueue<ArraySegment<byte>> inputQueue = _inputQueue;
                ConcurrentQueue<ArraySegment<byte>> sendQueue = _sendQueue;
                ConcurrentQueue<ArraySegment<byte>> receiveQueue = _receiveQueue;
                ConcurrentQueue<TaskCompletionSource<bool>> receiveAwaiterQueue = _receiveAwaiterQueue;
                KcpContext* context = _context;

                while (Interlocked.Read(ref _disposed) == 0UL)
                {
                    uint timestamp = GetTimestampAsUInt32();
                    uint nextTimestamp = Kcp.ikcp_check(context, timestamp);
                    if (nextTimestamp > timestamp)
                    {
                        Thread.Sleep((int)(nextTimestamp - timestamp));
                        timestamp = GetTimestampAsUInt32();
                    }
                    Kcp.ikcp_update(context, timestamp);

                    if (sendQueue.TryDequeue(out ArraySegment<byte> segment))
                    {
                        Thread.MemoryBarrier();
                        do
                        {
                            byte[]? buffer = segment.Array;
                            if (buffer is null)
                                continue;
                            try
                            {
                                Span<byte> span = segment.AsSpan();
                                fixed (byte* ptr = span)
                                    Kcp.ikcp_send(context, ptr, span.Length);
                            }
                            finally
                            {
                                pool.Return(buffer);
                            }
                        } while (sendQueue.TryDequeue(out segment));
                    }

                    if (inputQueue.TryDequeue(out segment))
                    {
                        Thread.MemoryBarrier();
                        do
                        {
                            byte[]? buffer = segment.Array;
                            if (buffer is null)
                                continue;
                            try
                            {
                                Span<byte> span = segment.AsSpan();
                                fixed (byte* ptr = span)
                                    Kcp.ikcp_input(context, ptr, span.Length);
                            }
                            finally
                            {
                                pool.Return(buffer);
                            }
                        } while (inputQueue.TryDequeue(out segment));
                    }

                    int receivedLength;
                    if ((receivedLength = Kcp.ikcp_peeksize(context)) >= 0)
                    {
                        do
                        {
                            if (receivedLength == 0)
                            {
                                Kcp.ikcp_recv(context, null, 0);
                                continue;
                            }
                            byte[] buffer = pool.Rent(receivedLength);
                            fixed (byte* ptr = buffer)
                            {
                                int status = Kcp.ikcp_recv(context, ptr, receivedLength);
                                Debug.Assert(status == receivedLength);
                            }
                            receiveQueue.Enqueue(new ArraySegment<byte>(buffer, 0, receivedLength));
                        }
                        while ((receivedLength = Kcp.ikcp_peeksize(context)) >= 0);
                        Thread.MemoryBarrier();
                        while (receiveAwaiterQueue.TryDequeue(out TaskCompletionSource<bool>? awaiter))
                            awaiter.TrySetResult(true);
                    }
                }

                Kcp.ikcp_release(context);
                Debug.WriteLine($"[{nameof(KcpPipe)}.{nameof(InternalThreadLoop)}] Released KCP Context (Thread Name: {_thread.Name})");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint GetTimestampAsUInt32()
                => (uint)(ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            private void DisposeCore()
            {
                if (Interlocked.Exchange(ref _disposed, 1UL) != 0UL)
                    return;
                Debug.WriteLine($"[{nameof(KcpPipe)}.{nameof(InternalThreadLoop)}] Disposing internal thread loop... (Thread Name: {_thread.Name})");

                while (_receiveAwaiterQueue.TryDequeue(out TaskCompletionSource<bool>? awaiter))
                    awaiter.TrySetResult(false);

                SemaphoreSlim barrier = _receiveBarrier;
                barrier.Wait();
                try
                {
                    ArraySegment<byte> segment = _currentSegment;
                    byte[]? array = segment.Array;
                    if (array is not null)
                        _pool.Return(array);
                }
                finally
                {
                    barrier.Release();
                    barrier.Dispose();
                }
                while (_inputQueue.TryDequeue(out ArraySegment<byte> segment))
                {
                    byte[]? array = segment.Array;
                    if (array is not null)
                        _pool.Return(array);
                }
                while (_sendQueue.TryDequeue(out ArraySegment<byte> segment))
                {
                    byte[]? array = segment.Array;
                    if (array is not null)
                        _pool.Return(array);
                }
                while (_receiveQueue.TryDequeue(out ArraySegment<byte> segment))
                {
                    byte[]? array = segment.Array;
                    if (array is not null)
                        _pool.Return(array);
                }
            }

            ~InternalThreadLoop()
            {
                DisposeCore();
            }

            public void Dispose()
            {
                DisposeCore();
                GC.SuppressFinalize(this);
            }

        }
    }
}
