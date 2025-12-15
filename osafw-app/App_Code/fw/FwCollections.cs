// FwRow and FwList classes - dictionary and list wrappers with legacy interop
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class FwDict : Dictionary<string, object?>
{
    public FwDict() : base(StringComparer.Ordinal) { }
    public FwDict(int capacity) : base(capacity, StringComparer.Ordinal) { }
    public FwDict(IDictionary? src) : base(src?.Count ?? 0, StringComparer.Ordinal)
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
    public FwList(IEnumerable collection)
    {
        if (collection != null)
            foreach (var item in collection)
                if (item is IDictionary)
                    Add((FwDict)item);

    }
}
