using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Rxnxt.Web.Pdf;

public sealed class ReturnInvoicePdfDocument : IDocument
{
    private readonly ReturnData _data;

    public sealed record ReturnData(
        string BillNo,
        DateTime BillDate,
        string CustomerName,
        string CustomerPhone,
        List<ReturnItem> Items,
        decimal AmountBeforeTax,
        decimal TaxAmount,
        decimal DiscountAmount,
        decimal BillAmount,
        decimal RoundOff
    );

    public sealed record ReturnItem(
        string ProductName,
        string BatchNumber,
        DateTime ExpiryDate,
        decimal Qty,
        decimal SalePrice,
        decimal ItemTotal
    );

    public ReturnInvoicePdfDocument(ReturnData data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        const int itemsPerPage = 15;

        var allItems = (_data.Items ?? new List<ReturnItem>()).ToList();

        string FormatMoney(decimal value) => string.Format(CultureInfo.InvariantCulture, "\u20B9 {0:0.00}", value);
        var netGrandTotal = Math.Max(0m, _data.BillAmount);
        var roundedGrandTotal = Math.Round(netGrandTotal, 0, MidpointRounding.AwayFromZero);
        var words = NumberToWords((long)roundedGrandTotal);
        var amountInWords = $"{words} Rupees Only";

        var pages = Chunk(allItems, itemsPerPage);
        if (pages.Count == 0)
            pages.Add(new List<ReturnItem>());

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var pageItems = pages[pageIndex];
            var showTotals = pageIndex == (pages.Count - 1);

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8));
                page.Content().Column(col =>
                {
                    ComposeCopy(col, "Customer Copy", pageItems, showTotals, FormatMoney, amountInWords, netGrandTotal);
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    ComposeCopy(col, "Office Copy", pageItems, showTotals, FormatMoney, amountInWords, netGrandTotal);
                });
            });
        }
    }

    private static List<List<T>> Chunk<T>(List<T> source, int size)
    {
        var pages = new List<List<T>>();
        if (size <= 0) return pages;
        for (var i = 0; i < source.Count; i += size)
            pages.Add(source.Skip(i).Take(size).ToList());
        return pages;
    }

    private void ComposeCopy(ColumnDescriptor col, string copyLabel, List<ReturnItem> pageItems, bool showTotals, Func<decimal, string> fmt, string amountInWords, decimal netGrandTotal)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().Column(left =>
            {
                left.Item().Text(text =>
                {
                    text.Span("Return Invoice No:").SemiBold();
                    text.Span($" {_data.BillNo}");
                });
                left.Item().Text(text =>
                {
                    text.Span("Customer Name:").SemiBold();
                    text.Span($" {_data.CustomerName}");
                });
                if (!string.IsNullOrWhiteSpace(_data.CustomerPhone))
                {
                    left.Item().Text(text =>
                    {
                        text.Span("Phone:").SemiBold();
                        text.Span($" {_data.CustomerPhone}");
                    });
                }
                left.Item().Text(text =>
                {
                    text.Span("Doctor:").SemiBold();
                    text.Span(" __________");
                });
            });

            r.ConstantItem(260).Column(right =>
            {
                right.Item().AlignRight().Text(copyLabel).SemiBold();
                right.Item().Text(text =>
                {
                    text.Span("Date:").SemiBold();
                    text.Span($" {_data.BillDate:dd-MMM-yyyy hh:mm tt}");
                });
                right.Item().Text(text =>
                {
                    text.Span("Refund Mode:").SemiBold();
                    text.Span(" Cash");
                });
                right.Item().Text(text =>
                {
                    text.Span("OP/IP No:").SemiBold();
                    text.Span(" __________");
                });
            });
        });

        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Black);

        col.Item().PaddingTop(2).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(6);
                columns.ConstantColumn(70);
                columns.ConstantColumn(34);
                columns.ConstantColumn(34);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyleHeader).Text("Product");
                header.Cell().Element(CellStyleHeader).Text("Batch");
                header.Cell().Element(CellStyleHeader).Text("Exp");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Qty");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Rate");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Total");
            });

            for (var i = 0; i < pageItems.Count; i++)
            {
                var it = pageItems[i];
                table.Cell().Element(CellStyleBody).Text(it.ProductName);
                table.Cell().Element(CellStyleBody).Text(it.BatchNumber);
                table.Cell().Element(CellStyleBody).Text(it.ExpiryDate.ToString("MM-yy"));
                table.Cell().Element(CellStyleBody).AlignRight().Text(it.Qty.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(CellStyleBody).AlignRight().Text(it.SalePrice.ToString("0.00", CultureInfo.InvariantCulture));
                table.Cell().Element(CellStyleBody).AlignRight().Text(it.ItemTotal.ToString("0.00", CultureInfo.InvariantCulture)).SemiBold();
            }

            static IContainer CellStyleHeader(IContainer c) => c
                .DefaultTextStyle(x => x.SemiBold().FontSize(8))
                .BorderBottom(1)
                .BorderColor(Colors.Black)
                .PaddingVertical(2)
                .PaddingHorizontal(2);

            static IContainer CellStyleBody(IContainer c) => c
                .DefaultTextStyle(x => x.FontSize(8))
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten4)
                .PaddingVertical(1)
                .PaddingHorizontal(2);
        });

        if (showTotals)
        {
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Black);

            col.Item().PaddingTop(8).Row(r =>
            {
                r.RelativeItem();
                r.ConstantItem(260).Column(totalsCol =>
                {
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Taxable Amt"); x.ConstantItem(90).AlignRight().Text(fmt(_data.AmountBeforeTax)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Tax Amt"); x.ConstantItem(90).AlignRight().Text(fmt(_data.TaxAmount)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Discount"); x.ConstantItem(90).AlignRight().Text(fmt(_data.DiscountAmount)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Round-off"); x.ConstantItem(90).AlignRight().Text(fmt(_data.RoundOff)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Refund Amount").SemiBold(); x.ConstantItem(90).AlignRight().Text(fmt(netGrandTotal)).SemiBold(); });
                });
            });

            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Black);

            col.Item().PaddingTop(6).Column(p =>
            {
                p.Item().PaddingBottom(4).Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8));
                    text.Span("Refund Details:-").SemiBold().Underline();
                });

                p.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8));
                    text.Span("Payment Mode: Cash");
                });

                p.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8));
                    text.Span("Amount in Words: ").SemiBold();
                    text.Span(amountInWords).Italic();
                });
            });
        }
    }

    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        var words = string.Empty;

        if ((number / 10000000) > 0)
        {
            words += NumberToWords(number / 10000000) + " Crore ";
            number %= 10000000;
        }

        if ((number / 100000) > 0)
        {
            words += NumberToWords(number / 100000) + " Lakh ";
            number %= 100000;
        }

        if ((number / 1000) > 0)
        {
            words += NumberToWords(number / 1000) + " Thousand ";
            number %= 1000;
        }

        if ((number / 100) > 0)
        {
            words += NumberToWords(number / 100) + " Hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            if (words != string.Empty) words += "";

            var unitsMap = new[]
            {
                "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
                "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
            };
            var tensMap = new[]
            {
                "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
            };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += " " + unitsMap[number % 10];
            }
        }

        return words.Trim();
    }
}
