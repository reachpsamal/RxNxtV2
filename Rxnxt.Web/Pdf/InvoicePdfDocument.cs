using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Rxnxt.Domain.Models;

namespace Rxnxt.Web.Pdf;

public sealed class InvoicePdfDocument : IDocument
{
    private readonly Sale _sale;

    private sealed record InvoiceTotals(
        string PaymentMode,
        string PaymentsText,
        string AmountInWords,
        decimal TaxableAmt,
        decimal TaxAmt,
        decimal ItemDiscountAmt,
        decimal AdditionalDiscountAmt,
        decimal RoundedGrandTotal,
        decimal RoundOffAmt,
        Dictionary<decimal, decimal> TaxTotalByRate,
        Dictionary<decimal, decimal> CgstByRate,
        Dictionary<decimal, decimal> SgstByRate);

    public InvoicePdfDocument(Sale sale)
    {
        _sale = sale;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        const int itemsPerPage = 15;

        var allItems = (_sale.SaleItems ?? new List<SaleItem>()).ToList();
        var payments = (_sale.Payments ?? new List<Payment>()).ToList();

        static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        static decimal IncludedTax(decimal taxInclusiveTotal, decimal rate) => rate > 0 ? (taxInclusiveTotal * (rate / (100m + rate))) : 0m;
        static decimal LineTotal(SaleItem it) => Math.Max(0m, (it.Price * it.Quantity) - it.DiscountAmount);
        static decimal IncludedTaxRounded(decimal taxInclusiveTotal, decimal rate) => Round2(IncludedTax(taxInclusiveTotal, rate));

        var lineTotalSum = allItems.Sum(LineTotal);
        var taxAmt = allItems.Sum(i => IncludedTaxRounded(LineTotal(i), i.TaxPercent));
        var taxableAmt = lineTotalSum - taxAmt;
        var itemDiscountAmt = allItems.Sum(i => i.DiscountAmount);
        var additionalDiscountAmt = _sale.AdditionalDiscount;
        var netGrandTotal = Math.Max(0m, lineTotalSum - additionalDiscountAmt);
        var roundedGrandTotal = Math.Round(netGrandTotal, 0, MidpointRounding.AwayFromZero);
        var roundOffAmt = roundedGrandTotal - netGrandTotal;

        var taxTotalByRate = allItems
            .GroupBy(i => i.TaxPercent)
            .ToDictionary(g => g.Key, g => g.Sum(x => IncludedTaxRounded(LineTotal(x), x.TaxPercent)));

        var cgstByRate = new Dictionary<decimal, decimal>();
        var sgstByRate = new Dictionary<decimal, decimal>();
        foreach (var kv in taxTotalByRate)
        {
            var rate = kv.Key;
            if (rate <= 0) continue;
            var slabTax = kv.Value;
            var half = Round2(slabTax / 2m);
            var otherHalf = Round2(slabTax - half);
            cgstByRate[rate] = half;
            sgstByRate[rate] = otherHalf;
        }

        string FormatMoney(decimal value) => string.Format(CultureInfo.InvariantCulture, "₹ {0:0.00}", value);
        var paymentsText = BuildPaymentsText(payments, FormatMoney);
        var words = NumberToWords((long)roundedGrandTotal);
        var amountInWords = $"{words} Rupees Only";

        var totals = new InvoiceTotals(
            GetPaymentMode(payments),
            paymentsText,
            amountInWords,
            taxableAmt,
            taxAmt,
            itemDiscountAmt,
            additionalDiscountAmt,
            roundedGrandTotal,
            roundOffAmt,
            taxTotalByRate,
            cgstByRate,
            sgstByRate);

        var pages = Chunk(allItems, itemsPerPage);
        if (pages.Count == 0)
            pages.Add(new List<SaleItem>());

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
                    ComposeCopy(col, "Customer Copy", pageItems, totals, showTotals);
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    ComposeCopy(col, "Office Copy", pageItems, totals, showTotals);
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

    private void ComposeCopy(ColumnDescriptor col, string copyLabel, List<SaleItem> pageItems, InvoiceTotals totals, bool showTotals)
    {
        static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        static decimal IncludedTax(decimal taxInclusiveTotal, decimal rate) => rate > 0 ? (taxInclusiveTotal * (rate / (100m + rate))) : 0m;
        static decimal LineTotal(SaleItem it) => Math.Max(0m, (it.Price * it.Quantity) - it.DiscountAmount);
        static decimal IncludedTaxRounded(decimal taxInclusiveTotal, decimal rate) => Round2(IncludedTax(taxInclusiveTotal, rate));

        var fixedRates = new[] { 5m, 12m, 18m };

        string FormatMoney(decimal value) => string.Format(CultureInfo.InvariantCulture, "₹ {0:0.00}", value);

        col.Item().Row(r =>
        {
            r.RelativeItem().Column(left =>
            {
                left.Item().Text(text =>
                {
                    text.Span("Invoice No:").SemiBold();
                    text.Span($" {_sale.InvoiceNumber ?? string.Empty}");
                });
                left.Item().Text(text =>
                {
                    text.Span("Customer Name:").SemiBold();
                    text.Span($" {_sale.Customer?.Name ?? string.Empty}");
                });
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
                    text.Span($" {_sale.SaleDate:dd-MMM-yyyy hh:mm tt}");
                });
                right.Item().Text(text =>
                {
                    text.Span("Payment Mode:").SemiBold();
                    text.Span($" {totals.PaymentMode}");
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
                columns.ConstantColumn(28);
                columns.ConstantColumn(45);
                columns.ConstantColumn(45);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyleHeader).Text("Product");
                header.Cell().Element(CellStyleHeader).Text("Batch");
                header.Cell().Element(CellStyleHeader).Text("Exp");
                header.Cell().Element(CellStyleHeader).Text("Unit");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Qty");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Mrp");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Disc");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Tax");
                header.Cell().Element(CellStyleHeader).AlignRight().Text("Total");
            });

            for (var i = 0; i < pageItems.Count; i++)
            {
                var it = pageItems[i];
                table.Cell().Element(CellStyleBody).Text(it.ProductName);
                table.Cell().Element(CellStyleBody).Text(it.BatchNumber);
                table.Cell().Element(CellStyleBody).Text(it.ExpiryDate.ToString("MM-yy"));
                table.Cell().Element(CellStyleBody).Text((it.UnitType ?? string.Empty).ToUpperInvariant());
                table.Cell().Element(CellStyleBody).AlignRight().Text(it.Quantity.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(CellStyleBody).AlignRight().Text(it.Price.ToString("0.00", CultureInfo.InvariantCulture));
                table.Cell().Element(CellStyleBody).AlignRight().Text(it.DiscountAmount.ToString("0.00", CultureInfo.InvariantCulture));
                table.Cell().Element(CellStyleBody).AlignRight().Text(IncludedTaxRounded(LineTotal(it), it.TaxPercent).ToString("0.00", CultureInfo.InvariantCulture));
                table.Cell().Element(CellStyleBody).AlignRight().Text(LineTotal(it).ToString("0.00", CultureInfo.InvariantCulture)).SemiBold();
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
                r.Spacing(12);
                r.RelativeItem().Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });

                    t.Header(h =>
                    {
                        h.Cell().Element(TaxHdr).Text("GST");
                        h.Cell().Element(TaxHdr).AlignRight().Text("5%");
                        h.Cell().Element(TaxHdr).AlignRight().Text("12%");
                        h.Cell().Element(TaxHdr).AlignRight().Text("18%");
                    });

                    t.Cell().Element(TaxCellLbl).Text("CGST");
                    foreach (var rate in fixedRates)
                    {
                        var total = totals.CgstByRate.TryGetValue(rate, out var v) ? v : 0m;
                        t.Cell().Element(TaxCellNum).AlignRight().Text(FormatMoney(total));
                    }

                    t.Cell().Element(TaxCellLbl).Text("SGST");
                    foreach (var rate in fixedRates)
                    {
                        var total = totals.SgstByRate.TryGetValue(rate, out var v) ? v : 0m;
                        t.Cell().Element(TaxCellNum).AlignRight().Text(FormatMoney(total));
                    }

                    static IContainer TaxHdr(IContainer c) => c
                        .DefaultTextStyle(x => x.SemiBold())
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(2);

                    static IContainer TaxCellLbl(IContainer c) => c
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(2);

                    static IContainer TaxCellNum(IContainer c) => c
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(2);
                });

                r.ConstantItem(260).PaddingLeft(6).Column(totalsCol =>
                {
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Taxable Amt"); x.ConstantItem(90).AlignRight().Text(FormatMoney(totals.TaxableAmt)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Tax Amt"); x.ConstantItem(90).AlignRight().Text(FormatMoney(totals.TaxAmt)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Item Discount"); x.ConstantItem(90).AlignRight().Text(FormatMoney(totals.ItemDiscountAmt)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Additional Discount"); x.ConstantItem(90).AlignRight().Text(FormatMoney(totals.AdditionalDiscountAmt)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Round-off"); x.ConstantItem(90).AlignRight().Text(FormatMoney(totals.RoundOffAmt)); });
                    totalsCol.Item().Row(x => { x.RelativeItem().Text("Grand Total").SemiBold(); x.ConstantItem(90).AlignRight().Text(FormatMoney(totals.RoundedGrandTotal)).SemiBold(); });
                });
            });

            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Black);

            col.Item().PaddingTop(6).Column(p =>
            {
                p.Item().PaddingBottom(4).Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8));
                    text.Span("Payment Details:-").SemiBold().Underline();
                });

                p.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8));
                    text.Span(string.IsNullOrWhiteSpace(totals.PaymentsText) ? "-" : totals.PaymentsText);
                });

                p.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8));
                    text.Span("Amount in Words: ").SemiBold();
                    text.Span(totals.AmountInWords).Italic();
                });
            });
        }
    }

    private static string GetPaymentMode(List<Payment> payments)
    {
        var nonZero = payments.Where(p => p.Amount != 0).ToList();
        if (nonZero.Count == 0) return string.Empty;
        if (nonZero.Count > 1) return "Split";
        return (nonZero[0].PaymentMode ?? string.Empty).Trim();
    }

    private static string BuildPaymentsText(List<Payment> payments, Func<decimal, string> formatMoney)
    {
        var groups = payments
            .Where(p => p.Amount != 0)
            .GroupBy(p => (p.PaymentMode ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new
            {
                Amount = g.Sum(x => x.Amount),
                Reference = g.Select(x => x.Reference).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))
            }, StringComparer.OrdinalIgnoreCase);

        var ordered = new[] { "Cash", "Card", "UPI" };
        var parts = new List<string>();

        foreach (var mode in ordered)
        {
            if (!groups.TryGetValue(mode, out var g) || g.Amount == 0) continue;
            var s = $"{mode} {formatMoney(g.Amount)}";
            if (!string.IsNullOrWhiteSpace(g.Reference) &&
                (mode.Equals("UPI", StringComparison.OrdinalIgnoreCase) || mode.Equals("Card", StringComparison.OrdinalIgnoreCase)))
            {
                s += $" ({g.Reference})";
            }
            parts.Add(s);
        }

        return string.Join(", ", parts);
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
