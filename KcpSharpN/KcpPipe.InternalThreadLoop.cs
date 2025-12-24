using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using KcpSharpN.Native;

namespace KcpSharpN
{
    partial class KcpPipe
    {
        private sealed unsafe class InternalThreadLoop : IDisposable
        {
            private static ulong _idCounter = 0;

            private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
            private readonly ConcurrentQueue<IMemoryOwner<byte>> _inputQueue, _sendQueue, _receiveQueue;
            private readonly Thread _thread;
            private readonly KcpContext* _context;

            private ulong _disposed = 0UL;

            public InternalThreadLoop(KcpContext* context)
            {
                _context = context;
                _memoryPool = MemoryPool<byte>.Shared;
                _inputQueue = new ConcurrentQueue<IMemoryOwner<byte>>();
                _sendQueue = new ConcurrentQueue<IMemoryOwner<byte>>();
                _receiveQueue = new ConcurrentQueue<IMemoryOwner<byte>>();
                _thread = new Thread(DoThreadLoop)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                    Name = $"KCP Protocol Thread Loop #{Interlocked.Increment(ref _idCounter):D}"
                };
            }

            private void DoThreadLoop()
            {
                MemoryPool<byte> memoryPool = _memoryPool;
                ConcurrentQueue<IMemoryOwner<byte>> inputQueue = _inputQueue;
                ConcurrentQueue<IMemoryOwner<byte>> sendQueue = _sendQueue;
                ConcurrentQueue<IMemoryOwner<byte>> receiveQueue = _receiveQueue;
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

                    while (inputQueue.TryDequeue(out IMemoryOwner<byte>? bufferOwner))
                    {
                        try
                        {
                            Span<byte> buffer = bufferOwner.Memory.Span;
                            fixed (byte* ptr = buffer)
                                Kcp.ikcp_input(context, ptr, buffer.Length);
                        }
                        finally
                        {
                            bufferOwner.Dispose();
                        }
                    }

                    while (sendQueue.TryDequeue(out IMemoryOwner<byte>? bufferOwner))
                    {
                        try
                        {
                            Span<byte> buffer = bufferOwner.Memory.Span;
                            fixed (byte* ptr = buffer)
                                Kcp.ikcp_send(context, ptr, buffer.Length);
                        }
                        finally
                        {
                            bufferOwner.Dispose();
                        }
                    }

                    int receivedLength;
                    while ((receivedLength = Kcp.ikcp_peeksize(context)) >= 0)
                    {
                        if (receivedLength == 0)
                        {
                            Kcp.ikcp_recv(context, null, 0);
                            continue;
                        }
                        IMemoryOwner<byte> bufferOwner = memoryPool.Rent(receivedLength);
                        Span<byte> buffer = bufferOwner.Memory.Span;
                        fixed (byte* ptr = buffer)
                        {
                            int status = Kcp.ikcp_recv(context, ptr, receivedLength);
                            Debug.Assert(status == receivedLength);
                        }
                        receiveQueue.Enqueue(bufferOwner);
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
