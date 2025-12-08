// FwRow and FwList classes - dictionary and list wrappers with legacy interop
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class FwRow : Dictionary<string, object?>
{
    public FwRow() : base(StringComparer.Ordinal) { }
    public FwRow(int capacity) : base(capacity, StringComparer.Ordinal) { }
    public FwRow(IDictionary<string, object?> other) : base(other, StringComparer.Ordinal) { }

    // Return null when key is missing without throwing exception
    public new object? this[string key]
    {
        get => TryGetValue(key, out var v) ? v : null;
        set => base[key] = value;
    }

    // Interop with legacy APIs:
    public static implicit operator Hashtable(FwRow row)
    {
        var h = new Hashtable(row.Count);
        foreach (var kv in row) h[kv.Key] = kv.Value;
        return h;
    }
    public static explicit operator FwRow(Hashtable h)
    {
        var r = new FwRow(h?.Count ?? 0);
        if (h != null) foreach (DictionaryEntry e in h) r[(string)e.Key] = e.Value;
        return r;
    }
}

public class FwList : List<FwRow>
{
    public FwList() { }
    public FwList(int capacity) : base(capacity) { }
    public static implicit operator ArrayList(FwList rows)
    {
        var a = new ArrayList(rows.Count);
        foreach (var r in rows) a.Add((Hashtable)r);
        return a;
    }
}
