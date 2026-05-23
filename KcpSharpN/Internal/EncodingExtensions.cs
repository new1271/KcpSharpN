#if !NET8_0_OR_GREATER
using System.Runtime.CompilerServices;

#pragma warning disable IDE0130

namespace System.Text;

internal static class EncodingExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool TryGetBytes(this Encoding _this, scoped in ReadOnlySpan<char> chars, scoped in Span<byte> bytes, out int bytesWritten)
    {
        fixed (char* pChars = chars)
        {
            int required = _this.GetByteCount(pChars, chars.Length);
            if (required <= bytes.Length)
            {
                fixed (byte* pBytes = bytes)
                    bytesWritten = _this.GetBytes(pChars, chars.Length, pBytes, bytes.Length);
                return true;
            }

            bytesWritten = 0;
            return false;
        }
    }
}
#endif