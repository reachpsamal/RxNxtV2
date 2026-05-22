using ClosedXML.Excel;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Exports;

public sealed class ItemWiseReportExcelService
{
    public byte[] Generate(ItemWiseReportViewModel model, ItemWiseFilterViewModel filter)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Item Wise Report");

        ws.Cell(1, 1).Value = "Item-wise Sale Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 14).Merge();

        ws.Cell(2, 1).Value = $"From: {filter.From:yyyy-MM-dd}  To: {filter.To:yyyy-MM-dd}";
        ws.Cell(3, 1).Value = $"Manufacturer: {filter.Manufacturer ?? "All"}  Batch: {filter.Batch ?? "All"}  Movement: {filter.MovementType ?? "All"}  Threshold: {filter.MovementThreshold}  Store: {filter.StoreId ?? "All"}  User: {filter.CreatedBy ?? "All"}  Status: {filter.BillStatus}";

        // KPIs
        ws.Cell(5, 1).Value = "Top 20 Items";
        ws.Cell(5, 2).Value = model.Top20.Select(r => r.ItemCode).Distinct().Count();
        ws.Cell(5, 3).Value = "Dead Stock";
        ws.Cell(5, 4).Value = model.DeadStockItems.Count;
        ws.Cell(5, 5).Value = "Near Expiry";
        ws.Cell(5, 6).Value = model.NearExpiryCount;
        ws.Cell(5, 7).Value = "ABC (A/B/C)";
        ws.Cell(5, 8).Value = $"{model.AbcA}/{model.AbcB}/{model.AbcC}";
        ws.Range(5, 1, 5, 8).Style.Font.Bold = true;

        var headerRow = 7;
        var headers = new[] { "Item Code", "Item Name", "Manufacturer", "Batch", "Expiry", "Qty Sold", "Free Qty", "Purchase Cost", "Sale Value", "Profit", "Margin %", "Current Stock", "Sale Price", "MRP" };
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
            ws.Cell(r, 1).Value = row.ItemCode;
            ws.Cell(r, 2).Value = row.ItemName;
            ws.Cell(r, 3).Value = row.Manufacturer;
            ws.Cell(r, 4).Value = row.Batch;
            ws.Cell(r, 5).Value = row.Expiry;
            ws.Cell(r, 6).Value = row.QtySold;
            ws.Cell(r, 7).Value = row.FreeQty;
            ws.Cell(r, 8).Value = row.PurchaseCost;
            ws.Cell(r, 9).Value = row.SaleValue;
            ws.Cell(r, 10).Value = row.Profit;
            ws.Cell(r, 11).Value = row.MarginPerc;
            ws.Cell(r, 12).Value = row.CurrentStock;
            ws.Cell(r, 13).Value = row.SalePrice;
            ws.Cell(r, 14).Value = row.MRP;
            r++;
        }

        // Totals row
        if (model.Rows.Count > 0)
        {
            var tr = r;
            ws.Cell(tr, 1).Value = "TOTAL";
            ws.Cell(tr, 1).Style.Font.Bold = true;
            ws.Cell(tr, 6).Value = model.Rows.Sum(x => x.QtySold);
            ws.Cell(tr, 7).Value = model.Rows.Sum(x => x.FreeQty);
            ws.Cell(tr, 8).Value = model.Rows.Sum(x => x.PurchaseCost);
            ws.Cell(tr, 9).Value = model.Rows.Sum(x => x.SaleValue);
            ws.Cell(tr, 10).Value = model.Rows.Sum(x => x.Profit);
            var totalSale = model.Rows.Sum(x => x.SaleValue);
            ws.Cell(tr, 11).Value = totalSale > 0 ? model.Rows.Sum(x => x.Profit) / totalSale * 100 : 0;

            var totalRange = ws.Range(tr, 1, tr, 14);
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }

        var currencyCols = new[] { 8, 9, 10, 11, 13, 14 };
        foreach (var c in currencyCols)
        {
            ws.Range(dataRowStart, c, (dataRowStart + model.Rows.Count - 1), c).Style.NumberFormat.Format = "#,##0.00";
        }
        var qtyCols = new[] { 6, 7, 12 };
        foreach (var c in qtyCols)
        {
            ws.Range(dataRowStart, c, (dataRowStart + model.Rows.Count - 1), c).Style.NumberFormat.Format = "#,##0.##";
        }

        ws.Column(1).Width = 14;
        ws.Column(2).Width = 30;
        ws.Column(3).Width = 22;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 10;
        ws.Column(8).Width = 14;
        ws.Column(9).Width = 14;
        ws.Column(10).Width = 14;
        ws.Column(11).Width = 10;
        ws.Column(12).Width = 10;
        ws.Column(13).Width = 10;
        ws.Column(14).Width = 10;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
