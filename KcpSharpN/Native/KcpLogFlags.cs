namespace KcpSharpN.Native
{
    public enum KcpLogFlags : int
    {
        None = 0,
        Output = 1,
        Input = 2,
        Send = 4,
        Receive = 8,
        IncomingData = 16,
        IncomingAck = 32,
        IncomingProbe = 64,
        IncomingWindows = 128,
        OutgoingData = 256,
        OutgoingAck = 512,
        OutgoingProbe = 1024,
        OutgoingWindows = 2048,
    }
}
