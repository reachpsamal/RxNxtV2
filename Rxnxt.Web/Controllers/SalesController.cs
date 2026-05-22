using Microsoft.AspNetCore.Mvc;
using Rxnxt.Web.ViewModels;
using Rxnxt.Services.Implementations;
using Rxnxt.Business.DTOs;
using System;
using System.Linq;
using System.Text.Json;
using System.Globalization;
using Rxnxt.Web.Pdf;
using QuestPDF.Fluent;
using Rxnxt.Business.Data;
using Rxnxt.Business.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Rxnxt.Web.Controllers
{
    public class SalesController : Controller
    {
        private readonly CustomerService _customerService;
        private readonly SaleService _saleService;
        private readonly StockService _stockService;
        private readonly PharmacyDbContext _db;
        private readonly IConfiguration _configuration;

        public SalesController(CustomerService customerService, SaleService saleService, StockService stockService, PharmacyDbContext db, IConfiguration configuration)
        {
            _customerService = customerService;
            _saleService = saleService;
            _stockService = stockService;
            _db = db;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var stocks = await _stockService.GetStocksAsync(cancellationToken);
            var vm = new SaleViewModel
            {
                PrefetchedStocks = stocks.Select(StockSearchItemViewModel.FromDto).ToList()
            };
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SearchBatch(string q, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return PartialView("_BatchDropdown", new List<StockSearchItemViewModel>());
            }

            var results = (await _stockService.SearchBatchAsync(q, 20, cancellationToken))
                .Select(StockSearchItemViewModel.FromDto)
                .ToList();

            return PartialView("_BatchDropdown", results);
        }

        [HttpGet]
        public async Task<IActionResult> SearchMedicine(string q, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return PartialView("_MedicineDropdown", new List<StockSearchItemViewModel>());
            }

            var results = (await _stockService.SearchMedicineAsync(q, 20, cancellationToken))
                .Select(StockSearchItemViewModel.FromDto)
                .ToList();

            return PartialView("_MedicineDropdown", results);
        }

        [HttpGet]
        public async Task<IActionResult> SearchCustomer(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return PartialView("_CustomerDropdown", new List<CustomerSearchItemViewModel>());
            }

            var vm = (await _customerService.SearchAsync(q))
                .Take(20)
                .Select(CustomerSearchItemViewModel.FromDto)
                .ToList();

            return PartialView("_CustomerDropdown", vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerCard(int id)
        {
            var dto = await _customerService.GetByIdAsync(id);
            if (dto == null) return NotFound();
            return PartialView("_SelectedCustomerCard", CustomerSearchItemViewModel.FromDto(dto));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer([FromForm] string name, [FromForm] string phone)
        {
            var (customer, error) = await _customerService.CreateAsync(name, phone);
            if (customer == null) return BadRequest(error ?? "Failed to save customer");
            return PartialView("_SelectedCustomerCard", CustomerSearchItemViewModel.FromDto(customer));
        }

        [HttpGet]
        public async Task<IActionResult> GetStockByProductBatch(Guid productId, string batchNumber, CancellationToken cancellationToken)
        {
            if (productId == Guid.Empty || string.IsNullOrWhiteSpace(batchNumber)) return BadRequest();

            var match = await _stockService.GetStockByProductBatchAsync(productId, batchNumber, cancellationToken);
            if (match == null) return NotFound();

            var vm = StockDetailsViewModel.FromDto(match);
            return PartialView("_StockDetailsPayload", vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductUomOptions(Guid productId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            if (productId == Guid.Empty) return BadRequest();

            var pid = productId.ToString();
            var pm = await _db.ProductMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UniqueID == pid);

            if (pm == null)
            {
                return Json(new { ok = false, message = "Product not found" }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }

            var baseId = (pm.UOMID ?? string.Empty).Trim();
            var otherId = (pm.OtherUOMID ?? string.Empty).Trim();
            var factor = pm.ConversionFactor.GetValueOrDefault(1m);

            var uomIds = new[] { baseId, otherId }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var names = uomIds.Count == 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : await _db.UomMasters
                    .AsNoTracking()
                    .Where(u => uomIds.Contains(u.UniqueID))
                    .Select(u => new { u.UniqueID, u.UOMName })
                    .ToDictionaryAsync(x => x.UniqueID, x => x.UOMName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            var baseName = (!string.IsNullOrWhiteSpace(baseId) && names.TryGetValue(baseId, out var bn)) ? bn : string.Empty;
            var otherName = (!string.IsNullOrWhiteSpace(otherId) && names.TryGetValue(otherId, out var on)) ? on : string.Empty;

            return Json(new
            {
                ok = true,
                baseUomName = baseName,
                otherUomName = otherName,
                conversionFactor = factor,
                uomId = baseId,
                otherUomId = otherId
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        [HttpGet]
        public async Task<IActionResult> AdvancedBatchSearch(
            
            string? batchNumber,
            string? medicineName,
            string? composition,
            DateTime? expiryFrom,
            DateTime? expiryTo,
            CancellationToken cancellationToken)
        {
            var results = (await _stockService.AdvancedBatchSearchAsync(batchNumber, medicineName, expiryFrom, expiryTo, 50, cancellationToken))
                .Select(StockSearchItemViewModel.FromDto)
                .ToList();

            return PartialView("_AdvancedBatchResults", results);
        }

        [HttpGet]
        public async Task<IActionResult> History(DateTime? from, DateTime? to, string? q, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var fromDate = (from ?? DateTime.Today).Date;
            var toDate = (to ?? DateTime.Today).Date;

            if (toDate < fromDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            var fromDt = fromDate;
            var toDt = toDate.AddDays(1).AddTicks(-1);

            var sales = await _saleService.SearchSalesAsync(fromDt, toDt, q);

            var vm = new SalesHistoryViewModel
            {
                Filter = new SalesHistoryFilterViewModel
                {
                    From = fromDate,
                    To = toDate,
                    Query = q
                },
                Rows = sales.Select(s => new SalesHistoryRowViewModel
                {
                    Id = s.Id,
                    SaleDate = s.SaleDate,
                    InvoiceNumber = s.InvoiceNumber ?? string.Empty,
                    CustomerName = s.Customer?.Name ?? string.Empty,
                    CustomerPhone = s.Customer?.Phone ?? string.Empty,
                    GrandTotal = s.GrandTotal,
                    PaymentStatus = s.PaymentStatus
                }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Summary(SaleSummaryFilterViewModel filter, CancellationToken ct)
        {
            _ = ct;
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var storeOptions = await _db.SaleHeaders
                .Where(h => h.StoreId != null && h.StoreId != "" && h.TenantId == tenantId)
                .Select(h => h.StoreId!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(ct);

            var userOptions = await _db.SaleHeaders
                .Where(h => h.CreatedBy != null && h.CreatedBy != "" && h.TenantId == tenantId)
                .Select(h => h.CreatedBy)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(ct);

            var vm = await GetSummaryDataAsync(filter, from, to, tenantId, ct);
            vm.Filter.StoreOptions = storeOptions;
            vm.Filter.UserOptions = userOptions;

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SummaryExcel(SaleSummaryFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var vm = await GetSummaryDataAsync(filter, from, to, tenantId, ct);
            var excelService = new Exports.SaleSummaryExcelService();
            var bytes = excelService.Generate(vm, filter);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SaleSummaryReport.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> SummaryPdf(SaleSummaryFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var vm = await GetSummaryDataAsync(filter, from, to, tenantId, ct);
            var doc = new Pdf.SaleSummaryPdfDocument(vm, filter);
            var bytes = doc.GeneratePdf();
            return File(bytes, "application/pdf", "SaleSummaryReport.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> SaleDetailsReport(SaleDetailsReportFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var userOptions = await _db.SaleHeaders
                .Where(h => h.CreatedBy != null && h.CreatedBy != "" && h.TenantId == tenantId)
                .Select(h => h.CreatedBy)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(ct);

            var vm = await GetSaleDetailsDataAsync(filter, from, to, tenantId, ct);
            vm.Filter.UserOptions = userOptions;
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SaleDetailsReportExcel(SaleDetailsReportFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var vm = await GetSaleDetailsDataAsync(filter, from, to, tenantId, ct);
            var excelService = new Exports.SaleDetailsReportExcelService();
            var bytes = excelService.Generate(vm, filter);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SaleDetailsReport.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> SaleDetailsReportPdf(SaleDetailsReportFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var vm = await GetSaleDetailsDataAsync(filter, from, to, tenantId, ct);
            var doc = new Pdf.SaleDetailsReportPdfDocument(vm);
            var bytes = doc.GeneratePdf();
            return File(bytes, "application/pdf", "SaleDetailsReport.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ItemWise(ItemWiseFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var storeOptions = await _db.SaleHeaders
                .Where(h => h.StoreId != null && h.StoreId != "" && h.TenantId == tenantId)
                .Select(h => h.StoreId!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(ct);

            var userOptions = await _db.SaleHeaders
                .Where(h => h.CreatedBy != null && h.CreatedBy != "" && h.TenantId == tenantId)
                .Select(h => h.CreatedBy)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync(ct);

            var vm = await GetItemWiseDataAsync(filter, from, to, tenantId, ct);
            vm.Filter.StoreOptions = storeOptions;
            vm.Filter.UserOptions = userOptions;

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ItemWiseExcel(ItemWiseFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var vm = await GetItemWiseDataAsync(filter, from, to, tenantId, ct);
            var excelService = new Exports.ItemWiseReportExcelService();
            var bytes = excelService.Generate(vm, filter);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ItemWiseReport.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ItemWisePdf(ItemWiseFilterViewModel filter, CancellationToken ct)
        {
            var from = filter.From.Date;
            var to = filter.To.Date.AddDays(1);
            var tenantId = _configuration["SalesIntegration:TenantId"]!;

            var vm = await GetItemWiseDataAsync(filter, from, to, tenantId, ct);
            var doc = new Pdf.ItemWiseReportPdfDocument(vm);
            var bytes = doc.GeneratePdf();
            return File(bytes, "application/pdf", "ItemWiseReport.pdf");
        }

        private async Task<SaleDetailsReportViewModel> GetSaleDetailsDataAsync(SaleDetailsReportFilterViewModel filter, DateTime from, DateTime to, string tenantId, CancellationToken ct)
        {
            var headerQuery = _db.SaleHeaders.Where(h => h.TenantId == tenantId && h.BillDate >= from && h.BillDate <= to);

            if (filter.BillStatus == "Completed")
                headerQuery = headerQuery.Where(h => h.ActiveStatus);
            else if (filter.BillStatus == "Cancelled")
                headerQuery = headerQuery.Where(h => !h.ActiveStatus);

            if (!string.IsNullOrWhiteSpace(filter.InvoiceNo))
                headerQuery = headerQuery.Where(h => h.BillNo.Contains(filter.InvoiceNo));

            if (!string.IsNullOrWhiteSpace(filter.CreatedBy))
                headerQuery = headerQuery.Where(h => h.CreatedBy == filter.CreatedBy);

            if (!string.IsNullOrWhiteSpace(filter.PaymentMode) && filter.PaymentMode != "All")
            {
                var paymentModeIds = _db.SalePayments
                    .Where(p => p.PaymentMode == filter.PaymentMode)
                    .Select(p => p.SaleId);
                headerQuery = headerQuery.Where(h => paymentModeIds.Contains(h.UniqueID));
            }

            var materializedHeaders = await headerQuery.ToListAsync(ct);
            if (materializedHeaders.Count == 0)
                return new SaleDetailsReportViewModel { Filter = filter };

            var headerUniqueIds = materializedHeaders.Select(h => h.UniqueID).ToList();

            var details = await _db.SaleDetails
                .Where(d => headerUniqueIds.Contains(d.SaleID))
                .ToListAsync(ct);

            var customerIds = materializedHeaders.Select(h => h.CustomerID).Distinct().ToList();
            var customers = await _db.CustomerMasters
                .Where(c => customerIds.Contains(c.UniqueID))
                .ToDictionaryAsync(c => c.UniqueID, ct);

            var productIds = details.Select(d => d.ProductID).Distinct().ToList();
            var products = await _db.ProductMasters
                .Where(p => productIds.Contains(p.UniqueID))
                .ToDictionaryAsync(p => p.UniqueID, ct);

            var paymentRaw = await _db.SalePayments
                .Where(p => headerUniqueIds.Contains(p.SaleId))
                .GroupBy(p => p.SaleId)
                .Select(g => new { SaleId = g.Key, Modes = g.Select(p => p.PaymentMode).Distinct() })
                .ToListAsync(ct);
            var paymentLookup = paymentRaw.ToDictionary(p => p.SaleId, p => string.Join(", ", p.Modes));

            var headerLookup = materializedHeaders.ToDictionary(h => h.UniqueID);

            var rawRows = new List<SaleDetailsReportRowViewModel>();
            foreach (var d in details)
            {
                if (!headerLookup.TryGetValue(d.SaleID, out var h)) continue;

                if (!string.IsNullOrWhiteSpace(filter.CustomerName))
                {
                    customers.TryGetValue(h.CustomerID, out var c);
                    if (c == null || !c.CustomerName.Contains(filter.CustomerName, StringComparison.OrdinalIgnoreCase)) continue;
                }
                if (!string.IsNullOrWhiteSpace(filter.ItemName))
                {
                    products.TryGetValue(d.ProductID, out var p);
                    if (p == null || !p.ProductName.Contains(filter.ItemName, StringComparison.OrdinalIgnoreCase)) continue;
                }

                customers.TryGetValue(h.CustomerID, out var cust);
                products.TryGetValue(d.ProductID, out var prod);

                rawRows.Add(new SaleDetailsReportRowViewModel
                {
                    HeaderId = h.UniqueID,
                    InvoiceNo = h.BillNo,
                    InvoiceDate = h.BillDate,
                    CustomerName = cust?.CustomerName ?? "",
                    Mobile = cust?.MobileNumber ?? "",
                    ItemName = prod?.ProductName ?? "",
                    Batch = d.BatchNumber ?? "",
                    Expiry = d.ExpiryDate?.ToString("MMM-yyyy") ?? "",
                    Qty = d.Qty ?? 0m,
                    FreeQty = d.FreeQty ?? 0m,
                    Mrp = d.MRP ?? 0m,
                    Rate = d.SalePrice ?? 0m,
                    Discount = d.ItemDiscAmount ?? 0m,
                    GstPercent = d.TaxPerc ?? 0m,
                    TaxAmount = d.TotalTaxAmount ?? 0m,
                    NetAmount = d.ItemTotal ?? 0m,
                    PaymentMode = paymentLookup.TryGetValue(h.UniqueID, out var pm) ? pm : "",
                    CreatedBy = h.CreatedBy,
                    IsCancelled = !h.ActiveStatus
                });
            }

            var vm = new SaleDetailsReportViewModel { Filter = filter };
            vm.Rows = rawRows.OrderBy(r => r.InvoiceDate).ThenBy(r => r.InvoiceNo).ToList();
            return vm;
        }

        private async Task<SaleSummaryViewModel> GetSummaryDataAsync(SaleSummaryFilterViewModel filter, DateTime from, DateTime to, string tenantId, CancellationToken ct)
        {
            var headerQuery = _db.SaleHeaders.Where(h => h.BillDate >= from && h.BillDate <= to && h.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(filter.StoreId))
                headerQuery = headerQuery.Where(h => h.StoreId == filter.StoreId);
            if (!string.IsNullOrWhiteSpace(filter.CreatedBy))
                headerQuery = headerQuery.Where(h => h.CreatedBy == filter.CreatedBy);
            if (filter.BillStatus == "Completed")
                headerQuery = headerQuery.Where(h => h.ActiveStatus);
            else if (filter.BillStatus == "Cancelled")
                headerQuery = headerQuery.Where(h => !h.ActiveStatus);

            if (!string.IsNullOrWhiteSpace(filter.PaymentMode) && filter.PaymentMode != "All")
            {
                var paymentModeIds = _db.SalePayments
                    .Where(p => p.PaymentMode == filter.PaymentMode)
                    .Select(p => p.SaleId);
                headerQuery = headerQuery.Where(h => paymentModeIds.Contains(h.UniqueID));
            }

            var vm = new SaleSummaryViewModel { Filter = filter };

            if (filter.GroupBy == "Payment")
            {
                var paymentQuery = headerQuery.Join(
                    _db.SalePayments,
                    h => h.UniqueID,
                    p => p.SaleId,
                    (h, p) => new { h, p });

                var grouped = await paymentQuery
                    .GroupBy(x => x.p.PaymentMode)
                    .Select(g => new
                    {
                        GroupKey = g.Key,
                        BillCount = g.Select(x => x.h.ID).Distinct().Count(),
                        GrossAmount = g.Sum(x => (x.h.BillAmount ?? 0) + (x.h.DiscountAmount ?? 0) + (x.h.ExtraLess ?? 0) - (x.h.RoundOff ?? 0)),
                        Discount = g.Sum(x => (x.h.DiscountAmount ?? 0) + (x.h.ExtraLess ?? 0)),
                        TaxAmount = g.Sum(x => x.h.TaxAmount ?? 0),
                        NetAmount = g.Sum(x => x.h.BillAmount ?? 0),
                        RoundOff = g.Sum(x => x.h.RoundOff ?? 0),
                        PaidAmount = g.Sum(x => x.p.Amount)
                    })
                    .ToListAsync(ct);

                vm.Rows = grouped.Select(g => new SaleSummaryRowViewModel
                {
                    GroupKey = g.GroupKey,
                    BillCount = g.BillCount,
                    GrossAmount = g.GrossAmount,
                    Discount = g.Discount,
                    TaxAmount = g.TaxAmount,
                    NetAmount = g.NetAmount,
                    RoundOff = g.RoundOff,
                    PaidAmount = g.PaidAmount,
                    RefundAmount = 0m,
                    Outstanding = g.NetAmount - g.PaidAmount
                }).OrderBy(r => r.GroupKey).ToList();
            }
            else if (filter.GroupBy == "Month")
            {
                var rawHeader = await headerQuery
                    .GroupBy(h => new { h.BillDate.Year, h.BillDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, BillCount = g.Count(), GrossAmount = g.Sum(h => (h.BillAmount ?? 0) + (h.DiscountAmount ?? 0) + (h.ExtraLess ?? 0) - (h.RoundOff ?? 0)), Discount = g.Sum(h => (h.DiscountAmount ?? 0) + (h.ExtraLess ?? 0)), TaxAmount = g.Sum(h => h.TaxAmount ?? 0), NetAmount = g.Sum(h => h.BillAmount ?? 0), RoundOff = g.Sum(h => h.RoundOff ?? 0) })
                    .ToListAsync(ct);

                var rawPayment = await headerQuery
                    .Join(_db.SalePayments, h => h.UniqueID, p => p.SaleId, (h, p) => new { h, p })
                    .GroupBy(x => new { x.h.BillDate.Year, x.h.BillDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, PaidAmount = g.Sum(x => x.p.Amount) })
                    .ToListAsync(ct);

                var rawRefund = await _db.SalesReturnHeaders
                    .Where(r => r.BillDate >= from && r.BillDate <= to && r.ActiveStatus && r.TenantId == tenantId)
                    .GroupBy(r => new { r.BillDate.Year, r.BillDate.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, RefundAmount = g.Sum(r => r.BillAmount ?? 0) })
                    .ToListAsync(ct);

                var payLookup = rawPayment.ToDictionary(p => $"{p.Year}-{p.Month:D2}");
                var refLookup = rawRefund.ToDictionary(r => $"{r.Year}-{r.Month:D2}");

                vm.Rows = rawHeader.Select(x =>
                {
                    var key = $"{x.Year}-{x.Month:D2}";
                    var paid = payLookup.TryGetValue(key, out var p) ? p.PaidAmount : 0m;
                    return new SaleSummaryRowViewModel
                    {
                        GroupKey = key,
                        BillCount = x.BillCount,
                        GrossAmount = x.GrossAmount,
                        Discount = x.Discount,
                        TaxAmount = x.TaxAmount,
                        NetAmount = x.NetAmount,
                        RoundOff = x.RoundOff,
                        PaidAmount = paid,
                        RefundAmount = refLookup.TryGetValue(key, out var r) ? r.RefundAmount : 0m,
                        Outstanding = x.NetAmount - paid
                    };
                }).OrderBy(r => r.GroupKey).ToList();
            }
            else if (filter.GroupBy == "User")
            {
                var rawHeader = await headerQuery
                    .GroupBy(h => h.CreatedBy ?? "Unknown")
                    .Select(g => new { Key = g.Key, BillCount = g.Count(), GrossAmount = g.Sum(h => (h.BillAmount ?? 0) + (h.DiscountAmount ?? 0) + (h.ExtraLess ?? 0) - (h.RoundOff ?? 0)), Discount = g.Sum(h => (h.DiscountAmount ?? 0) + (h.ExtraLess ?? 0)), TaxAmount = g.Sum(h => h.TaxAmount ?? 0), NetAmount = g.Sum(h => h.BillAmount ?? 0), RoundOff = g.Sum(h => h.RoundOff ?? 0) })
                    .ToListAsync(ct);

                var rawPayment = await headerQuery
                    .Join(_db.SalePayments, h => h.UniqueID, p => p.SaleId, (h, p) => new { h, p })
                    .GroupBy(x => x.h.CreatedBy ?? "Unknown")
                    .Select(g => new { Key = g.Key, PaidAmount = g.Sum(x => x.p.Amount) })
                    .ToListAsync(ct);

                var rawRefund = await _db.SalesReturnHeaders
                    .Where(r => r.BillDate >= from && r.BillDate <= to && r.ActiveStatus && r.TenantId == tenantId)
                    .GroupBy(r => r.CreatedBy ?? "Unknown")
                    .Select(g => new { Key = g.Key, RefundAmount = g.Sum(r => r.BillAmount ?? 0) })
                    .ToListAsync(ct);

                var payLookup = rawPayment.ToDictionary(p => p.Key);
                var refLookup = rawRefund.ToDictionary(r => r.Key);

                vm.Rows = rawHeader.Select(x =>
                {
                    var paid = payLookup.TryGetValue(x.Key, out var p) ? p.PaidAmount : 0m;
                    return new SaleSummaryRowViewModel
                    {
                        GroupKey = x.Key,
                        BillCount = x.BillCount,
                        GrossAmount = x.GrossAmount,
                        Discount = x.Discount,
                        TaxAmount = x.TaxAmount,
                        NetAmount = x.NetAmount,
                        RoundOff = x.RoundOff,
                        PaidAmount = paid,
                        RefundAmount = refLookup.TryGetValue(x.Key, out var r) ? r.RefundAmount : 0m,
                        Outstanding = x.NetAmount - paid
                    };
                }).OrderBy(r => r.GroupKey).ToList();
            }
            else // Day
            {
                var rawHeader = await headerQuery
                    .GroupBy(h => h.BillDate.Date)
                    .Select(g => new { Key = g.Key, BillCount = g.Count(), GrossAmount = g.Sum(h => (h.BillAmount ?? 0) + (h.DiscountAmount ?? 0) + (h.ExtraLess ?? 0) - (h.RoundOff ?? 0)), Discount = g.Sum(h => (h.DiscountAmount ?? 0) + (h.ExtraLess ?? 0)), TaxAmount = g.Sum(h => h.TaxAmount ?? 0), NetAmount = g.Sum(h => h.BillAmount ?? 0), RoundOff = g.Sum(h => h.RoundOff ?? 0) })
                    .ToListAsync(ct);

                var rawPayment = await headerQuery
                    .Join(_db.SalePayments, h => h.UniqueID, p => p.SaleId, (h, p) => new { h, p })
                    .GroupBy(x => x.h.BillDate.Date)
                    .Select(g => new { Key = g.Key, PaidAmount = g.Sum(x => x.p.Amount) })
                    .ToListAsync(ct);

                var rawRefund = await _db.SalesReturnHeaders
                    .Where(r => r.BillDate >= from && r.BillDate <= to && r.ActiveStatus && r.TenantId == tenantId)
                    .GroupBy(r => r.BillDate.Date)
                    .Select(g => new { Key = g.Key, RefundAmount = g.Sum(r => r.BillAmount ?? 0) })
                    .ToListAsync(ct);

                var payLookup = rawPayment.ToDictionary(p => p.Key.ToString("yyyy-MM-dd"));
                var refLookup = rawRefund.ToDictionary(r => r.Key.ToString("yyyy-MM-dd"));

                vm.Rows = rawHeader.Select(x =>
                {
                    var key = x.Key.ToString("yyyy-MM-dd");
                    var paid = payLookup.TryGetValue(key, out var p) ? p.PaidAmount : 0m;
                    return new SaleSummaryRowViewModel
                    {
                        GroupKey = key,
                        BillCount = x.BillCount,
                        GrossAmount = x.GrossAmount,
                        Discount = x.Discount,
                        TaxAmount = x.TaxAmount,
                        NetAmount = x.NetAmount,
                        RoundOff = x.RoundOff,
                        PaidAmount = paid,
                        RefundAmount = refLookup.TryGetValue(key, out var r) ? r.RefundAmount : 0m,
                        Outstanding = x.NetAmount - paid
                    };
                }).OrderBy(r => r.GroupKey).ToList();
            }

            // KPIs (SQL-level aggregates)
            if (vm.Rows.Count > 0)
            {
                vm.TotalBills = vm.Rows.Sum(r => r.BillCount);
                vm.TotalGrossSales = vm.Rows.Sum(r => r.GrossAmount);
                var totalNet = vm.Rows.Sum(r => r.NetAmount);
                vm.AvgBillValue = vm.TotalBills > 0 ? Math.Round(totalNet / vm.TotalBills, 2) : 0m;

                if (filter.GroupBy != "Payment")
                {
                    var paymentKpiQuery = headerQuery
                        .Join(_db.SalePayments, h => h.UniqueID, p => p.SaleId, (h, p) => p);
                    vm.CashAmount = await paymentKpiQuery.Where(p => p.PaymentMode == "Cash").SumAsync(p => p.Amount, ct);
                    vm.UpiAmount = await paymentKpiQuery.Where(p => p.PaymentMode == "UPI").SumAsync(p => p.Amount, ct);
                    vm.CardAmount = await paymentKpiQuery.Where(p => p.PaymentMode == "Card").SumAsync(p => p.Amount, ct);
                    vm.OtherAmount = await paymentKpiQuery.Where(p => p.PaymentMode != "Cash" && p.PaymentMode != "UPI" && p.PaymentMode != "Card").SumAsync(p => p.Amount, ct);
                }

                vm.TotalRefunds = await _db.SalesReturnHeaders
                    .Where(r => r.BillDate >= from && r.BillDate <= to && r.ActiveStatus && r.TenantId == tenantId)
                    .SumAsync(r => r.BillAmount ?? 0m, ct);
                vm.ReturnPercentage = vm.TotalGrossSales > 0 ? Math.Round(vm.TotalRefunds / vm.TotalGrossSales * 100, 2) : 0m;
            }

            return vm;
        }

        private async Task<ItemWiseReportViewModel> GetItemWiseDataAsync(ItemWiseFilterViewModel filter, DateTime from, DateTime to, string tenantId, CancellationToken ct)
        {
            var vm = new ItemWiseReportViewModel { Filter = filter };

            var headerQuery = _db.SaleHeaders.Where(h => h.TenantId == tenantId && h.BillDate >= from && h.BillDate <= to);

            if (!string.IsNullOrWhiteSpace(filter.StoreId))
                headerQuery = headerQuery.Where(h => h.StoreId == filter.StoreId);
            if (!string.IsNullOrWhiteSpace(filter.CreatedBy))
                headerQuery = headerQuery.Where(h => h.CreatedBy == filter.CreatedBy);
            if (filter.BillStatus == "Completed")
                headerQuery = headerQuery.Where(h => h.ActiveStatus);
            else if (filter.BillStatus == "Cancelled")
                headerQuery = headerQuery.Where(h => !h.ActiveStatus);

            var headers = await headerQuery.ToListAsync(ct);
            if (headers.Count == 0) return vm;

            var headerIds = headers.Select(h => h.UniqueID).ToHashSet();
            var headerLookup = headers.ToDictionary(h => h.UniqueID);

            var details = await _db.SaleDetails
                .Where(d => headerIds.Contains(d.SaleID))
                .ToListAsync(ct);

            if (details.Count == 0) return vm;

            var nearExpiryKeys = new HashSet<string>();
            var groupedRaw = details
                .GroupBy(d => new
                {
                    ProductID = d.ProductID ?? "",
                    Batch = d.BatchNumber ?? "",
                    ExpiryKey = d.ExpiryDate?.ToString("yyyy-MM-dd") ?? "",
                    ExpiryDate = d.ExpiryDate
                })
                .Select(g =>
                {
                    foreach (var d in g)
                    {
                        if (d.ExpiryDate.HasValue && headerLookup.TryGetValue(d.SaleID, out var h))
                        {
                            if ((d.ExpiryDate.Value - h.BillDate).TotalDays <= 90)
                            {
                                nearExpiryKeys.Add($"{g.Key.ProductID}|{g.Key.Batch}|{g.Key.ExpiryKey}");
                                break;
                            }
                        }
                    }

                    return new
                    {
                        g.Key.ProductID,
                        g.Key.Batch,
                        g.Key.ExpiryDate,
                        QtySold = g.Sum(d => d.Qty ?? 0m),
                        FreeQty = g.Sum(d => d.FreeQty ?? 0m),
                        PurchaseCost = g.Sum(d => (d.Qty ?? 0m) * (d.PurchasePrice ?? 0m)),
                        SaleValue = g.Sum(d => d.ItemTotal ?? 0m),
                        SalePrice = g.Max(d => d.SalePrice ?? 0m),
                        MRP = g.Max(d => d.MRP ?? 0m)
                    };
                })
                .ToList();

            var productIds = groupedRaw.Select(g => g.ProductID).Distinct().ToList();
            var products = await _db.ProductMasters
                .Where(p => productIds.Contains(p.UniqueID))
                .ToDictionaryAsync(p => p.UniqueID, p => p.ProductName ?? "", ct);

            var stockRows = await _db.ProductStockView
                .Where(s => productIds.Contains(s.ProductID) && s.TenantId == tenantId)
                .ToListAsync(ct);

            var stockLookup = stockRows
                .GroupBy(s => $"{s.ProductID}|{s.BatchNumber ?? ""}|{s.ExpiryDate?.ToString("yyyy-MM-dd") ?? ""}")
                .ToDictionary(g => g.Key, g =>
                {
                    var first = g.First();
                    return new
                    {
                        Manufacturer = first.ManufacturerName ?? "",
                        Stock = g.Sum(s => s.AvailableQty ?? 0m)
                    };
                });

            var nearExpiryRows = new List<ItemWiseRowViewModel>();
            var rawRows = new List<ItemWiseRowViewModel>();

            foreach (var g in groupedRaw)
            {
                var expiryKey = $"{g.ProductID}|{g.Batch}|{g.ExpiryDate?.ToString("yyyy-MM-dd") ?? ""}";
                stockLookup.TryGetValue(expiryKey, out var stockInfo);
                products.TryGetValue(g.ProductID, out var productName);

                var saleValue = g.SaleValue;
                var purchaseCost = g.PurchaseCost;
                var profit = saleValue - purchaseCost;

                var row = new ItemWiseRowViewModel
                {
                    ItemCode = g.ProductID,
                    ItemName = productName ?? "",
                    Manufacturer = stockInfo?.Manufacturer ?? "",
                    Batch = g.Batch,
                    Expiry = g.ExpiryDate?.ToString("MMM-yyyy") ?? "",
                    QtySold = g.QtySold,
                    FreeQty = g.FreeQty,
                    PurchaseCost = purchaseCost,
                    SaleValue = saleValue,
                    Profit = profit,
                    MarginPerc = saleValue > 0 ? Math.Round(profit / saleValue * 100, 2) : 0m,
                    CurrentStock = stockInfo?.Stock ?? 0m,
                    SalePrice = g.SalePrice,
                    MRP = g.MRP
                };

                if (!string.IsNullOrWhiteSpace(filter.Manufacturer) &&
                    !row.Manufacturer.Contains(filter.Manufacturer, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(filter.Batch) &&
                    !row.Batch.Contains(filter.Batch, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (filter.ExpiryFrom.HasValue && g.ExpiryDate.HasValue && g.ExpiryDate.Value < filter.ExpiryFrom.Value)
                    continue;

                if (filter.ExpiryTo.HasValue && g.ExpiryDate.HasValue && g.ExpiryDate.Value > filter.ExpiryTo.Value)
                    continue;

                if (nearExpiryKeys.Contains(expiryKey))
                    nearExpiryRows.Add(row);

                rawRows.Add(row);
            }

            if (!string.IsNullOrWhiteSpace(filter.MovementType) && filter.MovementType != "All" && filter.MovementThreshold.HasValue)
            {
                var productQty = rawRows
                    .GroupBy(r => r.ItemCode)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.QtySold));

                var threshold = filter.MovementThreshold.Value;
                rawRows = rawRows.Where(r =>
                {
                    var total = productQty.TryGetValue(r.ItemCode, out var t) ? t : 0m;
                    return filter.MovementType == "Fast" ? total > threshold : total <= threshold;
                }).ToList();
            }

            vm.Rows = rawRows.OrderBy(r => r.ItemName).ThenBy(r => r.Batch).ToList();
            vm.NearExpiryCount = nearExpiryRows.Count;
            vm.NearExpiryItems = nearExpiryRows;

            var topProductCodes = rawRows
                .GroupBy(r => r.ItemCode)
                .Select(g => new { ItemCode = g.Key, TotalQty = g.Sum(r => r.QtySold) })
                .OrderByDescending(x => x.TotalQty)
                .Take(20)
                .Select(x => x.ItemCode)
                .ToHashSet();

            vm.Top20 = rawRows.Where(r => topProductCodes.Contains(r.ItemCode)).ToList();

            var allStockWithStock = await _db.ProductStockView
                .Where(s => s.TenantId == tenantId && (s.AvailableQty ?? 0) > 0)
                .OrderByDescending(s => s.AvailableQty)
                .Take(2000)
                .ToListAsync(ct);

            var soldKeys = new HashSet<string>(groupedRaw.Select(g =>
                $"{g.ProductID}|{g.Batch}|{g.ExpiryDate?.ToString("yyyy-MM-dd") ?? ""}"));

            var deadStockItems = allStockWithStock
                .Where(s => !soldKeys.Contains($"{s.ProductID}|{s.BatchNumber ?? ""}|{s.ExpiryDate?.ToString("yyyy-MM-dd") ?? ""}"))
                .Select(s => new DeadStockViewModel
                {
                    ItemCode = s.ProductID ?? "",
                    ItemName = s.ProductName ?? "",
                    Manufacturer = s.ManufacturerName ?? "",
                    Batch = s.BatchNumber ?? "",
                    Expiry = s.ExpiryDate?.ToString("MMM-yyyy") ?? "",
                    CurrentStock = s.AvailableQty ?? 0
                })
                .ToList();

            vm.DeadStockItems = deadStockItems;

            var productSaleValues = rawRows
                .GroupBy(r => r.ItemCode)
                .Select(g => new { ItemCode = g.Key, TotalValue = g.Sum(r => r.SaleValue) })
                .OrderByDescending(x => x.TotalValue)
                .ToList();

            var grandTotal = productSaleValues.Sum(x => x.TotalValue);
            if (grandTotal > 0)
            {
                decimal cumulative = 0;
                int aCount = 0, bCount = 0, cCount = 0;
                foreach (var item in productSaleValues)
                {
                    cumulative += item.TotalValue;
                    var pct = cumulative / grandTotal * 100;
                    if (pct <= 80m) aCount++;
                    else if (pct <= 95m) bCount++;
                    else cCount++;
                }
                vm.AbcA = aCount;
                vm.AbcB = bCount;
                vm.AbcC = cCount;
            }

            return vm;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, DateTime? from, DateTime? to, string? q, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            await _saleService.CancelSaleAsync(id);
            return RedirectToAction(nameof(History), new { from, to, q });
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var sale = await _saleService.GetByIdAsync(id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        [HttpGet]
        public async Task<IActionResult> Pdf(int id, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var sale = await _saleService.GetByIdAsync(id);
            if (sale == null) return NotFound();

            var doc = new InvoicePdfDocument(sale);
            var bytes = doc.GeneratePdf();
            var invoice = string.IsNullOrWhiteSpace(sale.InvoiceNumber) ? $"{sale.Id}" : sale.InvoiceNumber;
            return File(bytes, "application/pdf", $"Invoice-{invoice}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> GetSaleForEdit(int id, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var sale = await _saleService.GetByIdAsync(id);
            if (sale == null) return NotFound();

            var paymentMethod = "Cash";
            if (sale.Payments != null && sale.Payments.Count > 1) paymentMethod = "Split";
            else if (sale.Payments != null && sale.Payments.Count == 1) paymentMethod = sale.Payments.First().PaymentMode;

            string? cardRef = null;
            string? upiRef = null;
            decimal cashReceived = 0;
            decimal cardAmount = 0;
            decimal upiAmount = 0;
            decimal splitCash = 0;
            decimal splitCard = 0;
            decimal splitUpi = 0;
            string? splitCardRefNo = null;
            string? splitUpiRefNo = null;

            foreach (var p in sale.Payments ?? Array.Empty<Rxnxt.Domain.Models.Payment>())
            {
                var mode = (p.PaymentMode ?? string.Empty).Trim();
                if (mode.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                {
                    cashReceived = p.Amount;
                    splitCash += p.Amount;
                }
                else if (mode.Equals("Card", StringComparison.OrdinalIgnoreCase))
                {
                    cardAmount = p.Amount;
                    splitCard += p.Amount;
                    cardRef = p.Reference;
                    splitCardRefNo = p.Reference;
                }
                else if (mode.Equals("UPI", StringComparison.OrdinalIgnoreCase))
                {
                    upiAmount = p.Amount;
                    splitUpi += p.Amount;
                    upiRef = p.Reference;
                    splitUpiRefNo = p.Reference;
                }
            }

            var payload = new
            {
                saleId = sale.Id,
                invoiceNumber = sale.InvoiceNumber,
                customer = sale.Customer == null ? null : new { id = sale.Customer.Id, name = sale.Customer.Name, phone = sale.Customer.Phone },
                items = (sale.SaleItems ?? Array.Empty<Rxnxt.Domain.Models.SaleItem>()).Select(i => new
                {
                    productId = i.ProductId,
                    productName = i.ProductName,
                    batchNumber = i.BatchNumber,
                    expiryDate = i.ExpiryDate,
                    uomName = i.UomName,
                    quantity = i.Quantity,
                    unitType = i.UnitType,
                    price = i.Price,
                    discountPercent = i.DiscountPercent,
                    discountAmount = i.DiscountAmount,
                    taxPercent = i.TaxPercent,
                    taxAmount = i.TaxAmount,
                    total = i.Total
                }).ToList(),
                additionalDiscount = sale.AdditionalDiscount,
                payment = new
                {
                    method = paymentMethod,
                    cashAmount = sale.GrandTotal,
                    cashReceived = cashReceived,
                    cardAmount = cardAmount,
                    cardRefNo = cardRef,
                    upiAmount = upiAmount,
                    upiRefNo = upiRef,
                    splitCash = splitCash,
                    splitCard = splitCard,
                    splitUpi = splitUpi,
                    splitCardRefNo = splitCardRefNo,
                    splitUpiRefNo = splitUpiRefNo
                }
            };

            return Json(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteSale(SaleSubmitViewModel model, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(model.SaleJson))
            {
                TempData["SaleError"] = "No items in the sale";
                return RedirectToAction(nameof(Index));
            }

            CompleteSaleRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<CompleteSaleRequest>(model.SaleJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                TempData["SaleError"] = "Invalid sale payload";
                return RedirectToAction(nameof(Index));
            }

            if (request?.Items == null || request.Items.Count == 0)
            {
                TempData["SaleError"] = "No items in the sale";
                return RedirectToAction(nameof(Index));
            }

            var result = await _saleService.CompleteSaleAsync(request);
            if (!result.Success)
            {
                TempData["SaleError"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            TempData["SaleSuccessInvoice"] = result.InvoiceNumber ?? string.Empty;
            TempData["SaleSuccessId"] = result.SaleId?.ToString() ?? string.Empty;
            return RedirectToAction(nameof(Index));
        }

    }
}
