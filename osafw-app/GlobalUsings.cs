// Common Global usings for the project
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

// aliases for commonly used types
global using ObjDict = System.Collections.Generic.Dictionary<string, object?>;
global using StrDict = System.Collections.Generic.Dictionary<string, string>;
global using ObjList = System.Collections.Generic.List<object?>;
global using StrList = System.Collections.Generic.List<string>;
global using IntList = System.Collections.Generic.List<int>;

// from fw/FwCollections.cs
global using FwDict = osafw.FwDict;
global using FwList = osafw.FwList; // List<object?> with FwDict-friendly interop

// for function parameters where more generic types are acceptable
global using FwDictLike = System.Collections.IDictionary; // FwDict compatible
global using FwListLike = System.Collections.IList;      // IList<IFwDict>
global using FwItemsLike = System.Collections.IEnumerable; // IEnumerable<IFwDict> for APIs that do not change the collection
