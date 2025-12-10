// FwRow and FwList classes - dictionary and list wrappers with legacy interop
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class FwDict : Hashtable
{
    public FwDict() : base(StringComparer.Ordinal) { }
    public FwDict(int capacity) : base(capacity, StringComparer.Ordinal) { }
    public FwDict(IDictionary? other) : base(other?.Count ?? 0, StringComparer.Ordinal)
    {
        if (other != null)
            foreach (DictionaryEntry e in other)
                this[e.Key] = e.Value;
    }

    // Return null when key is missing without throwing exception
    public object? this[string key]
    {
        get => ContainsKey(key) ? base[key] : null;
        set => base[key] = value;
    }

}

public class FwList : ArrayList
{
    public FwList() { }
    public FwList(int capacity) : base(capacity) { }
    public FwList(ICollection c) : base(c) { }
    public FwList(IEnumerable collection)
    {
        if (collection != null)
            foreach (var item in collection)
                Add(item);
    }
}
