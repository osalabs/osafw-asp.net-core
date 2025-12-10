// FwDict and FwList classes - dictionary and list wrappers with legacy interop
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class FwDict : Dictionary<string, object?>
{
    public FwDict() : base(StringComparer.Ordinal) { }
    public FwDict(int capacity) : base(capacity, StringComparer.Ordinal) { }
    public FwDict(IDictionary<string, object?> other) : base(other, StringComparer.Ordinal) { }

    // Return null when key is missing without throwing exception
    public new object? this[string key]
    {
        get => TryGetValue(key, out var v) ? v : null;
        set => base[key] = value;
    }

    // Contains() method for convenience
    public bool Contains(string key) => base.ContainsKey(key);
    // Shallow Clone
    public FwDict Clone()
    {
        var r = new FwDict(this.Count);
        foreach (var kv in this)
            r[kv.Key] = kv.Value;
        return r;
    }

    public static FwDict From(IDictionary? source)
    {
        if (source == null)
            return new FwDict();

        if (source is FwDict existing)
            return existing;

        if (source is IDictionary<string, object?> genericDict)
            return new FwDict(genericDict);

        if (source is DBRow row)
            return (FwDict)row;

        if (source is Hashtable ht)
            return (FwDict)ht;

        var result = new FwDict(source.Count);
        foreach (DictionaryEntry entry in source)
        {
            if (entry.Key == null)
                continue;

            var key = entry.Key.ToString();
            if (string.IsNullOrEmpty(key))
                continue;

            result[key] = entry.Value;
        }

        return result;
    }

    // Interop with legacy APIs:
    public static implicit operator Hashtable(FwDict row)
    {
        var h = new Hashtable(row.Count);
        foreach (var kv in row) h[kv.Key] = kv.Value;
        return h;
    }
    public static explicit operator FwDict(Hashtable h)
    {
        var r = new FwDict(h?.Count ?? 0);
        if (h != null) foreach (DictionaryEntry e in h) r[(string)e.Key] = e.Value;
        return r;
    }

    public static explicit operator FwDict(DBRow h)
    {
        var r = new FwDict(h?.Count ?? 0);
        if (h != null)
        {
            foreach (var kv in h)
                r[kv.Key] = kv.Value;
        }
        return r;
    }
}

public class FwList : List<FwDict>
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
