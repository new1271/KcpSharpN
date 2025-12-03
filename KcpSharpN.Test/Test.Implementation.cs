using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using KcpSharpN.Native;

#pragma warning disable IDE1006

namespace KcpSharpN.Test
{
    unsafe partial class Test
    {
        [StructLayout(LayoutKind.Explicit, Pack = 8)]
        private struct Parameter
        {
            [FieldOffset(0)]
            public int id;
            [FieldOffset(0)]
            public void* ptr;
        }

        // 模拟网络
        private static LatencySimulator? _vnet;

        // 模拟网络：模拟发送一个 udp包
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int udp_output(byte* buf, int len, KcpContext* kcp, void* user)
        {
            LatencySimulator? vnet = _vnet;
            if (vnet is null)
                return -1;
            Unsafe.SkipInit(out Parameter parameter);
            parameter.ptr = user;
            vnet.send(parameter.id, buf, len);
            return 0;
        }

        // 测试用例
        [SkipLocalsInit]
        public static partial void test(int mode)
        {
            // 创建模拟网络：丢包率10%，Rtt 60ms~125ms
            LatencySimulator vnet = new LatencySimulator(10, 60, 125);
            _vnet = vnet;

            // 创建两个端点的 kcp对象，第一个参数 conv是会话编号，同一个会话需要相同
            // 最后一个是 user参数，用来传递标识
            KcpContext* kcp1 = Kcp.ikcp_create(0x11223344, (void*)0);
            KcpContext* kcp2 = Kcp.ikcp_create(0x11223344, (void*)1);

            // 设置kcp的下层输出，这里为 udp_output，模拟udp网络输出函数
            delegate* unmanaged[Cdecl]<byte*, int, KcpContext*, void*, int> output = &udp_output;
            kcp1->output = output;
            kcp2->output = output;

            uint current = TimeHelper.iclock();
            uint slap = current + 20;
            uint index = 0;
            uint next = 0;
            long sumrtt = 0;
            int count = 0;
            int maxrtt = 0;

            // 配置窗口大小：平均延迟200ms，每20ms发送一个包，
            // 而考虑到丢包重发，设置最大收发窗口为128
            Kcp.ikcp_wndsize(kcp1, 128, 128);
            Kcp.ikcp_wndsize(kcp2, 128, 128);

            // 判断测试用例的模式
            if (mode == 0)
            {
                // 默认模式
                Kcp.ikcp_nodelay(kcp1, 0, 10, 0, 0);
                Kcp.ikcp_nodelay(kcp2, 0, 10, 0, 0);
            }
            else if (mode == 1)
            {
                // 普通模式，关闭流控等
                Kcp.ikcp_nodelay(kcp1, 0, 10, 0, 1);
                Kcp.ikcp_nodelay(kcp2, 0, 10, 0, 1);
            }
            else
            {
                // 启动快速模式
                // 第二个参数 nodelay-启用以后若干常规加速将启动
                // 第三个参数 interval为内部处理时钟，默认设置为 10ms
                // 第四个参数 resend为快速重传指标，设置为2
                // 第五个参数 为是否禁用常规流控，这里禁止
                Kcp.ikcp_nodelay(kcp1, 2, 10, 2, 1);
                Kcp.ikcp_nodelay(kcp2, 2, 10, 2, 1);
                kcp1->rx_minrto = 10;
                kcp1->fastresend = 1;
            }

            byte* buffer = stackalloc byte[2000];
            int hr;

            uint ts1 = TimeHelper.iclock();

            while (true)
            {
                TimeHelper.isleep(1);
                current = TimeHelper.iclock();
                Kcp.ikcp_update(kcp1, TimeHelper.iclock());
                Kcp.ikcp_update(kcp2, TimeHelper.iclock());

                // 每隔 20ms，kcp1发送数据
                for (; current >= slap; slap += 20)
                {
                    ((uint*)buffer)[0] = index++;
                    ((uint*)buffer)[1] = current;

                    // 发送上层协议包
                    Kcp.ikcp_send(kcp1, buffer, 8);
                }

                // 处理虚拟网络：检测是否有udp包从p1->p2
                while (true)
                {
                    hr = vnet.recv(1, buffer, 2000);
                    if (hr < 0) break;
                    // 如果 p2收到udp，则作为下层协议输入到kcp2
                    Kcp.ikcp_input(kcp2, buffer, hr);
                }

                // 处理虚拟网络：检测是否有udp包从p2->p1
                while (true)
                {
                    hr = vnet.recv(0, buffer, 2000);
                    if (hr < 0) break;
                    // 如果 p1收到udp，则作为下层协议输入到kcp1
                    Kcp.ikcp_input(kcp1, buffer, hr);
                }

                // kcp2接收到任何包都返回回去
                while (true)
                {
                    hr = Kcp.ikcp_recv(kcp2, buffer, 10);
                    // 没有收到包就退出
                    if (hr < 0) break;
                    // 如果收到包就回射
                    Kcp.ikcp_send(kcp2, buffer, hr);
                }

                // kcp1收到kcp2的回射数据
                while (true)
                {
                    hr = Kcp.ikcp_recv(kcp1, buffer, 10);
                    // 没有收到包就退出
                    if (hr < 0) break;
                    uint sn = *(uint*)(buffer + 0);
                    uint ts = *(uint*)(buffer + 4);
                    uint rtt = current - ts;

                    if (sn != next)
                    {
                        // 如果收到的包不连续
                        Console.Write($"ERROR sn {count:D}<->{next:D}\n");
                        return;
                    }

                    next++;
                    sumrtt += rtt;
                    count++;
                    if (rtt > (uint)maxrtt) maxrtt = (int)rtt;

                    Console.Write($"[RECV] mode={mode:D} sn={sn:D} rtt={rtt:D}\n");
                }
                if (next > 1000) break;
            }

            ts1 = TimeHelper.iclock() - ts1;

            Kcp.ikcp_release(kcp1);
            Kcp.ikcp_release(kcp2);

            string[] names = new string[3] { "default", "normal", "fast" };
            Console.Write($"{names[mode]} mode result ({ts1:D}ms):\n");
            Console.Write($"avgrtt={(int)(sumrtt / count):D} maxrtt={maxrtt:D} tx={vnet.tx1:D}\n");
            Console.Write("press enter to next ...\n");
            Console.ReadLine();
        }
    }
}
