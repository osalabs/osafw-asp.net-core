using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests
{
    [TestClass()]
    public class ConvUtilsTests
    {
        private static FW CreateFw()
        {
            var context = new DefaultHttpContext
            {
                Session = new FakeSession(),
            };
            return new FW(context, new ConfigurationBuilder().Build());
        }

        [TestMethod]
        public async Task Html2Pdf_ThrowsWhenFilenameMissing()
        {
            var fw = CreateFw();

            try
            {
                await ConvUtils.html2pdf(fw, "<html></html>", string.Empty, new FwDict());
                Assert.Fail("Expected ApplicationException for empty filename");
            }
            catch (ApplicationException ex)
            {
                StringAssert.Contains(ex.Message, "Wrong filename");
            }
        }

        [TestMethod]
        public void Html2xls_RequiresConfiguredConverter()
        {
            var fw = CreateFw();
            var htmlFile = Path.GetTempFileName();
            var xlsFile = Path.GetTempFileName();

            try
            {
                ConvUtils.html2xls(fw, htmlFile, xlsFile);
                Assert.Fail("Expected ApplicationException when html_converter is missing");
            }
            catch (ApplicationException ex)
            {
                StringAssert.Contains(ex.Message, "html_converter");
            }
            finally
            {
                File.Delete(htmlFile);
                File.Delete(xlsFile);
            }
        }

        [TestMethod]
        public void ExportNativeExcel_WritesHeadersAndRows()
        {
            var fw = CreateFw();
            var headers = new List<string> { "First", "Second" };
            var fields = new List<string> { "first", "second" };
            var rows = new FwList
            {
                new FwDict { { "first", "alpha" }, { "second", "1" } },
                new FwDict { { "first", "beta" } },
            };
            var filePath = Path.Combine(Path.GetTempPath(), $"convutils-{Guid.NewGuid()}.xlsx");

            try
            {
                ConvUtils.exportNativeExcel(fw, headers, fields, rows, filePath);

                Assert.IsTrue(File.Exists(filePath), "Excel file should be created");

                using var doc = SpreadsheetDocument.Open(filePath, false);
                var sheetData = doc.WorkbookPart!.WorksheetParts.First().Worksheet.Elements<SheetData>().First();
                var worksheetRows = sheetData.Elements<Row>().ToList();

                var headerCells = worksheetRows[0].Elements<Cell>().Select(c => c.CellValue!.Text).ToList();
                CollectionAssert.AreEqual(headers, headerCells, "Headers must match supplied list");

                var firstDataRow = worksheetRows[1].Elements<Cell>().Select(c => c.CellValue!.Text).ToList();
                CollectionAssert.AreEqual(new List<string> { "alpha", "1" }, firstDataRow, "First row must keep field order");

                var secondDataRow = worksheetRows[2].Elements<Cell>()
                    .Select(c => c.CellValue?.Text ?? string.Empty)
                    .ToList();
                CollectionAssert.AreEqual(new List<string> { "beta", string.Empty }, secondDataRow, "Missing cells should be empty strings");
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public void ReplaceSpecials_ReplacesTrademarkAndRegisteredSymbols()
        {
            var method = typeof(ConvUtils).GetMethod("_replace_specials", BindingFlags.NonPublic | BindingFlags.Static)!;
            var input = $"Marks {(char)153} and {(char)174}";

            var result = (string)method.Invoke(null, new object[] { input })!;

            StringAssert.Contains(result, "<sup><small>TM</small></sup>");
            StringAssert.Contains(result, "<sup><small>R</small></sup>");
        }

        [TestMethod]
        public void XlsxGetMaxCharacterWidth_ComputesMaxAcrossValuesAndHeaders()
        {
            var method = typeof(ConvUtils).GetMethod("xlsxGetMaxCharacterWidth", BindingFlags.NonPublic | BindingFlags.Static)!;
            var rows = new FwList
            {
                new FwDict { { "Col1", "short" }, { "Col2", "longervalue" } },
                new FwDict { { "Col1", "veryverylong" }, { "Col3", "x" } },
            };
            var headers = new List<string> { "Col1", "Col2", "Col3" };

            var result = (Dictionary<string, int>)method.Invoke(null, new object[] { rows, headers })!;

            Assert.AreEqual(12, result["Col1"]);
            Assert.AreEqual(11, result["Col2"]);
            Assert.AreEqual(10, result["Col3"]);
        }

        private class FakeSession : ISession
        {
            private readonly Dictionary<string, byte[]> store = new();

            public IEnumerable<string> Keys => store.Keys;
            public string Id { get; } = Guid.NewGuid().ToString();
            public bool IsAvailable => true;

            public void Clear() => store.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => store.Remove(key);
            public void Set(string key, byte[] value) => store[key] = value;
            public bool TryGetValue(string key, out byte[] value) => store.TryGetValue(key, out value!);
        }
    }
}
