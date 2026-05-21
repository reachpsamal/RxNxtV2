using ClosedXML.Excel;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Exports;

public sealed class SaleSummaryExcelService
{
    public byte[] Generate(SaleSummaryViewModel model, SaleSummaryFilterViewModel filter)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sale Summary");

        ws.Cell(1, 1).Value = "Sale Summary Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 10).Merge();

        ws.Cell(2, 1).Value = $"From: {filter.From:yyyy-MM-dd}  To: {filter.To:yyyy-MM-dd}";
        ws.Cell(3, 1).Value = $"Store: {(string.IsNullOrWhiteSpace(filter.StoreId) ? "All" : filter.StoreId)}  Cashier: {(string.IsNullOrWhiteSpace(filter.CreatedBy) ? "All" : filter.CreatedBy)}  Payment: {(string.IsNullOrWhiteSpace(filter.PaymentMode) || filter.PaymentMode == "All" ? "All" : filter.PaymentMode)}  Bill Status: {filter.BillStatus}  Group: {filter.GroupBy}";

        ws.Cell(5, 1).Value = "Total Bills";
        ws.Cell(5, 2).Value = "Avg Bill";
        ws.Cell(5, 3).Value = "Cash";
        ws.Cell(5, 4).Value = "UPI";
        ws.Cell(5, 5).Value = "Card";
        ws.Cell(5, 6).Value = "Other";
        ws.Cell(5, 7).Value = "Return %";
        ws.Cell(5, 8).Value = "Gross Sales";
        ws.Cell(5, 9).Value = "Total Refunds";

        ws.Cell(6, 1).Value = model.TotalBills;
        ws.Cell(6, 2).Value = model.AvgBillValue;
        ws.Cell(6, 3).Value = model.CashAmount;
        ws.Cell(6, 4).Value = model.UpiAmount;
        ws.Cell(6, 5).Value = model.CardAmount;
        ws.Cell(6, 6).Value = model.OtherAmount;
        ws.Cell(6, 7).Value = model.ReturnPercentage;
        ws.Cell(6, 8).Value = model.TotalGrossSales;
        ws.Cell(6, 9).Value = model.TotalRefunds;

        ws.Range(5, 1, 5, 9).Style.Font.Bold = true;
        ws.Range(5, 1, 5, 9).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(6, 3, 6, 9).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(6, 7).Style.NumberFormat.Format = "0.00\"%\"";

        var headerRow = 8;
        var groupLabel = filter.GroupBy switch { "Month" => "Month", "User" => "User", "Payment" => "Mode", _ => "Date" };
        ws.Cell(headerRow, 1).Value = groupLabel;
        ws.Cell(headerRow, 2).Value = "Bills";
        ws.Cell(headerRow, 3).Value = "Gross";
        ws.Cell(headerRow, 4).Value = "Discount";
        ws.Cell(headerRow, 5).Value = "Tax";
        ws.Cell(headerRow, 6).Value = "Net";
        ws.Cell(headerRow, 7).Value = "Round Off";
        ws.Cell(headerRow, 8).Value = "Paid";
        ws.Cell(headerRow, 9).Value = "Refund";
        ws.Cell(headerRow, 10).Value = "Outstanding";

        var headerRange = ws.Range(headerRow, 1, headerRow, 10);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var dataRowStart = headerRow + 1;
        var r = dataRowStart;
        foreach (var row in model.Rows)
        {
            ws.Cell(r, 1).Value = row.GroupKey;
            ws.Cell(r, 2).Value = row.BillCount;
            ws.Cell(r, 3).Value = row.GrossAmount;
            ws.Cell(r, 4).Value = row.Discount;
            ws.Cell(r, 5).Value = row.TaxAmount;
            ws.Cell(r, 6).Value = row.NetAmount;
            ws.Cell(r, 7).Value = row.RoundOff;
            ws.Cell(r, 8).Value = row.PaidAmount;
            ws.Cell(r, 9).Value = row.RefundAmount;
            ws.Cell(r, 10).Value = row.Outstanding;
            r++;
        }

        var totalRow = r;
        ws.Cell(totalRow, 1).Value = "Total";
        ws.Cell(totalRow, 2).Value = model.Rows.Sum(x => x.BillCount);
        ws.Cell(totalRow, 3).Value = model.Rows.Sum(x => x.GrossAmount);
        ws.Cell(totalRow, 4).Value = model.Rows.Sum(x => x.Discount);
        ws.Cell(totalRow, 5).Value = model.Rows.Sum(x => x.TaxAmount);
        ws.Cell(totalRow, 6).Value = model.Rows.Sum(x => x.NetAmount);
        ws.Cell(totalRow, 7).Value = model.Rows.Sum(x => x.RoundOff);
        ws.Cell(totalRow, 8).Value = model.Rows.Sum(x => x.PaidAmount);
        ws.Cell(totalRow, 9).Value = model.Rows.Sum(x => x.RefundAmount);
        ws.Cell(totalRow, 10).Value = model.Rows.Sum(x => x.Outstanding);

        var totalRange = ws.Range(totalRow, 1, totalRow, 10);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;

        var currencyCols = new[] { 3, 4, 5, 6, 7, 8, 9, 10 };
        foreach (var c in currencyCols)
        {
            ws.Range(dataRowStart, c, totalRow, c).Style.NumberFormat.Format = "#,##0.00";
        }

        ws.Column(1).Width = 22;
        ws.Column(2).Width = 10;
        for (var i = 3; i <= 10; i++)
            ws.Column(i).Width = 14;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
