using System.Runtime.InteropServices;

using KcpSharpN.Native;

namespace KcpSharpN;

#pragma warning disable CS1591

/// <summary>
/// The option for creating the KCP context
/// </summary>
[StructLayout(LayoutKind.Auto)]
public struct KcpPipeOption
{
    public bool StreamMode;
    public uint ConversationId;
    public uint Mtu;
    public uint Interval;
    public uint NoDelay;
    public uint SendWindow;
    public uint ReceiveWindow;
    public int FastResend;
    public bool NoCongestionControl;

    /// <summary>
    /// Get default conversation of the Kcp connection.
    /// </summary>
    public static unsafe KcpPipeOption GetDefaultPipeOption()
    {
        KcpContext* kcp = Kcp.ikcp_create(conv: 0, null);
        KcpPipeOption option = kcp->ToPipeOption();
        Kcp.ikcp_release(kcp);
        return option;
    }
}
