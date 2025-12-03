#define IKCP_FASTACK_CONSERVE

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using InlineMethod;

#pragma warning disable IDE1006

namespace KcpSharpN.Native
{
    unsafe partial class Kcp
    {
        private static readonly nuint SegmentNodeOffset =
            unchecked((nuint)(0));

        //---------------------------------------------------------------------
        // manage segment
        //---------------------------------------------------------------------

        private static delegate* unmanaged[Cdecl]<nuint, void*> ikcp_malloc_hook = null;
        private static delegate* unmanaged[Cdecl]<void*, void> ikcp_free_hook = null;

        // internal malloc
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void* ikcp_malloc(nuint size)
        {
            delegate* unmanaged[Cdecl]<nuint, void*> hooked_malloc = ikcp_malloc_hook;
            if (hooked_malloc != null)
                return hooked_malloc(size);
            return MemoryHelper.malloc(size);
        }

        // internal free
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void ikcp_free(void* ptr)
        {
            delegate* unmanaged[Cdecl]<void*, void> hooked_free = ikcp_free_hook;
            if (hooked_free != null)
                hooked_free(ptr);
            else
                MemoryHelper.free(ptr);
        }

        [Inline(InlineBehavior.Remove)]
        private static void ikcp_free_safe(ref void* ptr)
        {
            if (ptr == null)
                return;
            ikcp_free(ptr);
            ptr = null;
        }

        [Inline(InlineBehavior.Remove)]
        private static void ikcp_free_safe<T>(ref T* ptr) where T : unmanaged
        {
            if (ptr == null)
                return;
            ikcp_free(ptr);
            ptr = null;
        }

        public static partial void ikcp_allocator(
            delegate* unmanaged[Cdecl]<nuint, void*> new_malloc,
            delegate* unmanaged[Cdecl]<void*, void> new_free)
        {
            ikcp_malloc_hook = new_malloc;
            ikcp_free_hook = new_free;
        }

        // allocate a new kcp segment
        [Inline(InlineBehavior.Remove)]
        private static KcpSegment* ikcp_segment_new(KcpContext* kcp, nuint size) => (KcpSegment*)ikcp_malloc(SizeOf<KcpSegment>() + size);

        // delete a segment
        [Inline(InlineBehavior.Remove)]
        private static void ikcp_segment_delete(KcpContext* kcp, KcpSegment* seg) => ikcp_free(seg);

        // write log
        public static partial void ikcp_log(KcpContext* kcp, KcpLogFlags mask, string fmt)
        {
            if ((mask & kcp->logmask) == 0)
                return;
            delegate* unmanaged[Cdecl]<byte*, KcpContext*, void*, void> loggerFunc = kcp->writelog;
            if (loggerFunc == null)
                return;
            ikcp_log_internal(kcp, loggerFunc, fmt);
        }

        // write log
        public static partial void ikcp_log(KcpContext* kcp, KcpLogFlags mask, string fmt, params object[] args)
        {
            if ((mask & kcp->logmask) == 0)
                return;
            delegate* unmanaged[Cdecl]<byte*, KcpContext*, void*, void> loggerFunc = kcp->writelog;
            if (loggerFunc == null)
                return;
            ikcp_log_internal(kcp, loggerFunc, string.Format(fmt, args));
        }

        [SkipLocalsInit]
        private static void ikcp_log_internal(KcpContext* kcp, delegate* unmanaged[Cdecl]<byte*, KcpContext*, void*, void> loggerFunc, string log)
        {
            Span<byte> buffer = stackalloc byte[1024];
            if (!Encoding.UTF8.TryGetBytes(log, buffer, out int bytesWritten))
                return;
            buffer[bytesWritten] = 0;
            fixed (byte* ptr = buffer)
                loggerFunc(ptr, kcp, kcp->user);
        }

        // check log mask
        private static bool ikcp_canlog(KcpContext* kcp, KcpLogFlags mask, out delegate* unmanaged[Cdecl]<byte*, KcpContext*, void*, void> loggerFunc)
        {
            if ((mask & kcp->logmask) != 0)
                return (loggerFunc = kcp->writelog) != null;
            loggerFunc = null;
            return false;
        }

        // output segment
        public static int ikcp_output(KcpContext* kcp, void* data, int size)
        {
            if (kcp == null)
                throw new ArgumentNullException(nameof(kcp));
            delegate* unmanaged[Cdecl]<byte*, int, KcpContext*, void*, int> outputFunc = kcp->output;
            if (outputFunc == null)
                throw new ArgumentNullException(nameof(kcp) + "->" + nameof(KcpContext.output));
            if (ikcp_canlog(kcp, KcpLogFlags.Output, out var loggerFunc))
                ikcp_log_internal(kcp, loggerFunc, $"[RO] {size:D} bytes");
            if (size == 0)
                return 0;
            return outputFunc((byte*)data, size, kcp, kcp->user);
        }

        // output queue
        [Conditional("DEBUG")]
        private static void ikcp_qprint(string name, KcpQueueHead* head)
        {
#if true
            KcpQueueHead* p;
            Console.Write($"<{name}>: [");
            for (p = head->next; p != head; p = p->next)
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                Console.Write($"({seg->sn:D} {(seg->ts % 10000):D})");
                if (p->next != head)
                    Console.Write(",");
            }
            Console.Write("]\n");
#endif
        }

        //---------------------------------------------------------------------
        // create a new kcpcb
        //---------------------------------------------------------------------
        public static partial KcpContext* ikcp_create(uint conv, void* user)
        {
            KcpContext* kcp = (KcpContext*)ikcp_malloc(SizeOf<KcpContext>());
            if (kcp == null)
                return null;
            kcp->conv = conv;
            kcp->user = user;
            kcp->snd_una = 0;
            kcp->snd_nxt = 0;
            kcp->rcv_nxt = 0;
            kcp->ts_recent = 0;
            kcp->ts_lastack = 0;
            kcp->ts_probe = 0;
            kcp->probe_wait = 0;
            kcp->snd_wnd = IKCP_WND_SND;
            kcp->rcv_wnd = IKCP_WND_RCV;
            kcp->rmt_wnd = IKCP_WND_RCV;
            kcp->cwnd = 0;
            kcp->incr = 0;
            kcp->probe = 0;
            kcp->mtu = IKCP_MTU_DEF;
            kcp->mss = kcp->mtu - IKCP_OVERHEAD;
            kcp->stream = 0;

            kcp->buffer = (byte*)ikcp_malloc((kcp->mtu + IKCP_OVERHEAD) * 3);
            if (kcp->buffer == null)
            {
                ikcp_free(kcp);
                return null;
            }

            KcpQueue.Initialize(&kcp->snd_queue);
            KcpQueue.Initialize(&kcp->rcv_queue);
            KcpQueue.Initialize(&kcp->snd_buf);
            KcpQueue.Initialize(&kcp->rcv_buf);

            kcp->nrcv_buf = 0;
            kcp->nsnd_buf = 0;
            kcp->nrcv_que = 0;
            kcp->nsnd_que = 0;
            kcp->state = 0;
            kcp->acklist = null;
            kcp->ackblock = 0;
            kcp->ackcount = 0;
            kcp->rx_srtt = 0;
            kcp->rx_rttval = 0;
            kcp->rx_rto = IKCP_RTO_DEF;
            kcp->rx_minrto = IKCP_RTO_MIN;
            kcp->current = 0;
            kcp->interval = IKCP_INTERVAL;
            kcp->ts_flush = IKCP_INTERVAL;
            kcp->nodelay = 0;
            kcp->updated = 0;
            kcp->logmask = 0;
            kcp->ssthresh = IKCP_THRESH_INIT;
            kcp->fastresend = 0;
            kcp->fastlimit = IKCP_FASTACK_LIMIT;
            kcp->nocwnd = 0;
            kcp->xmit = 0;
            kcp->dead_link = IKCP_DEADLINK;
            kcp->output = null;
            kcp->writelog = null;

            return kcp;
        }

        //---------------------------------------------------------------------
        // release a new kcpcb
        //---------------------------------------------------------------------
        public static partial void ikcp_release(KcpContext* kcp)
        {
            if (kcp == null)
                throw new ArgumentNullException(nameof(kcp));
            kcp_release_queue(kcp, &kcp->snd_buf);
            kcp_release_queue(kcp, &kcp->rcv_buf);
            kcp_release_queue(kcp, &kcp->snd_queue);
            kcp_release_queue(kcp, &kcp->rcv_queue);
            ikcp_free_safe(ref kcp->buffer);
            ikcp_free_safe(ref kcp->acklist);
            kcp->nrcv_buf = 0;
            kcp->nsnd_buf = 0;
            kcp->nrcv_que = 0;
            kcp->nsnd_que = 0;
            kcp->ackcount = 0;
            ikcp_free(kcp);
        }

        [Inline(InlineBehavior.Remove)]
        private static void kcp_release_queue(KcpContext* kcp, KcpQueueHead* queue)
        {
            while (!KcpQueue.IsEmpty(queue))
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(queue->next, SegmentNodeOffset);
                KcpQueue.Delete(&seg->node);
                ikcp_segment_delete(kcp, seg);
            }
        }



        //---------------------------------------------------------------------
        // set output callback, which will be invoked by kcp
        //---------------------------------------------------------------------
        public static partial void ikcp_setoutput(KcpContext* kcp, delegate* unmanaged[Cdecl]<byte*, int, KcpContext*, void*, int> output)
        {
            kcp->output = output;
        }

        //---------------------------------------------------------------------
        // user/upper level recv: returns size, returns below zero for EAGAIN
        //---------------------------------------------------------------------
        public static partial int ikcp_recv(KcpContext* kcp, byte* buffer, int len)
        {
            if (kcp == null)
                throw new ArgumentNullException(nameof(kcp));

            KcpQueueHead* p;
            int ispeek = (len < 0) ? 1 : 0;
            int peeksize;
            bool recover = false;
            KcpSegment* seg;

            if (KcpQueue.IsEmpty(&kcp->rcv_queue))
                return -1;

            if (len < 0) len = -len;

            peeksize = ikcp_peeksize(kcp);

            if (peeksize < 0)
                return -2;

            if (peeksize > len)
                return -3;

            if (kcp->nrcv_que >= kcp->rcv_wnd)
                recover = true;

            // merge fragment
            for (len = 0, p = kcp->rcv_queue.next; p != &kcp->rcv_queue;)
            {
                int fragment;
                seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                p = p->next;

                if (buffer != null)
                {
                    memcpy(buffer, seg->data, seg->len);
                    buffer += seg->len;
                }

                len += unchecked((int)seg->len);
                fragment = unchecked((int)seg->frg);

                if (ikcp_canlog(kcp, KcpLogFlags.Receive, out delegate* unmanaged[Cdecl]<byte*, KcpContext*, void*, void> loggerFunc))
                {
                    ikcp_log_internal(kcp, loggerFunc, $"recv sn={seg->sn:D}");
                }

                if (ispeek == 0)
                {
                    KcpQueue.Delete(&seg->node);
                    ikcp_segment_delete(kcp, seg);
                    kcp->nrcv_que--;
                }

                if (fragment == 0)
                    break;
            }

            Debug.Assert(len == peeksize);

            // move available data from rcv_buf -> rcv_queue
            while (!KcpQueue.IsEmpty(&kcp->rcv_buf))
            {
                seg = KcpQueue.GetEntry<KcpSegment>(kcp->rcv_buf.next, SegmentNodeOffset);
                if (seg->sn == kcp->rcv_nxt && kcp->nrcv_que < kcp->rcv_wnd)
                {
                    KcpQueue.Delete(&seg->node);
                    kcp->nrcv_buf--;
                    KcpQueue.AddTail(&seg->node, &kcp->rcv_queue);
                    kcp->nrcv_que++;
                    kcp->rcv_nxt++;
                }
                else
                {
                    break;
                }
            }

            // fast recover
            if (kcp->nrcv_que < kcp->rcv_wnd && recover)
            {
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                kcp->probe |= IKCP_ASK_TELL;
            }

            return len;
        }

        //---------------------------------------------------------------------
        // peek data size
        //---------------------------------------------------------------------
        public static partial int ikcp_peeksize(KcpContext* kcp)
        {
            if (kcp == null)
                throw new ArgumentNullException(nameof(kcp));

            KcpQueueHead* p;
            KcpSegment* seg;
            int length = 0;

            if (KcpQueue.IsEmpty(&kcp->rcv_queue)) return -1;

            seg = KcpQueue.GetEntry<KcpSegment>(kcp->rcv_queue.next, SegmentNodeOffset);
            if (seg->frg == 0)
                return unchecked((int)seg->len);

            if (kcp->nrcv_que < seg->frg + 1) return -1;

            for (p = kcp->rcv_queue.next; p != &kcp->rcv_queue; p = p->next)
            {
                seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                length += unchecked((int)seg->len);
                if (seg->frg == 0) break;
            }

            return length;
        }


        //---------------------------------------------------------------------
        // user/upper level send, returns below zero for error
        //---------------------------------------------------------------------
        public static partial int ikcp_send(KcpContext* kcp, byte* buffer, int len)
        {
            KcpSegment* seg;
            int count, i;
            int sent = 0;

            if (kcp->mss <= 0)
                throw new ArgumentOutOfRangeException(nameof(kcp) + "->" + nameof(KcpContext.mss));
            if (len < 0)
                return -1;

            // append to previous segment in streaming mode (if possible)
            if (kcp->stream != 0)
            {
                if (!KcpQueue.IsEmpty(&kcp->snd_queue))
                {
                    KcpSegment* old = KcpQueue.GetEntry<KcpSegment>(kcp->snd_queue.prev, SegmentNodeOffset);
                    if (old->len < kcp->mss)
                    {
                        int capacity = unchecked((int)(kcp->mss - old->len));
                        int extend = (len < capacity) ? len : capacity;
                        seg = ikcp_segment_new(kcp, old->len + (uint)extend);
                        if (seg == null)
                            return -2;
                        KcpQueue.AddTail(&seg->node, &kcp->snd_queue);
                        memcpy(seg->data, old->data, old->len);
                        if (buffer != null)
                        {
                            memcpy(seg->data + old->len, buffer, (uint)extend);
                            buffer += extend;
                        }
                        seg->len = old->len + (uint)extend;
                        seg->frg = 0;
                        len -= extend;
                        KcpQueue.DeleteAndInitialize(&old->node);
                        ikcp_segment_delete(kcp, old);
                        sent = extend;
                    }
                }
                if (len <= 0)
                {
                    return sent;
                }
            }

            if
                (len <= (int)kcp->mss) count = 1;
            else
                count = (int)((len + kcp->mss - 1) / kcp->mss);

            if (count >= IKCP_WND_RCV)
            {
                if (kcp->stream != 0 && sent > 0)
                    return sent;
                return -2;
            }

            if (count == 0) count = 1;

            // fragment
            for (i = 0; i < count; i++)
            {
                int size = len > (int)kcp->mss ? (int)kcp->mss : len;
                seg = ikcp_segment_new(kcp, (uint)size);
                if (seg == null)
                    return -2;
                if (buffer != null && len > 0)
                {
                    memcpy(seg->data, buffer, (uint)size);
                }
                seg->len = (uint)size;
                seg->frg = (kcp->stream == 0) ? (uint)(count - i - 1) : 0;
                KcpQueue.Initialize(&seg->node);
                KcpQueue.AddTail(&seg->node, &kcp->snd_queue);
                kcp->nsnd_que++;
                if (buffer != null)
                {
                    buffer += size;
                }
                len -= size;
                sent += size;
            }

            return sent;
        }



        //---------------------------------------------------------------------
        // parse ack
        //---------------------------------------------------------------------
        private static void ikcp_update_ack(KcpContext* kcp, int rtt)
        {
            int rto = 0;
            if (kcp->rx_srtt == 0)
            {
                kcp->rx_srtt = rtt;
                kcp->rx_rttval = rtt / 2;
            }
            else
            {
                long delta = rtt - kcp->rx_srtt;
                if (delta < 0) delta = -delta;
                kcp->rx_rttval = unchecked((int)((3 * kcp->rx_rttval + delta) / 4));
                kcp->rx_srtt = (7 * kcp->rx_srtt + rtt) / 8;
                if (kcp->rx_srtt < 1) kcp->rx_srtt = 1;
            }
            rto = unchecked((int)(kcp->rx_srtt + _imax_(kcp->interval, unchecked((uint)(4 * kcp->rx_rttval)))));
            kcp->rx_rto = unchecked((int)_ibound_(unchecked((uint)kcp->rx_minrto), unchecked((uint)rto), IKCP_RTO_MAX));
        }

        private static void ikcp_shrink_buf(KcpContext* kcp)
        {
            KcpQueueHead* p = kcp->snd_buf.next;
            if (p != &kcp->snd_buf)
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                kcp->snd_una = seg->sn;
            }
            else
            {
                kcp->snd_una = kcp->snd_nxt;
            }
        }

        private static void ikcp_parse_ack(KcpContext* kcp, uint sn)
        {
            KcpQueueHead* p, next;

            if (_itimediff(sn, kcp->snd_una) < 0 || _itimediff(sn, kcp->snd_nxt) >= 0)
                return;

            for (p = kcp->snd_buf.next; p != &kcp->snd_buf; p = next)
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                next = p->next;
                if (sn == seg->sn)
                {
                    KcpQueue.Delete(p);
                    ikcp_segment_delete(kcp, seg);
                    kcp->nsnd_buf--;
                    break;
                }
                if (_itimediff(sn, seg->sn) < 0)
                {
                    break;
                }
            }
        }

        private static void ikcp_parse_una(KcpContext* kcp, uint una)
        {
            KcpQueueHead* p, next;
            for (p = kcp->snd_buf.next; p != &kcp->snd_buf; p = next)
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                next = p->next;
                if (_itimediff(una, seg->sn) > 0)
                {
                    KcpQueue.Delete(p);
                    ikcp_segment_delete(kcp, seg);
                    kcp->nsnd_buf--;
                }
                else
                {
                    break;
                }
            }
        }

        private static void ikcp_parse_fastack(KcpContext* kcp, uint sn, uint ts)
        {
            KcpQueueHead* p, next;

            if (_itimediff(sn, kcp->snd_una) < 0 || _itimediff(sn, kcp->snd_nxt) >= 0)
                return;

            for (p = kcp->snd_buf.next; p != &kcp->snd_buf; p = next)
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                next = p->next;
                if (_itimediff(sn, seg->sn) < 0)
                {
                    break;
                }
                else if (sn != seg->sn)
                {
# if !IKCP_FASTACK_CONSERVE
                    seg->fastack++;
#else
                    if (_itimediff(ts, seg->ts) >= 0)
                        seg->fastack++;
#endif
                }
            }
        }


        //---------------------------------------------------------------------
        // ack append
        //---------------------------------------------------------------------
        private static void ikcp_ack_push(KcpContext* kcp, uint sn, uint ts)
        {
            uint newsize = kcp->ackcount + 1;
            uint* ptr;

            if (newsize > kcp->ackblock)
            {
                uint* acklist;
                uint newblock;

                for (newblock = 8; newblock < newsize; newblock <<= 1) ;
                acklist = (uint*)ikcp_malloc(newblock * sizeof(uint) * 2);

                if (acklist == null)
                    throw new OutOfMemoryException();

                if (kcp->acklist != null)
                {
                    uint x;
                    for (x = 0; x < kcp->ackcount; x++)
                    {
                        acklist[x * 2 + 0] = kcp->acklist[x * 2 + 0];
                        acklist[x * 2 + 1] = kcp->acklist[x * 2 + 1];
                    }
                    ikcp_free(kcp->acklist);
                }

                kcp->acklist = acklist;
                kcp->ackblock = newblock;
            }

            ptr = &kcp->acklist[kcp->ackcount * 2];
            ptr[0] = sn;
            ptr[1] = ts;
            kcp->ackcount++;
        }

        private static void ikcp_ack_get(KcpContext* kcp, int p, uint* sn, uint* ts)
        {
            if (sn != null)
                sn[0] = kcp->acklist[p * 2 + 0];
            if (ts != null)
                ts[0] = kcp->acklist[p * 2 + 1];
        }



        //---------------------------------------------------------------------
        // parse data
        //---------------------------------------------------------------------
        private static void ikcp_parse_data(KcpContext* kcp, KcpSegment* newseg)
        {
            KcpQueueHead* p, prev;
            uint sn = newseg->sn;
            int repeat = 0;

            if (_itimediff(sn, kcp->rcv_nxt + kcp->rcv_wnd) >= 0 ||
                _itimediff(sn, kcp->rcv_nxt) < 0)
            {
                ikcp_segment_delete(kcp, newseg);
                return;
            }

            for (p = kcp->rcv_buf.prev; p != &kcp->rcv_buf; p = prev)
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                prev = p->prev;
                if (seg->sn == sn)
                {
                    repeat = 1;
                    break;
                }
                if (_itimediff(sn, seg->sn) > 0)
                {
                    break;
                }
            }

            if (repeat == 0)
            {
                KcpQueue.Initialize(&newseg->node);
                KcpQueue.Add(&newseg->node, p);
                kcp->nrcv_buf++;
            }
            else
            {
                ikcp_segment_delete(kcp, newseg);
            }

#if false
            ikcp_qprint("rcvbuf", &kcp->rcv_buf);
            Console.Write($"rcv_nxt={kcp->rcv_nxt:D}\n");
#endif

            // move available data from rcv_buf -> rcv_queue
            while (!KcpQueue.IsEmpty(&kcp->rcv_buf))
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(kcp->rcv_buf.next, SegmentNodeOffset);
                if (seg->sn == kcp->rcv_nxt && kcp->nrcv_que < kcp->rcv_wnd)
                {
                    KcpQueue.Delete(&seg->node);
                    kcp->nrcv_buf--;
                    KcpQueue.AddTail(&seg->node, &kcp->rcv_queue);
                    kcp->nrcv_que++;
                    kcp->rcv_nxt++;
                }
                else
                {
                    break;
                }
            }

#if false
            ikcp_qprint("queue", &kcp->rcv_queue);
            Console.Write($"rcv_nxt={kcp->rcv_nxt:D}\n");
#endif

#if true
            //	Console.Write($"snd(buf={kcp->nsnd_buf:D}, queue={kcp->nsnd_que:D})\n");
            //	Console.Write($"rcv(buf={kcp->nrcv_buf:D}, queue={kcp->nrcv_que:D})\n");
#endif
        }


        //---------------------------------------------------------------------
        // input data
        //---------------------------------------------------------------------
        public static partial int ikcp_input(KcpContext* kcp, byte* data, nint size)
        {
            uint prev_una = kcp->snd_una;
            uint maxack = 0, latest_ts = 0;
            int flag = 0;

            if (ikcp_canlog(kcp, KcpLogFlags.Input, out delegate* unmanaged[Cdecl]<byte*, KcpContext*, void*, void> loggerFunc))
                ikcp_log_internal(kcp, loggerFunc, $"[RI] {size:D} bytes");

            if (data == null || (int)size < IKCP_OVERHEAD) return -1;

            while (true)
            {
                uint ts, sn, len, una, conv;
                ushort wnd;
                byte cmd, frg;
                KcpSegment* seg;

                if (size < IKCP_OVERHEAD) break;

                data = ikcp_decode32u(data, &conv);
                if (conv != kcp->conv) return -1;

                data = ikcp_decode8u(data, &cmd);
                data = ikcp_decode8u(data, &frg);
                data = ikcp_decode16u(data, &wnd);
                data = ikcp_decode32u(data, &ts);
                data = ikcp_decode32u(data, &sn);
                data = ikcp_decode32u(data, &una);
                data = ikcp_decode32u(data, &len);

                size -= IKCP_OVERHEAD;

                if (size < len || (int)len < 0) return -2;

                if (cmd != IKCP_CMD_PUSH && cmd != IKCP_CMD_ACK &&

                    cmd != IKCP_CMD_WASK && cmd != IKCP_CMD_WINS)
                    return -3;

                kcp->rmt_wnd = wnd;
                ikcp_parse_una(kcp, una);
                ikcp_shrink_buf(kcp);

                if (cmd == IKCP_CMD_ACK)
                {
                    if (_itimediff(kcp->current, ts) >= 0)
                    {
                        ikcp_update_ack(kcp, _itimediff(kcp->current, ts));
                    }
                    ikcp_parse_ack(kcp, sn);
                    ikcp_shrink_buf(kcp);
                    if (flag == 0)
                    {
                        flag = 1;
                        maxack = sn;
                        latest_ts = ts;
                    }
                    else
                    {
                        if (_itimediff(sn, maxack) > 0)
                        {
# if !IKCP_FASTACK_CONSERVE
                            maxack = sn;
                            latest_ts = ts;
#else
                            if (_itimediff(ts, latest_ts) > 0)
                            {
                                maxack = sn;
                                latest_ts = ts;
                            }
#endif
                        }
                    }
                    if (ikcp_canlog(kcp, KcpLogFlags.IncomingAck, out loggerFunc))
                    {
                        ikcp_log_internal(kcp, loggerFunc, "input ack: " +
                            $"sn={sn:D} " +
                            $"rtt={(long)_itimediff(kcp->current, ts):D} " +
                            $"rto={(long)kcp->rx_rto:D}");
                    }
                }
                else if (cmd == IKCP_CMD_PUSH)
                {
                    if (ikcp_canlog(kcp, KcpLogFlags.IncomingData, out loggerFunc))
                    {
                        ikcp_log_internal(kcp, loggerFunc, "input psh: " +
                            $"sn={sn:D} " +
                            $"ts={ts:D}");
                    }
                    if (_itimediff(sn, kcp->rcv_nxt + kcp->rcv_wnd) < 0)
                    {
                        ikcp_ack_push(kcp, sn, ts);
                        if (_itimediff(sn, kcp->rcv_nxt) >= 0)
                        {
                            seg = ikcp_segment_new(kcp, len);
                            seg->conv = conv;
                            seg->cmd = cmd;
                            seg->frg = frg;
                            seg->wnd = wnd;
                            seg->ts = ts;
                            seg->sn = sn;
                            seg->una = una;
                            seg->len = len;

                            if (len > 0)
                            {
                                memcpy(seg->data, data, len);
                            }

                            ikcp_parse_data(kcp, seg);
                        }
                    }
                }
                else if (cmd == IKCP_CMD_WASK)
                {
                    // ready to send back IKCP_CMD_WINS in ikcp_flush
                    // tell remote my window size
                    kcp->probe |= IKCP_ASK_TELL;
                    if (ikcp_canlog(kcp, KcpLogFlags.IncomingProbe, out loggerFunc))
                    {
                        ikcp_log_internal(kcp, loggerFunc, "input probe");
                    }
                }
                else if (cmd == IKCP_CMD_WINS)
                {
                    // do nothing
                    if (ikcp_canlog(kcp, KcpLogFlags.IncomingWindows, out loggerFunc))
                    {
                        ikcp_log_internal(kcp, loggerFunc, $"input wins: {wnd:D}");
                    }
                }
                else
                {
                    return -3;
                }

                data += len;
                size = (nint)(size - len);
            }

            if (flag != 0)
            {
                ikcp_parse_fastack(kcp, maxack, latest_ts);
            }

            if (_itimediff(kcp->snd_una, prev_una) > 0)
            {
                if (kcp->cwnd < kcp->rmt_wnd)
                {
                    uint mss = kcp->mss;
                    if (kcp->cwnd < kcp->ssthresh)
                    {
                        kcp->cwnd++;
                        kcp->incr += mss;
                    }
                    else
                    {
                        if (kcp->incr < mss) kcp->incr = mss;
                        kcp->incr += (mss * mss) / kcp->incr + (mss / 16);
                        if ((kcp->cwnd + 1) * mss <= kcp->incr)
                        {
#if true
                            kcp->cwnd = (kcp->incr + mss - 1) / ((mss > 0) ? mss : 1);
#else
                            kcp->cwnd++;
#endif
                        }
                    }
                    if (kcp->cwnd > kcp->rmt_wnd)
                    {
                        kcp->cwnd = kcp->rmt_wnd;
                        kcp->incr = kcp->rmt_wnd * mss;
                    }
                }
            }

            return 0;
        }


        //---------------------------------------------------------------------
        // ikcp_encode_seg
        //---------------------------------------------------------------------
        private static byte* ikcp_encode_seg(byte* ptr, KcpSegment* seg)
        {
            ptr = ikcp_encode32u(ptr, seg->conv);
            ptr = ikcp_encode8u(ptr, (byte)seg->cmd);
            ptr = ikcp_encode8u(ptr, (byte)seg->frg);
            ptr = ikcp_encode16u(ptr, (ushort)seg->wnd);
            ptr = ikcp_encode32u(ptr, seg->ts);
            ptr = ikcp_encode32u(ptr, seg->sn);
            ptr = ikcp_encode32u(ptr, seg->una);
            ptr = ikcp_encode32u(ptr, seg->len);
            return ptr;
        }

        private static int ikcp_wnd_unused(KcpContext* kcp)
        {
            if (kcp->nrcv_que < kcp->rcv_wnd)
            {
                return unchecked((int)(kcp->rcv_wnd - kcp->nrcv_que));
            }
            return 0;
        }


        //---------------------------------------------------------------------
        // ikcp_flush
        //---------------------------------------------------------------------
        public static partial void ikcp_flush(KcpContext* kcp)
        {
            uint current = kcp->current;
            byte* buffer = kcp->buffer;
            byte* ptr = buffer;
            int count, size, i;
            uint resent, cwnd;
            uint rtomin;

            KcpQueueHead* p;
            int change = 0;
            bool lost = false;
            KcpSegment seg;

            // 'ikcp_update' haven't been called. 
            if (kcp->updated == 0) return;

            seg.conv = kcp->conv;
            seg.cmd = IKCP_CMD_ACK;
            seg.frg = 0;
            seg.wnd = unchecked((uint)ikcp_wnd_unused(kcp));
            seg.una = kcp->rcv_nxt;
            seg.len = 0;
            seg.sn = 0;
            seg.ts = 0;

            // flush acknowledges
            count = unchecked((int)kcp->ackcount);
            for (i = 0; i < count; i++)
            {
                size = (int)(ptr - buffer);
                if (size + IKCP_OVERHEAD > (int)kcp->mtu)
                {
                    ikcp_output(kcp, buffer, size);
                    ptr = buffer;
                }
                ikcp_ack_get(kcp, i, &seg.sn, &seg.ts);

                ptr = ikcp_encode_seg(ptr, &seg);
            }

            kcp->ackcount = 0;

            // probe window size (if remote window size equals zero)
            if (kcp->rmt_wnd == 0)
            {
                if (kcp->probe_wait == 0)
                {
                    kcp->probe_wait = IKCP_PROBE_INIT;
                    kcp->ts_probe = kcp->current + kcp->probe_wait;
                }
                else
                {
                    if (_itimediff(kcp->current, kcp->ts_probe) >= 0)
                    {
                        if (kcp->probe_wait < IKCP_PROBE_INIT)
                            kcp->probe_wait = IKCP_PROBE_INIT;
                        kcp->probe_wait += kcp->probe_wait / 2;
                        if (kcp->probe_wait > IKCP_PROBE_LIMIT)
                            kcp->probe_wait = IKCP_PROBE_LIMIT;
                        kcp->ts_probe = kcp->current + kcp->probe_wait;
                        kcp->probe |= IKCP_ASK_SEND;
                    }
                }
            }
            else
            {
                kcp->ts_probe = 0;
                kcp->probe_wait = 0;
            }

            // flush window probing commands
            if ((kcp->probe & IKCP_ASK_SEND) != 0)
            {
                seg.cmd = IKCP_CMD_WASK;
                size = (int)(ptr - buffer);
                if (size + IKCP_OVERHEAD > (int)kcp->mtu)
                {
                    ikcp_output(kcp, buffer, size);
                    ptr = buffer;
                }
                ptr = ikcp_encode_seg(ptr, &seg);
            }

            // flush window probing commands
            if ((kcp->probe & IKCP_ASK_TELL) != 0)
            {
                seg.cmd = IKCP_CMD_WINS;
                size = (int)(ptr - buffer);
                if (size + IKCP_OVERHEAD > (int)kcp->mtu)
                {
                    ikcp_output(kcp, buffer, size);
                    ptr = buffer;
                }
                ptr = ikcp_encode_seg(ptr, &seg);
            }

            kcp->probe = 0;

            // calculate window size
            cwnd = _imin_(kcp->snd_wnd, kcp->rmt_wnd);
            if (kcp->nocwnd == 0) 
                cwnd = _imin_(kcp->cwnd, cwnd);

            // move data from snd_queue to snd_buf
            while (_itimediff(kcp->snd_nxt, kcp->snd_una + cwnd) < 0)
            {
                KcpSegment* newseg;
                if (KcpQueue.IsEmpty(&kcp->snd_queue)) break;

                newseg = KcpQueue.GetEntry<KcpSegment>(kcp->snd_queue.next, SegmentNodeOffset);

                KcpQueue.Delete(&newseg->node);
                KcpQueue.AddTail(&newseg->node, &kcp->snd_buf);
                kcp->nsnd_que--;
                kcp->nsnd_buf++;

                newseg->conv = kcp->conv;
                newseg->cmd = IKCP_CMD_PUSH;
                newseg->wnd = seg.wnd;
                newseg->ts = current;
                newseg->sn = kcp->snd_nxt++;
                newseg->una = kcp->rcv_nxt;
                newseg->resendts = current;
                newseg->rto = unchecked((uint)kcp->rx_rto);
                newseg->fastack = 0;
                newseg->xmit = 0;
            }

            // calculate resent
            resent = (kcp->fastresend > 0) ? (uint)kcp->fastresend : 0xffffffff;
            rtomin = (kcp->nodelay == 0) ? (uint)(kcp->rx_rto >> 3) : 0;

            // flush data segments
            for (p = kcp->snd_buf.next; p != &kcp->snd_buf; p = p->next)
            {
                KcpSegment* segment = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                bool needsend = false;
                if (segment->xmit == 0)
                {
                    needsend = true;
                    segment->xmit++;
                    segment->rto = unchecked((uint)kcp->rx_rto);
                    segment->resendts = current + segment->rto + rtomin;
                }
                else if (_itimediff(current, segment->resendts) >= 0)
                {
                    needsend = true;
                    segment->xmit++;
                    kcp->xmit++;
                    if (kcp->nodelay == 0)
                    {
                        segment->rto += _imax_(segment->rto, (uint)kcp->rx_rto);
                    }
                    else
                    {
                        int step = (kcp->nodelay < 2) ?
                            ((int)(segment->rto)) : kcp->rx_rto;
                        segment->rto += (uint)(step / 2);
                    }
                    segment->resendts = current + segment->rto;
                    lost = true;
                }
                else if (segment->fastack >= resent)
                {
                    if ((int)segment->xmit <= kcp->fastlimit ||
                        kcp->fastlimit <= 0)
                    {
                        needsend = true;
                        segment->xmit++;
                        segment->fastack = 0;
                        segment->resendts = current + segment->rto;
                        change++;
                    }
                }

                if (needsend)
                {
                    int need;
                    segment->ts = current;
                    segment->wnd = seg.wnd;
                    segment->una = kcp->rcv_nxt;

                    size = (int)(ptr - buffer);
                    need = (int)(IKCP_OVERHEAD + segment->len);

                    if (size + need > (int)kcp->mtu)
                    {
                        ikcp_output(kcp, buffer, size);
                        ptr = buffer;
                    }

                    ptr = ikcp_encode_seg(ptr, segment);

                    if (segment->len > 0)
                    {
                        memcpy(ptr, segment->data, segment->len);
                        ptr += segment->len;
                    }

                    if (segment->xmit >= kcp->dead_link)
                    {
                        kcp->state = uint.MaxValue;
                    }
                }
            }

            // flash remain segments
            size = (int)(ptr - buffer);
            if (size > 0)
            {
                ikcp_output(kcp, buffer, size);
            }

            // update ssthresh
            if (change != 0)
            {
                uint inflight = kcp->snd_nxt - kcp->snd_una;
                kcp->ssthresh = inflight / 2;
                if (kcp->ssthresh < IKCP_THRESH_MIN)
                    kcp->ssthresh = IKCP_THRESH_MIN;
                kcp->cwnd = kcp->ssthresh + resent;
                kcp->incr = kcp->cwnd * kcp->mss;
            }

            if (lost)
            {
                kcp->ssthresh = cwnd / 2;
                if (kcp->ssthresh < IKCP_THRESH_MIN)
                    kcp->ssthresh = IKCP_THRESH_MIN;
                kcp->cwnd = 1;
                kcp->incr = kcp->mss;
            }

            if (kcp->cwnd < 1)
            {
                kcp->cwnd = 1;
                kcp->incr = kcp->mss;
            }
        }

        //---------------------------------------------------------------------
        // update state (call it repeatedly, every 10ms-100ms), or you can ask 
        // ikcp_check when to call it again (without ikcp_input/_send calling).
        // 'current' - current timestamp in millisec. 
        //---------------------------------------------------------------------
        public static partial void ikcp_update(KcpContext* kcp, uint current)
        {
            int slap;

            kcp->current = current;

            if (kcp->updated == 0)
            {
                kcp->updated = 1;
                kcp->ts_flush = kcp->current;
            }

            slap = _itimediff(kcp->current, kcp->ts_flush);

            if (slap >= 10000 || slap < -10000)
            {
                kcp->ts_flush = kcp->current;
                slap = 0;
            }

            if (slap >= 0)
            {
                kcp->ts_flush += kcp->interval;
                if (_itimediff(kcp->current, kcp->ts_flush) >= 0)
                {
                    kcp->ts_flush = kcp->current + kcp->interval;
                }
                ikcp_flush(kcp);
            }
        }


        //---------------------------------------------------------------------
        // Determine when should you invoke ikcp_update:
        // returns when you should invoke ikcp_update in millisec, if there 
        // is no ikcp_input/_send calling. you can call ikcp_update in that
        // time, instead of call update repeatly.
        // Important to reduce unnacessary ikcp_update invoking. use it to 
        // schedule ikcp_update (eg. implementing an epoll-like mechanism, 
        // or optimize ikcp_update when handling massive kcp connections)
        //---------------------------------------------------------------------
        public static partial uint ikcp_check(KcpContext* kcp, uint current)
        {
            uint ts_flush = kcp->ts_flush;
            int tm_flush = 0x7fffffff;
            int tm_packet = 0x7fffffff;
            uint minimal = 0;
            KcpQueueHead* p;

            if (kcp->updated == 0)
            {
                return current;
            }

            if (_itimediff(current, ts_flush) >= 10000 ||
                _itimediff(current, ts_flush) < -10000)
            {
                ts_flush = current;
            }

            if (_itimediff(current, ts_flush) >= 0)
            {
                return current;
            }

            tm_flush = _itimediff(ts_flush, current);

            for (p = kcp->snd_buf.next; p != &kcp->snd_buf; p = p->next)
            {
                KcpSegment* seg = KcpQueue.GetEntry<KcpSegment>(p, SegmentNodeOffset);
                int diff = _itimediff(seg->resendts, current);
                if (diff <= 0)
                {
                    return current;
                }
                if (diff < tm_packet) tm_packet = diff;
            }

            minimal = (uint)(tm_packet < tm_flush ? tm_packet : tm_flush);
            if (minimal >= kcp->interval) minimal = kcp->interval;

            return current + minimal;
        }

        public static partial int ikcp_setmtu(KcpContext* kcp, int mtu)
        {
            byte* buffer;
            if (mtu < 50 || mtu < IKCP_OVERHEAD)
                return -1;
            buffer = (byte*)ikcp_malloc(unchecked((nuint)((mtu + IKCP_OVERHEAD) * 3)));
            if (buffer == null)
                return -2;
            kcp->mtu = (uint)mtu;
            kcp->mss = kcp->mtu - IKCP_OVERHEAD;
            ikcp_free(kcp->buffer);
            kcp->buffer = buffer;
            return 0;
        }

        public static int ikcp_interval(KcpContext* kcp, int interval)
        {
            if (interval > 5000)
                interval = 5000;
            else if
                (interval < 10)
                interval = 10;
            kcp->interval = (uint)interval;
            return 0;
        }

        public static partial int ikcp_nodelay(KcpContext* kcp, int nodelay, int interval, int resend, int nc)
        {
            if (nodelay >= 0)
            {
                kcp->nodelay = (uint)nodelay;
                if (nodelay != 0)
                    kcp->rx_minrto = IKCP_RTO_NDL;
                else
                    kcp->rx_minrto = IKCP_RTO_MIN;
            }
            if (interval >= 0)
            {
                if (interval > 5000) interval = 5000;
                else if (interval < 10) interval = 10;
                kcp->interval = (uint)interval;
            }
            if (resend >= 0)
                kcp->fastresend = resend;
            if (nc >= 0)
                kcp->nocwnd = nc;
            return 0;
        }

        public static partial int ikcp_wndsize(KcpContext* kcp, int sndwnd, int rcvwnd)
        {
            if (kcp != null)
            {
                if (sndwnd > 0)
                {
                    kcp->snd_wnd = (uint)sndwnd;
                }
                if (rcvwnd > 0)
                {   // must >= max fragment size
                    kcp->rcv_wnd = _imax_((uint)rcvwnd, IKCP_WND_RCV);
                }
            }
            return 0;
        }

        public static partial int ikcp_waitsnd(KcpContext* kcp)
        {
            return (int)(kcp->nsnd_buf + kcp->nsnd_que);
        }

        // read conv
        public static partial uint ikcp_getconv(void* ptr)
        {
            uint conv;
            ikcp_decode32u((byte*)ptr, &conv);
            return conv;
        }
    }
}