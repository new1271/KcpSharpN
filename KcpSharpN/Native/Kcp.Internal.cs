using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

using InlineIL;

using InlineMethod;

#pragma warning disable IDE0060
#pragma warning disable IDE1006

namespace KcpSharpN.Native
{
    unsafe partial class Kcp
    {
        [Inline(InlineBehavior.Remove)]
        private static void memcpy(void* destination, void* source, [InlineParameter] nuint length)
        {
            IL.Emit.Ldarg_0();
            IL.Emit.Ldarg_1();
            IL.Emit.Ldarg_2();
            IL.Emit.Cpblk();
        }

        [Inline(InlineBehavior.Remove)]
        private static nuint SizeOf<T>()
        {
            IL.Emit.Sizeof<T>();
            return IL.Return<nuint>();
        }

        [Inline(InlineBehavior.Remove)]
        private static T* NullPointer<T>() where T : unmanaged => (T*)null;
    }
}
