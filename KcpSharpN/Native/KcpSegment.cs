using System.Runtime.InteropServices;

namespace KcpSharpN.Native
{
    //=====================================================================
    // SEGMENT
    //=====================================================================
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct KcpSegment
    {
        public KcpQueueHead node;
        public uint conv;
        public uint cmd;
        public uint frg;
        public uint wnd;
        public uint ts;
        public uint sn;
        public uint una;
        public uint len;
        public uint resendts;
        public uint rto;
        public uint fastack;
        public uint xmit;
        public fixed byte data[1];
    }
}
