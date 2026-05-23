#if !NET5_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;

namespace KcpSharpN.Internal;

internal static class ArraySegmentExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArraySegment<T> Slice<T>(this ArraySegment<T> _this, int index)
    {
        if (index >= _this.Count)
            return ThrowIndexOutOfRangeException();
        return new ArraySegment<T>(_this.Array!, _this.Offset + index, _this.Count);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static ArraySegment<T> ThrowIndexOutOfRangeException() => throw new ArgumentOutOfRangeException(nameof(index));
    }
}
#endif