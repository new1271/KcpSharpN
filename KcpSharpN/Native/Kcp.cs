//=====================================================================
//
// KCP - A Better ARQ Protocol Implementation
// skywind3000 (at) gmail.com, 2010-2011
//  
// Features:
// + Average RTT reduce 30% - 40% vs traditional ARQ like tcp.
// + Maximum RTT reduce three times vs tcp.
// + Lightweight, distributed as a single source file.
//
//=====================================================================

#pragma warning disable IDE1006

namespace KcpSharpN.Native
{
    public static unsafe partial class Kcp
    {
        //---------------------------------------------------------------------
        // interface
        //---------------------------------------------------------------------

        /// <summary>
        /// Create a new kcp context.
        /// </summary>
        /// <param name="conv">
        /// Conversation id.<br/>
        /// Must equal in two endpoint from the same connection.
        /// </param>
        /// <param name="user">
        /// User defined object.<br/>
        /// It will be passed to the output callback.</param>
        /// <returns></returns>
        /// <remarks>
        /// The output callback can be setup like this: 'kcp->output = my_udp_output'
        /// </remarks>
        public static partial KcpContext* ikcp_create(uint conv, void* user);

        /// <summary>
        /// Release kcp context.
        /// </summary>
        /// <param name="kcp">The context will be released.</param>
        public static partial void ikcp_release(KcpContext* kcp);

        /// <summary>
        /// Set output callback.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="output">The callback function which will be invoked by <see cref="Kcp"/></param>
        public static partial void ikcp_setoutput(KcpContext* kcp, delegate* unmanaged[Cdecl]<byte*, int, KcpContext*, void*, int> output);

        /// <summary>
        /// Receives data for user/upper level.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="buffer">The buffer which data will be copy to</param>
        /// <param name="len">The length of <paramref name="buffer"/></param>
        /// <returns>The size for received data when the value is positive or zero, or else for EAGAIN</returns>
        public static partial int ikcp_recv(KcpContext* kcp, byte* buffer, int len);

        /// <summary>
        /// Sends data for user/upper level.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="buffer">The buffer contains data</param>
        /// <param name="len">The length of <paramref name="buffer"/></param>
        /// <returns>The return value below zero for error.</returns>
        public static partial int ikcp_send(KcpContext* kcp, byte* buffer, int len);

        /// <summary>
        /// Updates the state of <paramref name="kcp"/> (call it repeatedly, every 10ms-100ms), <br/>
        /// or you can ask <see cref="ikcp_check(KcpContext*, uint)"/> when to call it again 
        /// (without <see cref="ikcp_input(KcpContext*, byte*, nint)"/>/<see cref="ikcp_send(KcpContext*, byte*, int)"/> calling).
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="current">Current timestamp in milliseconds</param>
        public static partial void ikcp_update(KcpContext* kcp, uint current);

        /// <summary>
        /// Determine when should you invoke <see cref="ikcp_update(KcpContext*, uint)"/>.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="current">Current timestamp in milliseconds</param>
        /// <returns>
        /// When you should invoke <see cref="ikcp_update(KcpContext*, uint)"/> in milliseconds.<br/>
        /// If there is no <see cref="ikcp_input(KcpContext*, byte*, nint)"/>/<see cref="ikcp_send(KcpContext*, byte*, int)"/> calling.<br/>
        /// you can call <see cref="ikcp_update(KcpContext*, uint)"/> in that time, instead of call update repeatly. 
        /// </returns>
        /// <remarks>
        /// Important to reduce unnecessary <see cref="ikcp_update(KcpContext*, uint)"/> invoking. <br/>
        /// use it to schedule <see cref="ikcp_update(KcpContext*, uint)"/> (eg. implementing an epoll-like mechanism, 
        /// or optimize <see cref="ikcp_update(KcpContext*, uint)"/> when handling massive kcp connections)
        /// </remarks>
        public static partial uint ikcp_check(KcpContext* kcp, uint current);

        /// <summary>
        /// Inputs packet from the low level (eg. UDP socket).
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="data">The buffer contains data</param>
        /// <param name="size">The size of <paramref name="data"/></param>
        /// <returns></returns>
        public static partial int ikcp_input(KcpContext* kcp, byte* data, nint size);

        /// <summary>
        /// Flush pending data.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        public static partial void ikcp_flush(KcpContext* kcp);

        /// <summary>
        /// Check the size of next message in the receive queue.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <returns>The size of next message.</returns>
        public static partial int ikcp_peeksize(KcpContext* kcp);

        /// <summary>
        /// Change MTU size.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="mtu">The maximum transmission unit(MTU) for <paramref name="kcp"/>, default is 1400</param>
        /// <returns>
        /// Possible values: <br/>
        /// <list type="bullet">
        /// <item>0 when successed</item>
        /// <item>-1 if <paramref name="mtu"/> is larger than <see cref="IKCP_OVERHEAD"/></item>
        /// <item>-2 if allocates memory failed</item>
        /// </list>
        /// </returns>
        public static partial int ikcp_setmtu(KcpContext* kcp, int mtu);

        /// <summary>
        /// Set maximum window size
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="sndwnd">The sending window size, default is 32</param>
        /// <param name="rcvwnd">The receiving window size, default is 32</param>
        /// <returns>Always be zero</returns>
        public static partial int ikcp_wndsize(KcpContext* kcp, int sndwnd, int rcvwnd);

        /// <summary>
        /// To get how many packet is waiting to be sent
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <returns>The number how many packet has to be sent</returns>
        public static partial int ikcp_waitsnd(KcpContext* kcp);

        /// <summary>
        /// Set enable or disable No-Delay mode.
        /// </summary>
        /// <param name="kcp">The context will be involved.</param>
        /// <param name="nodelay">0 for disable(default), 1 for enable.</param>
        /// <param name="interval">Internal update timer interval in milliseconds, default is 100ms.</param>
        /// <param name="resend">0 for disable fast resend(default), 1 or greater for enable fast resend (the number will be the maximum resend times).</param>
        /// <param name="nc">0 for normal congestion control(default), 1 for disable congestion control.</param>
        /// <returns>Always be zero</returns>
        /// <remarks>
        /// If you want fastest performance, you can try ikcp_nodelay(kcp, true, 20, 2, true)
        /// </remarks>
        public static partial int ikcp_nodelay(KcpContext* kcp, int nodelay, int interval, int resend, int nc);

        public static partial void ikcp_log(KcpContext* kcp, KcpLogFlags mask, string fmt);

        public static partial void ikcp_log(KcpContext* kcp, KcpLogFlags mask, string fmt, params object[] args);

        // setup allocator
        public static partial void ikcp_allocator(delegate* unmanaged[Cdecl]<nuint, void*> new_malloc, delegate* unmanaged[Cdecl]<void*, void> new_free);

        // read conv
        public static partial uint ikcp_getconv(void* ptr);
    }
}
