using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Controllers;

[Authorize]
public sealed class SalesReturnController : Controller
{
    private readonly PharmacyDbContext _db;

    public SalesReturnController(PharmacyDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? q, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var hasDateFilter = from.HasValue && to.HasValue;
        var hasQuery = !string.IsNullOrWhiteSpace(q);

        if (!hasDateFilter && !hasQuery)
        {
            return View(new SalesReturnViewModel
            {
                Filter = new SalesReturnFilterViewModel
                {
                    From = DateTime.Today,
                    To = DateTime.Today,
                    Query = q
                }
            });
        }

        var headerQuery = _db.SaleHeaders.AsNoTracking();

        if (!hasQuery && hasDateFilter)
        {
            var fromDate = from.Value.Date;
            var toDate = to.Value.Date;
            if (toDate < fromDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }
            var toDt = toDate.AddDays(1).AddTicks(-1);
            headerQuery = headerQuery.Where(h => h.BillDate >= fromDate && h.BillDate <= toDt);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            headerQuery = headerQuery.Where(h => h.BillNo.ToLower().Contains(term));
        }

        var headers = await headerQuery
            .OrderByDescending(h => h.BillDate)
            .ThenByDescending(h => h.ID)
            .ToListAsync(cancellationToken);

        // Resolve customer names
        var customerIdRawValues = headers
            .Select(h => (h.CustomerID ?? string.Empty).Trim())
            .Where(cid => !string.IsNullOrWhiteSpace(cid) && cid != "0")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var numericCustomerIds = customerIdRawValues
            .Select(cid => int.TryParse(cid, out var n) ? (int?)n : null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

        var customerMasters = (customerIdRawValues.Count == 0 && numericCustomerIds.Count == 0)
            ? new List<CustomerMasterRow>()
            : await _db.CustomerMasters
                .AsNoTracking()
                .Where(c => customerIdRawValues.Contains(c.UniqueID) || numericCustomerIds.Contains(c.ID))
                .ToListAsync(cancellationToken);

        var customerByUniqueId = customerMasters
            .Where(c => !string.IsNullOrWhiteSpace(c.UniqueID))
            .GroupBy(c => c.UniqueID.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var customerById = customerMasters
            .GroupBy(c => c.ID)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = headers.Select(h =>
        {
            var customerUniqueId = (h.CustomerID ?? string.Empty).Trim();
            string customerName;
            string? customerPhone;

            if (!string.IsNullOrWhiteSpace(customerUniqueId) && customerByUniqueId.TryGetValue(customerUniqueId, out var c))
            {
                customerName = c.CustomerName ?? string.Empty;
                customerPhone = c.MobileNumber;
            }
            else if (int.TryParse(customerUniqueId, out var numericId) && customerById.TryGetValue(numericId, out var c2))
            {
                customerName = c2.CustomerName ?? string.Empty;
                customerPhone = c2.MobileNumber;
            }
            else
            {
                customerName = string.IsNullOrWhiteSpace(customerUniqueId) || customerUniqueId == "0" ? "Walk-in" : "Walk-in";
                customerPhone = null;
            }

            return new SalesReturnRowViewModel
            {
                Id = h.ID,
                InvoiceNumber = h.BillNo ?? string.Empty,
                SaleDate = h.BillDate,
                CustomerName = customerName,
                CustomerPhone = customerPhone,
                GrandTotal = h.BillAmount ?? 0,
                PaymentStatus = h.ActiveStatus ? "Completed" : "Cancelled"
            };
        }).ToList();

        var vm = new SalesReturnViewModel
        {
            Filter = new SalesReturnFilterViewModel
            {
                From = from ?? DateTime.Today,
                To = to ?? DateTime.Today,
                Query = q
            },
            Rows = rows
        };

        return View(vm);
    }
}
