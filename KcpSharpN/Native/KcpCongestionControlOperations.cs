using System.Runtime.InteropServices;

namespace KcpSharpN.Native;

#pragma warning disable CS1591

//---------------------------------------------------------------------
// IKCPOPS - pluggable congestion control operations
//---------------------------------------------------------------------
/// <summary>
/// The pluggable congestion control operations of the KCP protocol.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct KcpCongestionControlOperations
{
    /// <summary>
    /// const char* name;
    /// </summary>
    public char* name;
    /// <summary>
    /// int (* init) (ikcpcb* kcp);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, int> init;
    /// <summary>
    /// void (* release) (ikcpcb* kcp);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, void> release;
    /// <summary>
    /// void (*on_ack)(ikcpcb *kcp, IUINT32 acked_segs, IUINT32 acked_bytes,
    ///                 IUINT32 prior_in_flight);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, uint, uint, uint, void> on_ack;
    /// <summary>
    /// void (* on_fast_retransmit) (ikcpcb* kcp, IUINT32 fast_retrans,
    ///             IUINT32 inflight, IUINT32 prior_cwnd);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, uint, uint, uint, void> on_fast_retransmit;
    /// <summary>
    /// void (* on_timeout) (ikcpcb* kcp, IUINT32 prior_cwnd);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, uint, void> on_timeout;
    /// <summary>
    /// void (* on_tick) (ikcpcb* kcp);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, void> on_tick;
    /// <summary>
    /// void (* on_app_limited) (ikcpcb* kcp, IUINT32 inflight);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, uint, void> on_app_limited;
    /// <summary>
    /// void (* on_rtt) (ikcpcb* kcp, IINT32 rtt);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, int, void> on_rtt;
    /// <summary>
    /// void (* on_pkt_sent) (ikcpcb* kcp, IUINT32 sn, IUINT32 ts,
    ///             IUINT32 len, IUINT32 inflight, IUINT32 xmit);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, uint, uint, uint, uint, uint, void> on_pkt_sent;
    /// <summary>
    /// void (* on_pkt_acked) (ikcpcb, IUINT32 sn, IUINT32 ts,
    ///             IUINT32 len, IINT32 rtt, IUINT32 xmit)
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, uint, uint, uint, int, uint, void> on_pkt_acked;
    /// <summary>
    /// IUINT32(*get_info)(ikcpcb* kcp, void* buf, IUINT32 bufsize);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, void*, uint, uint> get_info;
    /// <summary>
    /// IUINT32(*pacing_rate)(ikcpcb* kcp);
    /// </summary>
    public delegate* unmanaged[Cdecl]<KcpContext*, uint> pacing_rate;
}