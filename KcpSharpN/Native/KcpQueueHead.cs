using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using InlineMethod;

namespace KcpSharpN.Native
{
    public unsafe delegate nuint OffsetCalculateFunc<T>(T* nullptr) where T : unmanaged;

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct KcpQueueHead
    {
        public KcpQueueHead* prev, next;

        public KcpQueueHead(KcpQueueHead* prev, KcpQueueHead* next)
        {
            this.prev = prev;
            this.next = next;
        }

        [Inline(InlineBehavior.Keep, export: true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* ContainerOf<T>(KcpQueueHead* ptr, nuint offset) where T : unmanaged
            => (T*)(((byte*)(T*)ptr) - offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* ContainerOf<T>(KcpQueueHead* ptr, OffsetCalculateFunc<T> offsetFunc) where T : unmanaged => ContainerOf(ptr, offsetFunc);
    }
}
