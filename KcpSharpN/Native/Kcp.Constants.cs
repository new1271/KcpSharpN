namespace KcpSharpN.Native
{
    partial class Kcp
    {
        //=====================================================================
        // KCP BASIC
        //=====================================================================
        public const int IKCP_RTO_NDL = 30;        // no delay min rto
        public const int IKCP_RTO_MIN = 100;       // normal min rto
        public const int IKCP_RTO_DEF = 200;
        public const int IKCP_RTO_MAX = 60000;
        public const int IKCP_CMD_PUSH = 81;       // cmd: push data
        public const int IKCP_CMD_ACK = 82;        // cmd: ack
        public const int IKCP_CMD_WASK = 83;       // cmd: window probe (ask)
        public const int IKCP_CMD_WINS = 84;       // cmd: window size (tell)
        public const int IKCP_ASK_SEND = 1;        // need to send IKCP_CMD_WASK
        public const int IKCP_ASK_TELL = 2;        // need to send IKCP_CMD_WINS
        public const int IKCP_WND_SND = 32;
        public const int IKCP_WND_RCV = 128;       // must >= max fragment size
        public const int IKCP_MTU_DEF = 1400;
        public const int IKCP_ACK_FAST = 3;
        public const int IKCP_INTERVAL = 100;
        public const int IKCP_OVERHEAD = 24;
        public const int IKCP_DEADLINK = 20;
        public const int IKCP_THRESH_INIT = 2;
        public const int IKCP_THRESH_MIN = 2;
        public const int IKCP_PROBE_INIT = 7000;       // 7 secs to probe window size
        public const int IKCP_PROBE_LIMIT = 120000;    // up to 120 secs to probe window
        public const int IKCP_FASTACK_LIMIT = 5;       // max times to trigger fastack
    }
}
