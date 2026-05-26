using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;

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
    public async Task<IActionResult> Print(string billType, int id, string copy = "Both")
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

    private async Task<string?> ResolveUniqueIdAsync(int id)
    {
        var header = await _db.SaleHeaders.FirstOrDefaultAsync(h => h.ID == id);
        if (header != null)
            return header.UniqueID;

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
