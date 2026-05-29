using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using Rxnxt.Business.Data;
using Rxnxt.Web.Pdf;

namespace Rxnxt.Web.Controllers;

[Authorize]
public class BillController : Controller
{
    private readonly PharmacyDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public BillController(PharmacyDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Print(string billType, string id, string copy = "Both")
    {
        var uniqueId = await ResolveUniqueIdAsync(id);
        if (string.IsNullOrEmpty(uniqueId))
            return NotFound();

        ViewBag.BillType = billType;
        ViewBag.UniqueId = uniqueId;
        ViewBag.Copy = copy;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(string billType, string uniqueId, string copy = "Both")
    {
        if (string.Equals(billType, "SalesReturn", StringComparison.OrdinalIgnoreCase))
        {
            var pdfBytes = await GenerateReturnPdfAsync(uniqueId);
            if (pdfBytes == null) return NotFound();
            return File(pdfBytes, "application/pdf", $"Return-{uniqueId}.pdf");
        }

        var baseUrl = _configuration["BillApi:BaseUrl"]
            ?? "https://arogyanxt-test-api.arogyanxt.com/api/pharmacy/bills";
        var tenantId = User.FindFirst("TenantID")?.Value
            ?? _configuration["SalesIntegration:TenantId"]
            ?? "687C831E-DBCB-4A01-A2C6-2B9D260B2E45";

        var url = $"{baseUrl.TrimEnd('/')}/{billType}/{uniqueId}/pdf?copy={copy}";

        var client = _httpClientFactory.CreateClient("BillApi");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("accept", "*/*");
        request.Headers.Add("X-Arogya-TenantId", tenantId);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return NotFound();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        return File(bytes, "application/pdf");
    }

    private async Task<byte[]?> GenerateReturnPdfAsync(string uniqueId)
    {
        var header = await _db.SalesReturnHeaders.FirstOrDefaultAsync(h => h.UniqueID == uniqueId);
        if (header == null) return null;

        var details = await _db.SalesReturnDetails.Where(d => d.UniqueID == uniqueId).ToListAsync();

        var productIds = details.Select(d => d.ProductID).Distinct().ToList();
        var products = await _db.ProductMasters.Where(p => productIds.Contains(p.UniqueID)).ToListAsync();
        var productMap = products.ToDictionary(p => p.UniqueID, p => p.ProductName ?? p.UniqueID);

        var customerName = string.Empty;
        var customerPhone = string.Empty;
        if (!string.IsNullOrWhiteSpace(header.CustomerID))
        {
            var customer = await _db.CustomerMasters.FirstOrDefaultAsync(c => c.UniqueID == header.CustomerID);
            if (customer != null)
            {
                customerName = customer.CustomerName;
                customerPhone = customer.MobileNumber ?? string.Empty;
            }
        }

        var items = details.Select(d => new ReturnInvoicePdfDocument.ReturnItem(
            ProductName: productMap.GetValueOrDefault(d.ProductID ?? string.Empty, d.ProductID ?? string.Empty),
            BatchNumber: d.BatchNumber ?? string.Empty,
            ExpiryDate: d.ExpiryDate ?? DateTime.MinValue,
            Qty: d.Qty ?? 0m,
            SalePrice: d.SalePrice ?? 0m,
            ItemTotal: ((d.Qty ?? 0m) * (d.SalePrice ?? 0m))
        )).ToList();

        var data = new ReturnInvoicePdfDocument.ReturnData(
            BillNo: header.BillNo,
            BillDate: header.BillDate,
            CustomerName: customerName,
            CustomerPhone: customerPhone,
            Items: items,
            AmountBeforeTax: header.AmountBeforeTax ?? 0m,
            TaxAmount: header.TaxAmount ?? 0m,
            DiscountAmount: header.DiscountAmount ?? 0m,
            BillAmount: header.BillAmount ?? 0m,
            RoundOff: header.RoundOff ?? 0m
        );

        var doc = new ReturnInvoicePdfDocument(data);
        return doc.GeneratePdf();
    }

    private async Task<string?> ResolveUniqueIdAsync(string id)
    {
        if (Guid.TryParse(id, out _)) return id;
        if (int.TryParse(id, out var intId)) return await ResolveUniqueIdAsync(intId);
        return null;
    }

    private async Task<string?> ResolveUniqueIdAsync(int id)
    {
        var header = await _db.SaleHeaders.FirstOrDefaultAsync(h => h.ID == id);
        if (header != null)
            return header.UniqueID;

        var returnHeader = await _db.SalesReturnHeaders.FirstOrDefaultAsync(h => h.ID == id);
        if (returnHeader != null)
            return returnHeader.UniqueID;

        var sale = await _db.Sales.FindAsync(id);
        if (sale?.InvoiceNumber != null)
        {
            header = await _db.SaleHeaders.FirstOrDefaultAsync(h => h.BillNo == sale.InvoiceNumber);
            if (header != null)
                return header.UniqueID;
        }

        return null;
    }
}
