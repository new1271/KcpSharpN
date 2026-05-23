#if NETSTANDARD2_0
#pragma warning disable IDE0130
// Original source code from https://github.com/dotnet/dotnet/blob/main/src/runtime/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SuppressGCTransitionAttribute.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.InteropServices;

/// <summary>
/// An attribute used to indicate a GC transition should be skipped when making an unmanaged function call.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class SuppressGCTransitionAttribute : Attribute
{
    public SuppressGCTransitionAttribute()
    {
    }
}
#endif