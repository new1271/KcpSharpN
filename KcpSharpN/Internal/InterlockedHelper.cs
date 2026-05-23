using System.Runtime.CompilerServices;
using System.Threading;

namespace KcpSharpN.Internal;

internal static class InterlockedHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Exchange(ref ulong location, ulong value)
        => unchecked((ulong)Interlocked.Exchange(ref UnsafeHelper.As<ulong, long>(ref location), (long)value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CompareExchange(ref ulong location, ulong value, ulong comparand)
        => unchecked((ulong)Interlocked.CompareExchange(ref UnsafeHelper.As<ulong, long>(ref location), (long)value, (long)comparand));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Increment(ref ulong location)
        => unchecked((ulong)Interlocked.Increment(ref UnsafeHelper.As<ulong, long>(ref location)));
}
