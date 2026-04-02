using Microsoft.AspNetCore.Mvc;
using Rxnxt.Web.ViewModels;
using Rxnxt.Services.Implementations;
using Rxnxt.Business.DTOs;
using System;
using System.Linq;
using System.Text.Json;

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

        public IActionResult Index()
        {
            return View();
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
            return RedirectToAction(nameof(Index));
        }

    }
}
