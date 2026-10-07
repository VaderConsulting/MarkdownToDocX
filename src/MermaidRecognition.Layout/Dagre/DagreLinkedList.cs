// Port of graphlib data/list from dagre-d3-es 7.0.14.
// Original graphlib copyright: Copyright (c) 2012-2014 Chris Pettitt
// Copyright (c) 2022-2024 dagre-es contributors
// Permission is granted under the MIT licence that ships with dagre-d3-es.

using System.Collections.Generic;

namespace MermaidRecognition.Layout.Dagre;

/// <summary>
/// Doubly linked list used by the greedy feedback-arc heuristic. New entries join the front and leave from the back.
/// </summary>
internal sealed class DagreLinkedList
{
    private readonly DagreNodeLabel _Sentinel = new DagreNodeLabel();

    public DagreLinkedList()
    {
        _Sentinel.ListNext = _Sentinel;
        _Sentinel.ListPrev = _Sentinel;
    }

    public DagreNodeLabel? Dequeue()
    {
        DagreNodeLabel Entry = _Sentinel.ListPrev!;
        if (ReferenceEquals(Entry, _Sentinel))
        {
            return null;
        }

        Unlink(Entry);
        return Entry;
    }

    public void Enqueue(DagreNodeLabel Entry)
    {
        if (Entry.ListPrev != null && Entry.ListNext != null)
        {
            Unlink(Entry);
        }

        Entry.ListNext = _Sentinel.ListNext;
        _Sentinel.ListNext!.ListPrev = Entry;
        _Sentinel.ListNext = Entry;
        Entry.ListPrev = _Sentinel;
    }

    private static void Unlink(DagreNodeLabel Entry)
    {
        Entry.ListPrev!.ListNext = Entry.ListNext;
        Entry.ListNext!.ListPrev = Entry.ListPrev;
        Entry.ListNext = null;
        Entry.ListPrev = null;
    }
}
