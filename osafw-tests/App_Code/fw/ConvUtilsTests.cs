using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace osafw.Tests
{
    [TestClass()]
    public class ConvUtilsTests
    {
        [TestMethod]
        public async Task Html2Pdf_ThrowsWhenFilenameMissing()
        {
            var fw = TestHelpers.CreateFw();

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
            var fw = TestHelpers.CreateFw();
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
            var fw = TestHelpers.CreateFw();
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
                var workbookPart = doc.WorkbookPart;
                Assert.IsNotNull(workbookPart);
                var worksheet = workbookPart!.WorksheetParts.First().Worksheet;
                Assert.IsNotNull(worksheet);
                var sheetData = worksheet!.Elements<SheetData>().First();
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

    }
}
