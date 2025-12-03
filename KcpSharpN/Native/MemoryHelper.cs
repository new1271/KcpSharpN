using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;

#pragma warning disable IDE1006

namespace KcpSharpN.Native
{
    public static unsafe class MemoryHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* malloc(nuint size)
        {
            if (OperatingSystem.IsWindows())
                return malloc_win32(size);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
                return malloc_unix(size);
            return (void*)Marshal.AllocHGlobal(unchecked((int)size));
        }

        [SupportedOSPlatform("windows")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void* malloc_win32(nuint size) => SystemCallForWin32.malloc(size);

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("freebsd")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void* malloc_unix(nuint size) => SystemCallForUnix.malloc(size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void free(void* ptr)
        {
            if (OperatingSystem.IsWindows())
            {
                free_win32(ptr);
                return;
            }
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            {
                free_unix(ptr);
                return;
            }
            Marshal.FreeHGlobal((nint)ptr);
        }

        [SupportedOSPlatform("windows")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void free_win32(void* ptr)
        {
            SystemCallForWin32.free(ptr);
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("freebsd")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void free_unix(void* ptr)
        {
            SystemCallForUnix.free(ptr);
        }

        [SupportedOSPlatform("windows")]
        [SuppressUnmanagedCodeSecurity]
        private static class SystemCallForWin32
        {
            private const string LibraryName = "msvcrt";

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(malloc))]
            public static extern void* malloc(nuint size);

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(free))]
            public static extern void free(void* ptr);
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("freebsd")]
        [SuppressUnmanagedCodeSecurity]
        private static class SystemCallForUnix
        {
            private const string LibraryName = "libc";

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(malloc))]
            public static extern void* malloc(nuint size);

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(free))]
            public static extern void free(void* ptr);
        }
    }
}
