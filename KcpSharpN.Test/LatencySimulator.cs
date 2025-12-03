using System.Runtime.CompilerServices;

using DelayTunnel = System.Collections.Generic.List<KcpSharpN.Test.DelayPacket>;

#pragma warning disable IDE1006

namespace KcpSharpN.Test
{
    /// <summary>
    /// 网络延迟模拟器
    /// </summary>
    internal sealed class LatencySimulator
    {
        public int tx1, tx2;

        private uint current;
        private int lostrate;
        private int rttmin;
        private int rttmax;
        private int nmax;
        private DelayTunnel p12;
        private DelayTunnel p21;
        private Random r12;
        private Random r21;

        // lostrate: 往返一周丢包率的百分比，默认 10%
        // rttmin：rtt最小值，默认 60
        // rttmax：rtt最大值，默认 125
        public LatencySimulator(int lostrate = 10, int rttmin = 60, int rttmax = 125, int nmax = 1000)
        {
            r12 = new Random(100);
            r21 = new Random(100);
            p12 = new DelayTunnel();
            p21 = new DelayTunnel();

            current = TimeHelper.iclock();
            this.lostrate = lostrate / 2;  // 上面数据是往返丢包率，单程除以2
            this.rttmin = rttmin / 2;
            this.rttmax = rttmax / 2;
            this.nmax = nmax;
            tx1 = tx2 = 0;
        }

        // 清除数据
        public void clear()
        {
            p12.ForEach(static obj => obj.Dispose());
            p21.ForEach(static obj => obj.Dispose());
            p12.Clear();
            p21.Clear();
        }

        // 发送数据
        // peer - 端点0/1，从0发送，从1接收；从1发送从0接收
        public unsafe void send(int peer, void* data, int size)
        {
            if (peer == 0)
            {
                tx1++;
                if (r12.random() < lostrate) return;
                if (p12.Count >= nmax) return;
            }
            else
            {
                tx2++;
                if (r21.random() < lostrate) return;
                if (p21.Count >= nmax) return;
            }
            DelayPacket pkt = new DelayPacket(size, data);
            current = TimeHelper.iclock();
            int delay = rttmin;
            if (rttmax > rttmin)
                delay += Random.rand() % (rttmax - rttmin);
            pkt.setts(current + (uint)delay);
            if (peer == 0)
            {
                p12.Add(pkt);
            }
            else
            {
                p21.Add(pkt);
            }
        }

        // 接收数据
        public unsafe int recv(int peer, void* data, int maxsize)
        {
            DelayTunnel turnel;
            int index = 0;
            if (peer == 0)
                turnel = p21;
            else
                turnel = p12;
            if (turnel.Count == 0)
                return -1;
            DelayPacket pkt = turnel[index];
            current = TimeHelper.iclock();
            if (current < pkt.ts()) 
                return -2;
            if (maxsize < pkt.size()) 
                return -3;
            turnel.RemoveAt(index);
            maxsize = pkt.size();
            Unsafe.CopyBlock(data, pkt.ptr(), (uint)maxsize);
            pkt.Dispose();
            return maxsize;
        }
    }
}
