using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace osafw.Tests;

[TestClass]
public class FwUpdatesTests
{
    [TestMethod]
    public void FileNameWithoutExtComparer_SortsByDatePart()
    {
        var comparerType = typeof(FwUpdates).GetNestedType("FileNameWithoutExtComparer", BindingFlags.NonPublic);
        Assert.IsNotNull(comparerType, "Comparer type not found");

        var comparer = (IComparer<string>?)Activator.CreateInstance(comparerType!);
        Assert.IsNotNull(comparer);

        var files = new List<string>
        {
            "update2025-03-03-001.sql",
            "update2025-02-20.sql",
            "update2025-03-03.sql",
            "update2025-02-30.sql"
        };

        files.Sort(comparer);

        CollectionAssert.AreEqual(new[]
        {
            "update2025-02-20.sql",
            "update2025-02-30.sql",
            "update2025-03-03.sql",
            "update2025-03-03-001.sql"
        }, files);
    }
}
