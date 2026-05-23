#if NETSTANDARD2_0
#pragma warning disable IDE0130
// Original source code from https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/KeyValuePair.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace System.Collections.Generic;

/// <summary>
/// Provides the <see cref="Create{TKey, TValue}(TKey, TValue)"/> factory method for <see cref="KeyValuePair{TKey, TValue}"/>.
/// </summary>
internal static class KeyValuePair
{
    /// <summary>
    /// Creates a new <see cref="KeyValuePair{TKey, TValue}"/> from the given values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeyValuePair<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value) =>
        new KeyValuePair<TKey, TValue>(key, value);
}
#endif