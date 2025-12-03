using System.Collections.Generic;
using System.Runtime.CompilerServices;

#pragma warning disable IDE1006

namespace KcpSharpN.Test
{
    /// <summary>
    /// 均匀分布的随机数
    /// </summary>
    internal sealed class Random
    {
        private readonly int[] _seeds;
        private int _size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int rand() => System.Random.Shared.Next(0, 0x7fff);

        public Random(int size)
        {
            _size = 0;
            _seeds = new int[size];
        }

        public int random()
        {
            int[] seeds = _seeds;
            int x, i, length = seeds.Length, size = _size;
            if (length == 0) return 0;
            if (size == 0)
            {
                for (i = 0; i < length; i++)
                {
                    seeds[i] = i;
                }
                size = length;
            }
            i = rand() % size;
            x = seeds[i];
            seeds[i] = seeds[--size];
            _size = size;
            return x;
        }
    }
}
