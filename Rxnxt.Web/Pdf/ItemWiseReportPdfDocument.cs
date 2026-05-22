using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Pdf;

public sealed class ItemWiseReportPdfDocument : IDocument
{
    private readonly ItemWiseReportViewModel _model;

    public ItemWiseReportPdfDocument(ItemWiseReportViewModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(16);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(7));

            page.Content().Column(col =>
            {
                col.Item().Text("Item-wise Sale Report").FontSize(16).Bold();

                col.Item().PaddingTop(2).Text(text =>
                {
                    text.Span($"From: {_model.Filter.From:yyyy-MM-dd}  To: {_model.Filter.To:yyyy-MM-dd}  ");
                    text.Span($"Manufacturer: {_model.Filter.Manufacturer ?? "All"}  Batch: {_model.Filter.Batch ?? "All"}  Movement: {_model.Filter.MovementType ?? "All"}");
                });

                col.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);

                // KPI row
                col.Item().PaddingTop(4).Row(row =>
                {
                    KpiCell(row.RelativeItem(), "Top 20", _model.Top20.Select(r => r.ItemCode).Distinct().Count().ToString());
                    KpiCell(row.RelativeItem(), "Dead Stock", _model.DeadStockItems.Count.ToString());
                    KpiCell(row.RelativeItem(), "Near Expiry", _model.NearExpiryCount.ToString());
                    KpiCell(row.RelativeItem(), $"ABC (A/B/C)", $"{_model.AbcA}/{_model.AbcB}/{_model.AbcC}");
                });

                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Black);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(4);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
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
                        var labels = new[] { "Code", "Item Name", "Manufacturer", "Batch", "Expiry", "Qty", "Free", "Cost", "Sale Value", "Profit", "Margin%", "Stock", "Rate", "MRP" };
                        foreach (var label in labels)
                        {
                            HeaderCell(header.Cell(), label);
                        }
                    });

                    foreach (var r in _model.Rows)
                    {
                        DataCell(table.Cell(), r.ItemCode);
                        DataCell(table.Cell(), r.ItemName);
                        DataCell(table.Cell(), r.Manufacturer);
                        DataCell(table.Cell(), r.Batch);
                        DataCell(table.Cell(), r.Expiry);
                        DataCell(table.Cell().AlignRight(), r.QtySold.ToString("0.##"));
                        DataCell(table.Cell().AlignRight(), r.FreeQty.ToString("0.##"));
                        DataCell(table.Cell().AlignRight(), r.PurchaseCost.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.SaleValue.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.Profit.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.MarginPerc.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.CurrentStock.ToString("0.##"));
                        DataCell(table.Cell().AlignRight(), r.SalePrice.ToString("0.00"));
                        DataCell(table.Cell().AlignRight(), r.MRP.ToString("0.00"));
                    }

                    // Totals
                    if (_model.Rows.Count > 0)
                    {
                        var tQty = _model.Rows.Sum(x => x.QtySold);
                        var tFree = _model.Rows.Sum(x => x.FreeQty);
                        var tCost = _model.Rows.Sum(x => x.PurchaseCost);
                        var tSale = _model.Rows.Sum(x => x.SaleValue);
                        var tProfit = _model.Rows.Sum(x => x.Profit);

                        TotalsCell(table.Cell(), "TOTAL");
                        TotalsCell(table.Cell(), "");
                        TotalsCell(table.Cell(), "");
                        TotalsCell(table.Cell(), "");
                        TotalsCell(table.Cell(), "");
                        TotalsCell(table.Cell().AlignRight(), tQty.ToString("0.##"));
                        TotalsCell(table.Cell().AlignRight(), tFree.ToString("0.##"));
                        TotalsCell(table.Cell().AlignRight(), tCost.ToString("0.00"));
                        TotalsCell(table.Cell().AlignRight(), tSale.ToString("0.00"));
                        TotalsCell(table.Cell().AlignRight(), tProfit.ToString("0.00"));
                        TotalsCell(table.Cell().AlignRight(), tSale > 0 ? (tProfit / tSale * 100).ToString("0.00") : "0.00");
                        TotalsCell(table.Cell(), "");
                        TotalsCell(table.Cell(), "");
                        TotalsCell(table.Cell(), "");
                    }
                });
            });
        });
    }

    private static void KpiCell(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
        {
            col.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1).SemiBold();
            col.Item().Text(value).FontSize(14).FontColor(Colors.Black).Bold();
        });
    }

    private static void HeaderCell(IContainer container, string text)
    {
        container.DefaultTextStyle(x => x.SemiBold().FontSize(6))
            .BorderBottom(1).BorderColor(Colors.Black)
            .PaddingVertical(2).PaddingHorizontal(2)
            .Text(text);
    }

    private static void DataCell(IContainer container, string text)
    {
        container.DefaultTextStyle(x => x.FontSize(7))
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten4)
            .PaddingVertical(1).PaddingHorizontal(2)
            .Text(text);
    }

    private static void TotalsCell(IContainer container, string text)
    {
        container.DefaultTextStyle(x => x.SemiBold().FontSize(7))
            .BorderTop(1).BorderColor(Colors.Black)
            .PaddingVertical(1).PaddingHorizontal(2)
            .Text(text);
    }
}
