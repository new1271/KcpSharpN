using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;

#pragma warning disable IDE1006

namespace KcpSharpN.Test
{
    internal unsafe class TimeHelper
    {
        private static nint _mode = 0, _addsec = 0;
        private static ulong _freq = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void itimeofday(nint* sec, nint* usec)
        {
            if (OperatingSystem.IsWindows())
                itimeofday_win32(sec, usec);
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
                itimeofday_unix(sec, usec);
            else
                throw new PlatformNotSupportedException();
        }

        [SupportedOSPlatform("windows")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void itimeofday_win32(nint* sec, nint* usec)
        {
            ulong qpc;
            if (_mode == 0)
            {
                ulong freq;
                if (!SystemCallForWin32.QueryPerformanceFrequency(&freq))
                    freq = 0;
                freq = (freq == 0) ? 1 : freq;
                _freq = freq;
                SystemCallForWin32.QueryPerformanceCounter(&qpc);
                _addsec = SystemCallForWin32.time(null);
                _addsec -= (nint)((qpc / freq) & 0x7fffffff);
                _mode = 1;
            }
            SystemCallForWin32.QueryPerformanceCounter(&qpc);
            if (sec != null)
                *sec = (nint)(qpc / _freq) + _addsec;
            if (usec != null)
                *usec = (nint)((qpc % _freq) * 1000000 / _freq);
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("freebsd")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void itimeofday_unix(nint* sec, nint* usec)
        {
            TimeVal time;
            SystemCallForUnix.gettimeofday(&time, null);
            if (sec != null)
                *sec = time.tv_sec;
            if (usec != null)
                *usec = time.tv_usec;
        }

        /* get clock in millisecond 64 */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long iclock64()
        {
            nint s, u;
            nint value;
            itimeofday(&s, &u);
            value = s * 1000 + (u / 1000);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint iclock()
        {
            return (uint)(iclock64() & (long)0xfffffffful);
        }

        /* sleep in millisecond */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void isleep(nuint millisecond)
        {
            if (OperatingSystem.IsWindows())
            {
                isleep_win32(millisecond);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            {
                isleep_unix((millisecond << 10) - (millisecond << 4) - (millisecond << 3));
            }
            else
                throw new PlatformNotSupportedException();
        }

        [SupportedOSPlatform("windows")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void isleep_win32(nuint millisecond)
        {
            SystemCallForWin32.Sleep((uint)millisecond);
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("freebsd")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void isleep_unix(nuint millisecond)
        {
            SystemCallForUnix.usleep((millisecond << 10) - (millisecond << 4) - (millisecond << 3));
        }

        [SupportedOSPlatform("windows")]
        [SuppressUnmanagedCodeSecurity]
        private static class SystemCallForWin32
        {
            public const string LibraryName = "Kernel32";
            public const string LibraryName2 = "msvcrt";

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, EntryPoint = nameof(QueryPerformanceFrequency))]
            public static extern bool QueryPerformanceFrequency(ulong* lpFrequency);

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, EntryPoint = nameof(QueryPerformanceCounter))]
            public static extern bool QueryPerformanceCounter(ulong* lpPerformanceCount);

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.StdCall, EntryPoint = nameof(Sleep))]
            public static extern void Sleep(uint dwMilliseconds);

            [SuppressGCTransition]
            [DllImport(LibraryName2, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(time))]
            public static extern nint time(nint* timer);
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("freebsd")]
        [SuppressUnmanagedCodeSecurity]
        private static class SystemCallForUnix
        {
            public const string LibraryName = "libc";

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(gettimeofday))]
            public static extern int gettimeofday(TimeVal* tv, void* tz);

            [SuppressGCTransition]
            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(usleep))]
            public static extern int usleep(nuint usec);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private readonly struct TimeVal
        {
            public readonly nint tv_sec;
            public readonly nint tv_usec;
        }
    }
}
