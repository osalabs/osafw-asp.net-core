// FwRow and FwList classes - dictionary and list wrappers with legacy interop
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class FwDict : Dictionary<string, object?>
{
    public FwDict() : this((IEqualityComparer<string>?)null) { }
    public FwDict(IEqualityComparer<string>? comparer) : base(comparer ?? StringComparer.Ordinal) { }
    public FwDict(int capacity) : this(capacity, null) { }
    public FwDict(int capacity, IEqualityComparer<string>? comparer) : base(capacity, comparer ?? StringComparer.Ordinal) { }
    public FwDict(IDictionary? src) : this(src, null) { }
    public FwDict(IDictionary? src, IEqualityComparer<string>? comparer) : base(src?.Count ?? 0, comparer ?? StringComparer.Ordinal)
    {
        if (src != null)
            foreach (DictionaryEntry e in src)
                if (e.Key is string key)
                    this[key] = e.Value;
    }

    // Return null when key is missing without throwing exception
    public new object? this[string key]
    {
        get => TryGetValue(key, out var v) ? v : null;
        set => base[key] = value;
    }

    public static implicit operator Hashtable(FwDict d)
    {
        var h = new Hashtable(d.Count);
        foreach (var kv in d) h[kv.Key] = kv.Value;
        return h;
    }
    public static explicit operator FwDict(Hashtable h) => new(h);
}

public class FwList : List<FwDict>
{
    public FwList() { }
    public FwList(int capacity) : base(capacity) { }
    public FwList(ICollection c) : base((IEnumerable<FwDict>)c) { }
    public FwList(IList list)
    {
        if (list != null)
            foreach (var item in list)
                if (item is IDictionary)
                    Add((FwDict)item);
    }
    public FwList(IEnumerable collection)
    {
        if (collection != null)
            foreach (var item in collection)
                if (item is IDictionary)
                    Add((FwDict)item);

    }
}
