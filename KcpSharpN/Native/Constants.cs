namespace KcpSharpN.Native
{
    internal static class Constants
    {
        //=====================================================================
        // KCP BASIC
        //=====================================================================
        public const uint IKCP_RTO_NDL = 30;        // no delay min rto
        public const uint IKCP_RTO_MIN = 100;       // normal min rto
        public const uint IKCP_RTO_DEF = 200;
        public const uint IKCP_RTO_MAX = 60000;
        public const uint IKCP_CMD_PUSH = 81;       // cmd: push data
        public const uint IKCP_CMD_ACK = 82;        // cmd: ack
        public const uint IKCP_CMD_WASK = 83;       // cmd: window probe (ask)
        public const uint IKCP_CMD_WINS = 84;       // cmd: window size (tell)
        public const uint IKCP_ASK_SEND = 1;        // need to send IKCP_CMD_WASK
        public const uint IKCP_ASK_TELL = 2;        // need to send IKCP_CMD_WINS
        public const uint IKCP_WND_SND = 32;
        public const uint IKCP_WND_RCV = 128;       // must >= max fragment size
        public const uint IKCP_MTU_DEF = 1400;
        public const uint IKCP_ACK_FAST = 3;
        public const uint IKCP_INTERVAL = 100;
        public const uint IKCP_OVERHEAD = 24;
        public const uint IKCP_DEADLINK = 20;
        public const uint IKCP_THRESH_INIT = 2;
        public const uint IKCP_THRESH_MIN = 2;
        public const uint IKCP_PROBE_INIT = 7000;       // 7 secs to probe window size
        public const uint IKCP_PROBE_LIMIT = 120000;    // up to 120 secs to probe window
        public const uint IKCP_FASTACK_LIMIT = 5;		// max times to trigger fastack
    }
}
