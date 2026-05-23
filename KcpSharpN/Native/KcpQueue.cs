using System.Runtime.CompilerServices;

using InlineMethod;

namespace KcpSharpN.Native;

/// <summary>
/// Provides the operations for <see cref="KcpQueueHead"/>.
/// </summary>
public unsafe static class KcpQueue
{
    //---------------------------------------------------------------------
    // queue init                                                         
    //---------------------------------------------------------------------
    /// <summary>
    /// Initialize the queue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(KcpQueueHead* ptr)
    {
        ptr->next = ptr;
        ptr->prev = ptr;
    }

    /// <summary>
    /// Get the entry of the queue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* GetEntry<T>(KcpQueueHead* ptr, nuint offset) where T : unmanaged
        => KcpQueueHead.ContainerOf<T>(ptr, offset);

    /// <summary>
    /// Get the entry of the queue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* GetEntry<T>(KcpQueueHead* ptr, OffsetCalculateFunc<T> offsetFunc) where T : unmanaged
        => KcpQueueHead.ContainerOf(ptr, offsetFunc);

    //---------------------------------------------------------------------
    // queue operation                     
    //---------------------------------------------------------------------

    /// <summary>
    /// Appends the <paramref name="node"/> before the <paramref name="head"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendBefore(KcpQueueHead* node, KcpQueueHead* head)
    {
        KcpQueueHead* headPrev = head->prev;
        node->prev = headPrev;
        node->next = head;
        head->prev = node;
        headPrev->next = node;
    }

    /// <summary>
    /// Appends the <paramref name="node"/> after the <paramref name="head"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApeendAfter(KcpQueueHead* node, KcpQueueHead* head)
    {
        KcpQueueHead* headNext = head->next;
        node->prev = head;
        node->next = headNext;
        head->next = node;
        headNext->prev = node;
    }

    /// <summary>
    /// Deletes the nodes betweens <paramref name="p"/> and <paramref name="n"/>.
    /// </summary>
    /// <param name="p"></param>
    /// <param name="n"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DeleteBetween(KcpQueueHead* p, KcpQueueHead* n)
    {
        n->prev = p;
        p->next = n;
    }

    /// <summary>
    /// Deletes the <paramref name="entry"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Delete(KcpQueueHead* entry)
    {
        entry->next->prev = entry->prev;
        entry->prev->next = entry->next;
        entry->next = null;
        entry->prev = null;
    }

    /// <summary>
    /// Do <see cref="Delete(KcpQueueHead*)"/> and <see cref="Initialize(KcpQueueHead*)"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DeleteAndInitialize(KcpQueueHead* entry)
    {
        Delete(entry);
        Initialize(entry);
    }

    /// <summary>
    /// Checks the node is empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty(KcpQueueHead* entry) => entry == entry->next;

    /// <summary>
    /// Splice the <paramref name="list"/> into the <paramref name="head"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Splice(KcpQueueHead* list, KcpQueueHead* head)
    {
        if (IsEmpty(list))
            return;
        SpliceCore(list, head);
    }

    /// <summary>
    /// Do <see cref="Splice(KcpQueueHead*, KcpQueueHead*)"/> and <see cref="Initialize(KcpQueueHead*)"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SpliceAndInitialize(KcpQueueHead* list, KcpQueueHead* head)
    {
        Splice(list, head);
        Initialize(list);
    }

    [Inline(InlineBehavior.Remove)]
    private static void SpliceCore(KcpQueueHead* list, KcpQueueHead* head)
    {
        KcpQueueHead* first = list->next, last = list->prev;
        KcpQueueHead* at = head->next;
        first->prev = head;
        head->next = first;
        last->next = at;
        at->prev = last;
    }
}
