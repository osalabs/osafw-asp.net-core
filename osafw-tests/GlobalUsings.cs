// Common global usings for the tests project
//
// Mirrors the aliases from osafw-app so shared helper types like StrList and IntList
// are available when running the unit tests.

global using StrDict = System.Collections.Generic.Dictionary<string, string>;
global using ObjList = System.Collections.Generic.List<object?>;
global using StrList = System.Collections.Generic.List<string>;
global using IntList = System.Collections.Generic.List<int>;

// from fw/FwCollections.cs
global using FwDict = osafw.FwDict;
global using FwList = osafw.FwList; // List<FwDict> with legacy dictionary interop

// for function parameters where more generic types are acceptable
global using FwDictLike = System.Collections.IDictionary; // FwDict compatible
global using FwListLike = System.Collections.IList;      // IList of dictionary-shaped rows
global using FwItemsLike = System.Collections.IEnumerable; // IEnumerable of dictionary-shaped rows for APIs that do not change the collection
