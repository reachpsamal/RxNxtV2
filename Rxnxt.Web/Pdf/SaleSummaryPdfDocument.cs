using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Pdf;

public sealed class SaleSummaryPdfDocument : IDocument
{
    private readonly SaleSummaryViewModel _model;
    private readonly SaleSummaryFilterViewModel _filter;

    public SaleSummaryPdfDocument(SaleSummaryViewModel model, SaleSummaryFilterViewModel filter)
    {
        _model = model;
        _filter = filter;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

            page.Content().Column(col =>
            {
                col.Item().Text("Sale Summary Report").FontSize(18).Bold();

                col.Item().PaddingTop(4).Text(text =>
                {
                    text.Span($"From: {_filter.From:yyyy-MM-dd}  To: {_filter.To:yyyy-MM-dd}");
                });

                col.Item().Text(text =>
                {
                    text.Span($"Store: {_filter.StoreId ?? "All"}  Cashier: {_filter.CreatedBy ?? "All"}  Payment: {_filter.PaymentMode ?? "All"}  Status: {_filter.BillStatus}  Group: {_filter.GroupBy}");
                });

                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Black);

                col.Item().PaddingTop(6).Row(r =>
                {
                    KpiCell(r.RelativeItem(), "Total Bills", _model.TotalBills.ToString());
                    KpiCell(r.RelativeItem(), "Avg Bill", $"Rs {_model.AvgBillValue:0.00}");
                    KpiCell(r.RelativeItem(), "Cash", $"Rs {_model.CashAmount:0.00}");
                    KpiCell(r.RelativeItem(), "UPI", $"Rs {_model.UpiAmount:0.00}");
                    KpiCell(r.RelativeItem(), "Card", $"Rs {_model.CardAmount:0.00}");
                    KpiCell(r.RelativeItem(), "Return %", $"{_model.ReturnPercentage:0.00}%");
                });

                col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Black);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        var groupLabel = _filter.GroupBy switch { "Month" => "Month", "User" => "User", "Payment" => "Mode", _ => "Date" };
                        HeaderCell(header.Cell(), groupLabel);
                        HeaderCell(header.Cell().AlignRight(), "Bills");
                        HeaderCell(header.Cell().AlignRight(), "Gross");
                        HeaderCell(header.Cell().AlignRight(), "Discount");
                        HeaderCell(header.Cell().AlignRight(), "Tax");
                        HeaderCell(header.Cell().AlignRight(), "Net");
                        HeaderCell(header.Cell().AlignRight(), "Round Off");
                        HeaderCell(header.Cell().AlignRight(), "Paid");
                        HeaderCell(header.Cell().AlignRight(), "Refund");
                        HeaderCell(header.Cell().AlignRight(), "Outstanding");
                    });

                    foreach (var r in _model.Rows)
                    {
                        DataCell(table.Cell(), r.GroupKey);
                        DataCell(table.Cell().AlignRight(), r.BillCount.ToString());
                        DataCell(table.Cell().AlignRight(), r.GrossAmount.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.Discount.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.TaxAmount.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.NetAmount.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.RoundOff.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.PaidAmount.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.RefundAmount.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.Outstanding.ToString("0.00"));
                    }

                    TotalsCell(table.Cell(), "Total");
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.BillCount).ToString());
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.GrossAmount).ToString("0.00"));
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.Discount).ToString("0.00"));
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.TaxAmount).ToString("0.00"));
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.NetAmount).ToString("0.00"));
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.RoundOff).ToString("0.00"));
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.PaidAmount).ToString("0.00"));
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.RefundAmount).ToString("0.00"));
                    TotalsCell(table.Cell().AlignRight(), _model.Rows.Sum(x => x.Outstanding).ToString("0.00"));
                });
            });
        });
    }

    private static void KpiCell(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().Text(value).FontSize(12).Bold();
        });
    }

    private static void HeaderCell(IContainer container, string text)
    {
        container.DefaultTextStyle(x => x.SemiBold().FontSize(8))
            .BorderBottom(1).BorderColor(Colors.Black)
            .PaddingVertical(3).PaddingHorizontal(3)
            .Text(text);
    }

    private static void DataCell(IContainer container, string text)
    {
        container.DefaultTextStyle(x => x.FontSize(8))
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten4)
            .PaddingVertical(2).PaddingHorizontal(3)
            .Text(text);
    }

    private static void TotalsCell(IContainer container, string text)
    {
        container.DefaultTextStyle(x => x.SemiBold().FontSize(8))
            .BorderBottom(1).BorderColor(Colors.Black)
            .PaddingVertical(3).PaddingHorizontal(3)
            .Text(text);
    }
}
