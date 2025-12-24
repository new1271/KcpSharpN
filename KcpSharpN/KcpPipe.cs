using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using KcpSharpN.Native;

namespace KcpSharpN
{
    public sealed partial class KcpPipe : IDisposable
    {
        private unsafe readonly KcpContext* _context;
        private readonly Lazy<InternalThreadLoop> _threadLoopLazy;
        private readonly Socket _socket;
        private readonly EndPoint _endPoint;

        private bool _disposed;

        public unsafe KcpContext* Context => _context;
        public unsafe KcpPipeOption Option => _context->ToPipeOption();
        public bool IsDisposed => _disposed;

        public unsafe KcpPipe(Socket socket, EndPoint endPoint, in KcpPipeOption option)
        {
            _socket = socket;
            _endPoint = endPoint;
            KcpContext* context = Kcp.ikcp_create(option.ConversationId, (void*)GCHandle.ToIntPtr(GCHandle.Alloc(this, GCHandleType.Normal)));
            if (context is null)
                throw new InvalidOperationException("Failed to create KCP context.");
            _context = context;
            Kcp.ikcp_setmtu(context, (int)option.Mtu);
            Kcp.ikcp_interval(context, (int)option.Interval);
            Kcp.ikcp_wndsize(context, (int)option.SendWindow, (int)option.ReceiveWindow);
            Kcp.ikcp_nodelay(context, (int)option.NoDelay, (int)option.Interval, option.FastResend, option.CongestionControl);
            Kcp.ikcp_setoutput(context, &HandleOutputPacket);
            _threadLoopLazy = new Lazy<InternalThreadLoop>(() => new InternalThreadLoop(_context), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public unsafe KcpPipe(Socket socket, EndPoint endPoint, KcpContext* context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            _socket = socket;
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

        private void HandleOutputPacket(ReadOnlySpan<byte> packet) => _socket.SendTo(packet, _endPoint);

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
        }
    }
}
