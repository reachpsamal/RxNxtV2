using Microsoft.AspNetCore.Mvc;
using Rxnxt.Web.ViewModels;
using Rxnxt.Services.Implementations;
using Rxnxt.Business.DTOs;
using System;
using System.Linq;
using System.Text.Json;
using System.Globalization;

namespace Rxnxt.Web.Controllers
{
    public class SalesController : Controller
    {
        private readonly CustomerService _customerService;
        private readonly SaleService _saleService;
        private readonly StockService _stockService;

        public SalesController(CustomerService customerService, SaleService saleService, StockService stockService)
        {
            _customerService = customerService;
            _saleService = saleService;
            _stockService = stockService;
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
