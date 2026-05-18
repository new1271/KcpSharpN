using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using KcpSharpN.Native;

namespace KcpSharpN
{
    public delegate void OnUnderlyingPacketSendingEventHandler(KcpPipe sender, in ReadOnlySpan<byte> packet);

    public sealed partial class KcpPipe : IDisposable
    {
        private static readonly ConcurrentDictionary<nuint, GCHandle> _instanceDict = new();
        private static long _identifierCounter = 0;

        private unsafe readonly KcpContext* _context;
        private readonly Lazy<InternalThreadLoop> _threadLoopLazy;
        private readonly EndPoint _endPoint;
        private readonly nuint _identifier;

        private bool _disposed;

        public event OnUnderlyingPacketSendingEventHandler? OnUnderlyingPacketSending;
        public unsafe KcpContext* Context => _context;
        public unsafe KcpPipeOption Option => _context->ToPipeOption();
        public bool IsDisposed => _disposed;

        public unsafe KcpPipe(EndPoint endPoint, in KcpPipeOption option)
        {
            _endPoint = endPoint;
            nuint identifier;
            while (true)
            {
                identifier = (nuint)Interlocked.Increment(ref _identifierCounter);
                if (_instanceDict.TryAdd(identifier, GCHandle.Alloc(this, GCHandleType.Weak)))
                    break;
            }
            KcpContext* context = Kcp.ikcp_create(option.ConversationId, (void*)identifier);
            if (context is null)
            {
                _instanceDict.TryRemove(identifier, out GCHandle handle);
                handle.Free();
                throw new InvalidOperationException("Failed to create KCP context.");
            }
            _context = context;
            _identifier = identifier;
            Kcp.ikcp_setmtu(context, (int)option.Mtu);
            Kcp.ikcp_interval(context, (int)option.Interval);
            Kcp.ikcp_wndsize(context, (int)option.SendWindow, (int)option.ReceiveWindow);
            Kcp.ikcp_nodelay(context, (int)option.NoDelay, (int)option.Interval, option.FastResend, option.NoCongestionControl ? 1 : 0);
            Kcp.ikcp_setoutput(context, &HandleOutputPacket);
            _threadLoopLazy = new Lazy<InternalThreadLoop>(() => new InternalThreadLoop(_context), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public unsafe KcpPipe(EndPoint endPoint, KcpContext* context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            _endPoint = endPoint;
            _context = context;
            Kcp.ikcp_setoutput(context, &HandleOutputPacket);
            _threadLoopLazy = new Lazy<InternalThreadLoop>(() => new InternalThreadLoop(_context), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public void Input(ReadOnlySpan<byte> data)
            => _threadLoopLazy.Value.Input(data);

        public void Send<T>(T value) where T : unmanaged
            => Send(CreateBytesSpanFromLocalVariable(ref value));

        public void Send(ReadOnlySpan<byte> data)
            => _threadLoopLazy.Value.Send(data);

        public void Flush(bool blocking)
            => _threadLoopLazy.Value.Flush(blocking);

        public Task FlushAsync(bool blocking)
            => _threadLoopLazy.Value.FlushAsync(blocking);

        public ValueTask FlushAsync(bool blocking, CancellationToken cancellationToken)
            => _threadLoopLazy.Value.FlushAsync(blocking, cancellationToken);

        public unsafe bool Receive<T>(out T value) where T : unmanaged
        {
            Unsafe.SkipInit(out value);
            return Receive(CreateBytesSpanFromLocalVariable(ref value), out int bytesWritten) && bytesWritten == sizeof(T);
        }

        public bool Receive(Span<byte> destination, out int bytesWritten)
            => _threadLoopLazy.Value.Receive(destination, out bytesWritten);

        public async ValueTask<int?> ReceiveAsync(Memory<byte> destination, CancellationToken cancellationToken)
            => await _threadLoopLazy.Value.ReceiveAsync(destination, cancellationToken);

        public Task<bool> WaitToReceiveAsync() => _threadLoopLazy.Value.WaitToReceiveAsync();

        public async ValueTask<bool> WaitToReceiveAsync(CancellationToken cancellationToken)
            => await _threadLoopLazy.Value.WaitToReceiveAsync(cancellationToken);

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe int HandleOutputPacket(byte* buffer, int length, KcpContext* context, void* user)
        {
            if (user == null || !_instanceDict.TryGetValue((nuint)user, out GCHandle handle))
                goto Failed;
            if (handle.Target is not KcpPipe pipe)
            {
                _instanceDict.TryRemove(KeyValuePair.Create((nuint)user, handle));
                goto Failed;
            }
            pipe.OnUnderlyingPacketSending?.Invoke(pipe, new ReadOnlySpan<byte>(buffer, length));
            return 0;
        Failed:
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> CreateBytesSpanFromLocalVariable<T>(scoped ref T reference) where T : unmanaged
            => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref reference, 1));

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

        private void DisposeCore(bool disposing)
        {
            if (!_threadLoopLazy.IsValueCreated)
                return;
            _threadLoopLazy.Value.Dispose();
            if (_instanceDict.TryRemove(_identifier, out GCHandle handle))
                handle.Free();
        }
    }
}
