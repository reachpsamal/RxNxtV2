using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Web.ViewModels;

namespace Rxnxt.Web.Controllers;

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

        var hasSearch = from.HasValue || to.HasValue || !string.IsNullOrWhiteSpace(q);

        SalesReturnFilterViewModel filter;
        List<SalesReturnRowViewModel> rows;

        if (!hasSearch)
        {
            filter = new SalesReturnFilterViewModel();
            rows = new List<SalesReturnRowViewModel>();
        }
        else
        {
            var fromDate = (from ?? DateTime.Today).Date;
            var toDate = (to ?? DateTime.Today).Date;

            if (toDate < fromDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            var toDt = toDate.AddDays(1).AddTicks(-1);

            var query = _db.SalesReturnHeaders.AsNoTracking();

            if (from.HasValue || to.HasValue)
            {
                query = query.Where(r => r.BillDate >= fromDate && r.BillDate <= toDt);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(r =>
                    (r.BillNo != null && r.BillNo.ToLower().Contains(term)) ||
                    (r.CustomerID != null && r.CustomerID.ToLower().Contains(term)) ||
                    (r.SaleId != null && r.SaleId.ToLower().Contains(term)) ||
                    (r.SaleId != null && _db.SaleHeaders.Any(h => h.UniqueID == r.SaleId && h.BillNo.ToLower().Contains(term))));
            }

            var results = await query
                .OrderByDescending(r => r.BillDate)
                .ThenByDescending(r => r.ID)
                .ToListAsync(cancellationToken);

            filter = new SalesReturnFilterViewModel
            {
                From = fromDate,
                To = toDate,
                Query = q
            };

            rows = results.Select(r => new SalesReturnRowViewModel
            {
                Id = r.ID,
                BillNo = r.BillNo ?? string.Empty,
                BillDate = r.BillDate,
                CustomerID = r.CustomerID ?? string.Empty,
                BillAmount = r.BillAmount,
                SaleId = r.SaleId
            }).ToList();
        }

        var vm = new SalesReturnViewModel
        {
            Filter = filter,
            Rows = rows
        };

        return View(vm);
    }
}
