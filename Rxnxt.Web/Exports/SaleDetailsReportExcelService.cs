using ClosedXML.Excel;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Exports;

public sealed class SaleDetailsReportExcelService
{
    public byte[] Generate(SaleDetailsReportViewModel model, SaleDetailsReportFilterViewModel filter)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sale Details");

        ws.Cell(1, 1).Value = "Sale Details Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 17).Merge();

        ws.Cell(2, 1).Value = $"From: {filter.From:yyyy-MM-dd}  To: {filter.To:yyyy-MM-dd}";
        ws.Cell(3, 1).Value = $"Invoice: {filter.InvoiceNo ?? "All"}  Customer: {filter.CustomerName ?? "All"}  Item: {filter.ItemName ?? "All"}  User: {filter.CreatedBy ?? "All"}  Payment: {filter.PaymentMode ?? "All"}  Status: {filter.BillStatus}";

        var headerRow = 5;
        var headers = new[] { "Invoice No", "Invoice Date", "Customer", "Mobile", "Item", "Batch", "Expiry", "Qty", "Free Qty", "MRP", "Rate", "Discount", "GST %", "Tax", "Net Amount", "Payment Mode", "Created By" };
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(headerRow, i + 1).Value = headers[i];
        }

        var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var dataRowStart = headerRow + 1;
        var r = dataRowStart;
        foreach (var row in model.Rows)
        {
            ws.Cell(r, 1).Value = row.InvoiceNo;
            ws.Cell(r, 2).Value = row.InvoiceDate.ToString("yyyy-MM-dd");
            ws.Cell(r, 3).Value = row.CustomerName;
            ws.Cell(r, 4).Value = row.Mobile;
            ws.Cell(r, 5).Value = row.ItemName;
            ws.Cell(r, 6).Value = row.Batch;
            ws.Cell(r, 7).Value = row.Expiry;
            ws.Cell(r, 8).Value = row.Qty;
            ws.Cell(r, 9).Value = row.FreeQty;
            ws.Cell(r, 10).Value = row.Mrp;
            ws.Cell(r, 11).Value = row.Rate;
            ws.Cell(r, 12).Value = row.Discount;
            ws.Cell(r, 13).Value = row.GstPercent;
            ws.Cell(r, 14).Value = row.TaxAmount;
            ws.Cell(r, 15).Value = row.NetAmount;
            ws.Cell(r, 16).Value = row.PaymentMode;
            ws.Cell(r, 17).Value = row.CreatedBy;

            if (row.IsCancelled)
            {
                var rowRange = ws.Range(r, 1, r, headers.Length);
                rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xFEF2F2);
                rowRange.Style.Font.FontColor = XLColor.FromArgb(0x991B1B);
            }
            r++;
        }

        var currencyCols = new[] { 10, 11, 12, 14, 15 };
        foreach (var c in currencyCols)
        {
            ws.Range(dataRowStart, c, r - 1, c).Style.NumberFormat.Format = "#,##0.00";
        }

        ws.Column(1).Width = 16;
        ws.Column(2).Width = 14;
        ws.Column(3).Width = 22;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 28;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 12;
        ws.Column(8).Width = 8;
        ws.Column(9).Width = 10;
        ws.Column(10).Width = 10;
        ws.Column(11).Width = 10;
        ws.Column(12).Width = 10;
        ws.Column(13).Width = 8;
        ws.Column(14).Width = 12;
        ws.Column(15).Width = 12;
        ws.Column(16).Width = 14;
        ws.Column(17).Width = 16;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
