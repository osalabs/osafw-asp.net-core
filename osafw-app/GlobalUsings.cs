// Common Global usings for the project
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

global using ObjDict = System.Collections.Generic.Dictionary<string, object?>;
global using StrDict = System.Collections.Generic.Dictionary<string, string>;
global using ObjList = System.Collections.Generic.List<object?>;
global using StrList = System.Collections.Generic.List<string>;

// from fw/FwCollections.cs
global using FwRow = osafw.FwRow;
global using FwList = osafw.FwList; // List<FwRow>

global using IFwRow = System.Collections.Generic.IDictionary<string, object?>; // FwRow compatible
global using IFwList = System.Collections.Generic.IList<System.Collections.Generic.IDictionary<string, object?>>; // IList<IFwRow>
global using IFwEnumerable = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object?>>; // IEnumerable<IFwRow> for APIs that do not change the collection
