using System.Runtime.CompilerServices;

using InlineMethod;

namespace KcpSharpN.Native
{
    public unsafe static class KcpQueue
    {
        //---------------------------------------------------------------------
        // queue init                                                         
        //---------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Initialize(KcpQueueHead* ptr)
        {
            ptr->next = ptr;
            ptr->prev = ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* GetEntry<T>(KcpQueueHead* ptr, nuint offset) where T : unmanaged
            => KcpQueueHead.ContainerOf<T>(ptr, offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* GetEntry<T>(KcpQueueHead* ptr, OffsetCalculateFunc<T> offsetFunc) where T : unmanaged
            => KcpQueueHead.ContainerOf(ptr, offsetFunc);

        //---------------------------------------------------------------------
        // queue operation                     
        //---------------------------------------------------------------------
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(KcpQueueHead* node, KcpQueueHead* head)
        {
            KcpQueueHead* headNext = head->next;
            node->prev = head;
            node->next = headNext;
            head->next = node;
            headNext->prev = node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddTail(KcpQueueHead* node, KcpQueueHead* head)
        {
            KcpQueueHead* headPrev = head->prev;
            node->prev = headPrev;
            node->next = head;
            head->prev = node;
            headPrev->next = node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeleteBetween(KcpQueueHead* p, KcpQueueHead* n)
        {
            n->prev = p;
            p->next = n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Delete(KcpQueueHead* entry)
        {
            entry->next->prev = entry->prev;
            entry->prev->next = entry->next;
            entry->next = null;
            entry->prev = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeleteAndInitialize(KcpQueueHead* entry)
        {
            Delete(entry);
            Initialize(entry);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(KcpQueueHead* entry) => entry == entry->next;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Splice(KcpQueueHead* list, KcpQueueHead* head)
        {
            if (IsEmpty(list))
                return;
            SpliceCore(list, head);
        }

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
}
