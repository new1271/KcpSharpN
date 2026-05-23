using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;

#pragma warning disable IDE1006

namespace KcpSharpN.Native;

/// <summary>
/// The static class for allocate/deallocate native memory.
/// </summary>
public static unsafe class MemoryHelper
{
    private static readonly bool
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
        _isUnix = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                        RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"));

    /// <summary>
    /// Allocates native memory with specific size
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* malloc(nuint size)
    {
        if (_isWindows)
            return malloc_win32(size);
        else if (_isUnix)
            return malloc_unix(size);
        else
            return (void*)Marshal.AllocHGlobal(unchecked((int)size));
    }

    /// <summary>
    /// Deallocates native memory with specific size
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void free(void* ptr)
    {
        if (_isWindows)
            free_win32(ptr);
        else if (_isUnix)
            free_unix(ptr);
        else
            Marshal.FreeHGlobal((nint)ptr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* malloc_win32(nuint size) => SystemCallForWin32.malloc(size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void* malloc_unix(nuint size) => SystemCallForUnix.malloc(size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void free_win32(void* ptr) => SystemCallForWin32.free(ptr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void free_unix(void* ptr) => SystemCallForUnix.free(ptr);

    [SuppressUnmanagedCodeSecurity]
    private static class SystemCallForWin32
    {
        private const string LibraryName = "msvcrt";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(malloc))]
        public static extern void* malloc(nuint size);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(free))]
        public static extern void free(void* ptr);
    }

    [SuppressUnmanagedCodeSecurity]
    private static class SystemCallForUnix
    {
        private const string LibraryName = "libc";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(malloc))]
        public static extern void* malloc(nuint size);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(free))]
        public static extern void free(void* ptr);
    }
}
