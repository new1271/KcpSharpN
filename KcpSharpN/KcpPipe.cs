using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using KcpSharpN.Internal;
using KcpSharpN.Native;

namespace KcpSharpN;

/// <summary>
/// The event handler for <see cref="KcpPipe.OnUnderlyingPacketSending"/>.
/// </summary>
/// <param name="sender">the sender of event</param>
/// <param name="packet">the packet for sending</param>
public delegate void OnUnderlyingPacketSendingEventHandler(KcpPipe sender, in ReadOnlySpan<byte> packet);

/// <summary>
/// Represent the simple pipe to make accessing the KCP protocol easier.
/// </summary>
public sealed partial class KcpPipe : IDisposable
{
#if !NET5_0_OR_GREATER
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, BestFitMapping = false, CharSet = CharSet.Ansi, SetLastError = false, ThrowOnUnmappableChar = false)]
    private unsafe delegate int HandleOutputPacketDelegate(byte* buffer, int length, KcpContext* context, void* user);

    private static readonly HandleOutputPacketDelegate _handleOutputPacketDelegate;
#endif

    private static unsafe readonly delegate* unmanaged[Cdecl]<byte*, int, KcpContext*, void*, int> _handleOutputPacketFunc;
    private static readonly ConcurrentDictionary<nuint, GCHandle> _instanceDict = new();
    private static long _identifierCounter = 0;

    private unsafe readonly KcpContext* _context;
    private readonly Lazy<InternalThreadLoop> _threadLoopLazy;
    private readonly nuint _identifier;

    private bool _disposed;

    /// <summary>
    /// Triggered when the underlying packet sending.
    /// </summary>
    /// <remarks>
    /// The user needs to send the packet passed by the event to the underlying socket so that the KCP pipe can work.
    /// </remarks>
    public event OnUnderlyingPacketSendingEventHandler? OnUnderlyingPacketSending;

    /// <summary>
    /// The raw context in the <see cref="KcpPipe"/> object.
    /// </summary>
    public unsafe KcpContext* Context => _context;
    /// <summary>
    /// Get the <see cref="KcpPipeOption"/> that creating the <see cref="KcpPipe"/> object.
    /// </summary>
    public unsafe KcpPipeOption Option => _context->ToPipeOption();
    /// <summary>
    /// Check whether the object is disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    static unsafe KcpPipe()
    {
#if NET5_0_OR_GREATER
        _handleOutputPacketFunc = &HandleOutputPacket;
#else
        HandleOutputPacketDelegate handleOutputPacketDelegate = HandleOutputPacket;
        _handleOutputPacketDelegate = handleOutputPacketDelegate;
        _handleOutputPacketFunc = (delegate* unmanaged[Cdecl]<byte*, int, KcpContext*, void*, int>)Marshal.GetFunctionPointerForDelegate(handleOutputPacketDelegate);
#endif
    }

    /// <summary>
    /// The constructor of the <see cref="KcpPipe"/>.
    /// </summary>
    /// <param name="option">the option that creating underlying the KCP context</param>
    /// <exception cref="InvalidOperationException">Failed to create KCP context</exception>
    public unsafe KcpPipe(in KcpPipeOption option)
    {
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
        Kcp.ikcp_setoutput(context, _handleOutputPacketFunc);
        _threadLoopLazy = new Lazy<InternalThreadLoop>(() => new InternalThreadLoop(_context), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// The constructor of the <see cref="KcpPipe"/>.
    /// </summary>
    /// <param name="context">the underlying KCP context</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null</exception>
    public unsafe KcpPipe(KcpContext* context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        _context = context;
        Kcp.ikcp_setoutput(context, _handleOutputPacketFunc);
        _threadLoopLazy = new Lazy<InternalThreadLoop>(() => new InternalThreadLoop(_context), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Inputs the data from underlying socket.
    /// </summary>
    public void Input(ReadOnlySpan<byte> data)
        => _threadLoopLazy.Value.Input(data);

    /// <summary>
    /// Sends the data into the pipe.
    /// </summary>
    public void Send<T>(T value) where T : unmanaged
    {
        ReadOnlySpan<byte> span;
#if NET5_0_OR_GREATER
        span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
#else
        unsafe
        {
            span = new ReadOnlySpan<byte>(UnsafeHelper.AsPointer(ref value), sizeof(T));
        }
#endif

        Send(span);
    }

    /// <summary>
    /// Sends the data into the pipe.
    /// </summary>
    public void Send(ReadOnlySpan<byte> data)
        => _threadLoopLazy.Value.Send(data);

    /// <summary>
    /// Flushes the whole pipe and make sure all data has be sended.
    /// </summary>
    /// <param name="blocking">whether the calling thread needs to wait for the flushing operation ended</param>
    public void Flush(bool blocking)
        => _threadLoopLazy.Value.Flush(blocking);

    /// <summary>
    /// Flushes the whole pipe and make sure all data has be sended.
    /// </summary>
    /// <param name="blocking">whether the calling task needs to wait for the flushing operation ended</param>
    public Task FlushAsync(bool blocking)
        => _threadLoopLazy.Value.FlushAsync(blocking);

    /// <summary>
    /// Flushes the whole pipe and make sure all data has be sended.
    /// </summary>
    /// <param name="blocking">whether the calling task needs to wait for the flushing operation ended</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation</param>
    public ValueTask FlushAsync(bool blocking, CancellationToken cancellationToken)
        => _threadLoopLazy.Value.FlushAsync(blocking, cancellationToken);

    /// <summary>
    /// Receives the data out of the pipe.
    /// </summary>
    [SkipLocalsInit]
    public unsafe bool Receive<T>(out T value) where T : unmanaged
    {
        Span<byte> span;
#if NET5_0_OR_GREATER          
        span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref UnsafeHelper.AsRefOut(out value), 1));
#else
        unsafe
        {
            span = new Span<byte>(UnsafeHelper.AsPointer(ref UnsafeHelper.AsRefOut(out value)), sizeof(T));
        }
#endif

        return Receive(span, out int bytesWritten) && bytesWritten == sizeof(T);
    }

    /// <summary>
    /// Receives the data out of the pipe.
    /// </summary>
    public bool Receive(Span<byte> destination, out int bytesWritten)
        => _threadLoopLazy.Value.Receive(destination, out bytesWritten);

    /// <summary>
    /// Receives the data out of the pipe.
    /// </summary>
    public async ValueTask<int?> ReceiveAsync(Memory<byte> destination, CancellationToken cancellationToken)
        => await _threadLoopLazy.Value.ReceiveAsync(destination, cancellationToken);

    /// <summary>
    /// Waits for the next data coming.
    /// </summary>
    /// <returns></returns>
    public Task<bool> WaitToReceiveAsync() => _threadLoopLazy.Value.WaitToReceiveAsync();

    /// <summary>
    /// Waits for the next data coming.
    /// </summary>
    /// <returns></returns>
    public async ValueTask<bool> WaitToReceiveAsync(CancellationToken cancellationToken)
        => await _threadLoopLazy.Value.WaitToReceiveAsync(cancellationToken);

#if NET5_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
#endif
    private static unsafe int HandleOutputPacket(byte* buffer, int length, KcpContext* context, void* user)
    {
        if (user == null || !_instanceDict.TryGetValue((nuint)user, out GCHandle handle))
            goto Failed;
        if (handle.Target is not KcpPipe pipe)
        {
            ((ICollection<KeyValuePair<nuint, GCHandle>>)_instanceDict).Remove(KeyValuePair.Create((nuint)user, handle));
            goto Failed;
        }
        pipe.OnUnderlyingPacketSending?.Invoke(pipe, new ReadOnlySpan<byte>(buffer, length));
        return 0;
    Failed:
        return -1;
    }

    /// <summary>
    /// The deconstructor of the <see cref="KcpPipe"/>.
    /// </summary>
    ~KcpPipe() => Dispose(disposing: false);

    /// <inheritdoc />
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
