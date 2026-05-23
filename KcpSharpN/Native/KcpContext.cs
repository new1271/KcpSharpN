using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KcpSharpN.Native;

#pragma warning disable CS1591

//---------------------------------------------------------------------
// IKCPCB
//---------------------------------------------------------------------
/// <summary>
/// The context for the KCP protocol.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct KcpContext
{
    public uint conv, mtu, mss, state;
    public uint snd_una, snd_nxt, rcv_nxt;
    public uint ts_recent, ts_lastack, ssthresh;
    public int rx_rttval, rx_srtt, rx_rto, rx_minrto;
    public uint snd_wnd, rcv_wnd, rmt_wnd, cwnd, probe;
    public uint current, interval, ts_flush, xmit;
    public uint nrcv_buf, nsnd_buf;
    public uint nrcv_que, nsnd_que;
    public uint nodelay, updated;
    public uint ts_probe, probe_wait;
    public uint dead_link, incr;
    public KcpQueueHead snd_queue;
    public KcpQueueHead rcv_queue;
    public KcpQueueHead snd_buf;
    public KcpQueueHead rcv_buf;
    public uint* acklist;
    public uint ackcount;
    public uint ackblock;
    public uint ackedlen;
    public void* user;
    public byte* buffer;
    public int fastresend;
    public int fastlimit;
    public int nocwnd, stream;
    public KcpCongestionControlOperations* ccops;
    public void* congest;
    public KcpLogFlags logmask;
    public delegate* unmanaged[Cdecl]<byte*, int, KcpContext*, void*, int> output;
    public delegate* unmanaged[Cdecl]<byte*, KcpContext*, void*, void> writelog;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KcpPipeOption ToPipeOption()
        => new KcpPipeOption()
        {
            StreamMode = stream != 0,
            ConversationId = conv,
            Mtu = mtu,
            SendWindow = snd_wnd,
            ReceiveWindow = rcv_wnd,
            NoDelay = nodelay,
            Interval = interval,
            FastResend = fastresend,
            NoCongestionControl = nocwnd != 0
        };
}
