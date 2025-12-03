using System;
using System.Runtime.CompilerServices;

using InlineMethod;

#pragma warning disable IDE1006

namespace KcpSharpN.Internal
{
    unsafe partial class Kcp
    {
        //---------------------------------------------------------------------
        // encode / decode
        //---------------------------------------------------------------------

        /* encode 8 bits unsigned int */
        [Inline(InlineBehavior.Remove)]
        private static byte* ikcp_encode8u(byte* p, byte c)
        {
            *p++ = c;
            return p;
        }

        /* decode 8 bits unsigned int */
        [Inline(InlineBehavior.Remove)]
        private static byte* ikcp_decode8u(byte* p, byte* c)
        {
            *c = *p++;
            return p;
        }

        /* encode 16 bits unsigned int (lsb) */
        [Inline(InlineBehavior.Remove)]
        private static byte* ikcp_encode16u(byte* p, ushort w)
        {
            if (BitConverter.IsLittleEndian)
                Unsafe.CopyBlockUnaligned(p, &w, sizeof(ushort));
            else
            {
                *(p + 0) = unchecked((byte)(w & 255));
                *(p + 1) = unchecked((byte)(w >> 8));
            }
            p += sizeof(ushort);
            return p;
        }

        /* decode 16 bits unsigned int (lsb) */
        [Inline(InlineBehavior.Remove)]
        private static byte* ikcp_decode16u(byte* p, ushort* w)
        {
            if (BitConverter.IsLittleEndian)
                Unsafe.CopyBlockUnaligned(w, p, sizeof(ushort));
            else
                *w = unchecked((ushort)(*(p + 0) + (*(p + 1) << 8)));
            p += sizeof(ushort);
            return p;
        }

        /* encode 32 bits unsigned int (lsb) */
        [Inline(InlineBehavior.Remove)]
        private static byte* ikcp_encode32u(byte* p, uint l)
        {
            if (BitConverter.IsLittleEndian)
                Unsafe.CopyBlockUnaligned(p, &l, sizeof(uint));
            else
            {
                *(p + 0) = (byte)((l >> 0) & 0xff);
                *(p + 1) = (byte)((l >> 8) & 0xff);
                *(p + 2) = (byte)((l >> 16) & 0xff);
                *(p + 3) = (byte)((l >> 24) & 0xff);
            }
            p += sizeof(int);
            return p;
        }

        /* decode 32 bits unsigned int (lsb) */
        [Inline(InlineBehavior.Remove)]
        private static byte* ikcp_decode32u(byte* p, uint* l)
        {
            if (BitConverter.IsLittleEndian)
                Unsafe.CopyBlockUnaligned(l, p, sizeof(uint));
            else
            {
                uint temp = *(p + 3);
                temp = *(p + 2) + (temp << 8);
                temp = *(p + 1) + (temp << 8);
                *l = *(p + 0) + (temp << 8);
            }
            p += sizeof(int);
            return p;
        }

        [Inline(InlineBehavior.Remove)]
        private static uint _imin_(uint a, uint b) => a <= b ? a : b;

        [Inline(InlineBehavior.Remove)]
        private static uint _imax_(uint a, uint b) => a >= b ? a : b;

        [Inline(InlineBehavior.Remove)]
        private static uint _ibound_(uint lower, uint middle, uint upper) => _imin_(_imax_(lower, middle), upper);

        [Inline(InlineBehavior.Remove)]
        private static long _itimediff(uint later, uint earlier) => later - earlier;
    }
}
