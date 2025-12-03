using System;
using System.Runtime.CompilerServices;

using KcpSharpN.Native;

#pragma warning disable IDE1006

namespace KcpSharpN.Test
{
    /// <summary>
    /// 带延迟的数据包
    /// </summary>
    internal sealed unsafe class DelayPacket : IDisposable
    {
        private readonly byte* _ptr;
        private readonly int _size;
        private uint _ts;
        private bool _disposed;

        public DelayPacket(int size, void* src = null)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));
            _ptr = (byte*)MemoryHelper.malloc((nuint)size * sizeof(byte));
            _size = size;
            if (src == null)
                Unsafe.InitBlock(_ptr, 0, (uint)size);
            else
                Unsafe.CopyBlock(_ptr, src, (uint)size);
        }

        public byte* ptr() => _ptr;

        public int size() => _size;
        public uint ts() => _ts;
        public void setts(uint ts) => _ts = ts;

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;

            MemoryHelper.free(_ptr);
        }

        ~DelayPacket() => Dispose(disposing: false);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
