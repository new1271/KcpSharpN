using System.Collections.Generic;
using System.Runtime.CompilerServices;

using InlineMethod;

namespace KcpSharpN.Native
{
    public unsafe struct KcpQueueHead
    {
        public KcpQueueHead* prev, next;

        public KcpQueueHead(KcpQueueHead* prev, KcpQueueHead* next)
        {
            this.prev = prev;
            this.next = next;
        }

        //---------------------------------------------------------------------
        // queue init                                                         
        //---------------------------------------------------------------------
        [Inline(InlineBehavior.Keep, export: true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Initialize(KcpQueueHead* ptr)
        {
            ptr->next = ptr;
            ptr->prev = ptr;
        }

        //---------------------------------------------------------------------
        // queue operation                     
        //---------------------------------------------------------------------
        [Inline(InlineBehavior.Keep, export: true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(KcpQueueHead* node, KcpQueueHead* head)
        {
            KcpQueueHead* headNext = head->next;
            node->prev = head;
            node->next = headNext;
            head->next = node;
            headNext->prev = node;
        }

        [Inline(InlineBehavior.Keep, export: true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddTail(KcpQueueHead* node, KcpQueueHead* head)
        {
            KcpQueueHead* headPrev = head->prev;
            node->prev = headPrev;
            node->next = head;
            head->prev = node;
            headPrev->next = node;
        }

        [Inline(InlineBehavior.Keep, export: true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeleteBetween(KcpQueueHead* p, KcpQueueHead* n)
        {
            n->prev = p;
            p->next = n;
        }

        [Inline(InlineBehavior.Keep, export: true)]
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

        [Inline(InlineBehavior.Keep, export: true)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(KcpQueueHead* entry) => entry == entry->next;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Splice(KcpQueueHead* list, KcpQueueHead* head)
        {
            if (IsEmpty(list))
                return;
            SpliceCore(list, head);
        }

        [Inline(InlineBehavior.Keep, export: true)]
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
