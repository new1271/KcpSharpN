using System.Runtime.CompilerServices;

using InlineIL;

namespace KcpSharpN.Internal;

internal static class UnsafeHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TTo As<TFrom, TTo>(ref TFrom from)
    {
        IL.Emit.Ldarg_0();
        IL.Emit.Ret();
        throw IL.Unreachable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void* AsPointer<T>(ref T reference)
    {
        IL.Emit.Ldarg_0();
        IL.Emit.Ret();
        throw IL.Unreachable();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRefOut<T>(out T value)
    {
        IL.Emit.Ldarg_0();
        IL.Emit.Ret();
        throw IL.Unreachable();
    }
}
