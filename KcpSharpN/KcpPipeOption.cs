using System.Runtime.InteropServices;

using KcpSharpN.Native;

namespace KcpSharpN
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct KcpPipeOption
    {
        public bool StreamMode;
        public uint ConversationId, Mtu,
            SendWindow, ReceiveWindow,
            NoDelay, Interval;
        public int FastResend, CongestionControl;

        public static unsafe KcpPipeOption GetDefaultPipeOption(uint conversationId)
        {
            KcpContext* kcp = Kcp.ikcp_create(conversationId, null);
            KcpPipeOption option = kcp->ToPipeOption();
            Kcp.ikcp_release(kcp);
            return option;
        }
    }
}
