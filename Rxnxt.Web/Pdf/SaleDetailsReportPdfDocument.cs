using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Pdf;

public sealed class SaleDetailsReportPdfDocument : IDocument
{
    private readonly SaleDetailsReportViewModel _model;

    public SaleDetailsReportPdfDocument(SaleDetailsReportViewModel model)
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
                col.Item().Text("Sale Details Report").FontSize(16).Bold();

                col.Item().PaddingTop(2).Text(text =>
                {
                    text.Span($"From: {_model.Filter.From:yyyy-MM-dd}  To: {_model.Filter.To:yyyy-MM-dd}  ");
                    text.Span($"Invoice: {_model.Filter.InvoiceNo ?? "All"}  Customer: {_model.Filter.CustomerName ?? "All"}  Item: {_model.Filter.ItemName ?? "All"}");
                });

                col.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(4);
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
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        var labels = new[] { "Invoice No", "Date", "Customer", "Mobile", "Item", "Batch", "Expiry", "Qty", "Free", "MRP", "Rate", "Disc", "GST%", "Tax", "Net", "Payment", "User" };
                        foreach (var label in labels)
                        {
                            HeaderCell(header.Cell(), label);
                        }
                    });

                    foreach (var r in _model.Rows)
                    {
                        var isCancelled = r.IsCancelled;
                        DataCell(table.Cell(), r.InvoiceNo, isCancelled);
                        DataCell(table.Cell().AlignRight(), r.InvoiceDate.ToString("dd-MM-yy"), isCancelled);
                        DataCell(table.Cell(), r.CustomerName, isCancelled);
                        DataCell(table.Cell().AlignRight(), r.Mobile, isCancelled);
                        DataCell(table.Cell(), r.ItemName, isCancelled);
                        DataCell(table.Cell(), r.Batch, isCancelled);
                        DataCell(table.Cell(), r.Expiry, isCancelled);
                        DataCell(table.Cell().AlignRight(), r.Qty.ToString("0.##"), isCancelled);
                        DataCell(table.Cell().AlignRight(), r.FreeQty.ToString("0.##"), isCancelled);
                        DataCell(table.Cell().AlignRight(), r.Mrp.ToString("0.00"), isCancelled);
                        DataCell(table.Cell().AlignRight(), r.Rate.ToString("0.00"), isCancelled);
                        DataCell(table.Cell().AlignRight(), r.Discount.ToString("0.00"), isCancelled);
                        DataCell(table.Cell().AlignRight(), r.GstPercent.ToString("0.##"), isCancelled);
                        DataCell(table.Cell().AlignRight(), r.TaxAmount.ToString("0.00"), isCancelled);
                        DataCell(table.Cell().AlignRight(), r.NetAmount.ToString("0.00"), isCancelled);
                        DataCell(table.Cell(), r.PaymentMode, isCancelled);
                        DataCell(table.Cell(), r.CreatedBy, isCancelled);
                    }
                });
            });
        });
    }

    private static void HeaderCell(IContainer container, string text)
    {
        container.DefaultTextStyle(x => x.SemiBold().FontSize(6))
            .BorderBottom(1).BorderColor(Colors.Black)
            .PaddingVertical(2).PaddingHorizontal(2)
            .Text(text);
    }

    private static void DataCell(IContainer container, string text, bool isCancelled)
    {
        container.DefaultTextStyle(x => isCancelled ? x.FontSize(7).FontColor(Colors.Red.Darken3) : x.FontSize(7))
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten4)
            .PaddingVertical(1).PaddingHorizontal(2)
            .Text(text);
    }
}
