using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;
using Rxnxt.Services.Implementations;

namespace Rxnxt.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ICustomerRepository _customerRepo;
        private readonly ISaleRepository _saleRepo;
        private readonly ISupplierRepository _supplierRepo;
        private readonly IPurchaseRepository _purchaseRepo;
        private readonly StockService _stockService;
        private readonly PharmacyDbContext _db;

        public ApiController(
            ICustomerRepository customerRepo,
            ISaleRepository saleRepo,
            ISupplierRepository supplierRepo,
            IPurchaseRepository purchaseRepo,
            StockService stockService,
            PharmacyDbContext db)
        {
            _customerRepo = customerRepo;
            _saleRepo = saleRepo;
            _supplierRepo = supplierRepo;
            _purchaseRepo = purchaseRepo;
            _stockService = stockService;
            _db = db;
        }

        // ==================== CUSTOMER ENDPOINTS ====================

        [HttpGet("customers/search")]
        public async Task<IActionResult> SearchCustomers([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<CustomerSearchResult>());

            var results = await _customerRepo.SearchAsync(q);
            return Ok(results);
        }

        // ==================== PRODUCT MASTER ENDPOINTS ====================

        [HttpGet("products/search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<object>());

            var term = q.Trim();
            var termLower = term.ToLowerInvariant();

            var rows = await _db.ProductMasters
                .AsNoTracking()
                .Where(p => p.ProductName != null && p.ProductName.ToLower().Contains(termLower))
                .OrderBy(p => p.ProductName)
                .Take(20)
                .ToListAsync();

            var results = rows
                .Select(p =>
                {
                    var ok = Guid.TryParse(p.UniqueID, out var guid);
                    if (!ok) return null;
                    return new { productId = guid, productName = p.ProductName ?? string.Empty };
                })
                .Where(x => x != null)
                .ToList();

            return Ok(results);
        }

        // ==================== PRODUCT STOCK ENDPOINTS ====================

        [HttpGet("product-stocks/batches")]
        public async Task<IActionResult> GetProductStockBatches([FromQuery] Guid productId, [FromQuery] string? q)
        {
            if (productId == Guid.Empty)
                return BadRequest(new { message = "productId is required" });

            var productIdString = productId.ToString();
            var term = (q ?? string.Empty).Trim();

            var query = _db.ProductStocks
                .AsNoTracking()
                .Where(s => s.ProductID == productIdString);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var termLower = term.ToLowerInvariant();
                query = query.Where(s => s.BatchNumber != null && s.BatchNumber.ToLower().Contains(termLower));
            }

            var results = await query
                .OrderBy(s => s.BatchNumber)
                .ThenBy(s => s.ExpiryDate)
                .Select(s => new
                {
                    batchNumber = s.BatchNumber ?? string.Empty,
                    expiryDate = s.ExpiryDate,
                    availableQty = s.PackQty ?? 0m
                })
                .Take(20)
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("customers/{id}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            var customer = await _customerRepo.GetByIdAsync(id);
            if (customer == null) return NotFound();
            return Ok(new CustomerSearchResult
            {
                Id = customer.Id,
                Name = customer.Name,
                Phone = customer.Phone,
                Email = customer.Email,
                LoyaltyPoints = customer.LoyaltyPoints
            });
        }

        // ==================== SUPPLIER ENDPOINTS ====================

        [HttpGet("suppliers/search")]
        public async Task<IActionResult> SearchSuppliers([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<SupplierSearchResult>());

            var results = await _supplierRepo.SearchAsync(q);
            return Ok(results);
        }

        [HttpPost("suppliers")]
        public async Task<IActionResult> CreateSupplier([FromBody] Supplier supplier)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _supplierRepo.CreateAsync(supplier);
            return Ok(new SupplierSearchResult
            {
                Id = created.Id,
                Name = created.Name,
                Phone = created.Phone,
                Gstin = created.Gstin,
                Address = created.Address
            });
        }

        [HttpPost("customers")]
        public async Task<IActionResult> CreateCustomer([FromBody] Customer customer)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _customerRepo.PhoneExistsAsync(customer.Phone))
                return BadRequest(new { message = "Phone number already exists" });

            var created = await _customerRepo.CreateAsync(customer);
            return Ok(new CustomerSearchResult
            {
                Id = created.Id,
                Name = created.Name,
                Phone = created.Phone,
                Email = created.Email,
                LoyaltyPoints = created.LoyaltyPoints
            });
        }

        // ==================== MEDICINE ENDPOINTS ====================

        [HttpGet("medicines/search")]
        public async Task<IActionResult> SearchMedicines([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<StockSearchResult>());

            var stocks = await _stockService.GetStocksAsync();
            var query = q.Trim();
            var results = stocks
                .Where(s => !string.IsNullOrWhiteSpace(s.ProductName) && s.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.ProductName)
                .ThenBy(s => s.BatchNumber)
                .Take(20)
                .Select(MapToStockSearchResult)
                .ToList();

            return Ok(results);
        }

        // ==================== BATCH ENDPOINTS ====================

        [HttpGet("batches/search")]
        public async Task<IActionResult> SearchBatches([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<StockSearchResult>());

            var stocks = await _stockService.GetStocksAsync();
            var query = q.Trim();
            var results = stocks
                .Where(s => !string.IsNullOrWhiteSpace(s.BatchNumber) && s.BatchNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.BatchNumber)
                .Take(20)
                .Select(MapToStockSearchResult)
                .ToList();

            return Ok(results);
        }

        [HttpGet("stocks/by-batch")]
        public async Task<IActionResult> GetStockByBatch([FromQuery] string batchNumber)
        {
            if (string.IsNullOrWhiteSpace(batchNumber)) return BadRequest();
            var stocks = await _stockService.GetStocksAsync();
            var match = stocks.FirstOrDefault(s => string.Equals(s.BatchNumber, batchNumber, StringComparison.OrdinalIgnoreCase));
            if (match == null) return NotFound();
            return Ok(MapToStockSearchResult(match));
        }

        [HttpGet("stocks/by-product-batch")]
        public async Task<IActionResult> GetStockByProductBatch([FromQuery] Guid productId, [FromQuery] string batchNumber)
        {
            if (productId == Guid.Empty || string.IsNullOrWhiteSpace(batchNumber)) return BadRequest();
            var stocks = await _stockService.GetStocksAsync();
            var match = stocks.FirstOrDefault(s => s.ProductId == productId && string.Equals(s.BatchNumber, batchNumber, StringComparison.OrdinalIgnoreCase));
            if (match == null) return NotFound();
            return Ok(MapToStockSearchResult(match));
        }

        [HttpGet("batches/advanced-search")]
        public async Task<IActionResult> AdvancedBatchSearch(
            [FromQuery] string? batchNumber,
            [FromQuery] string? medicineName,
            [FromQuery] string? composition,
            [FromQuery] DateTime? expiryFrom,
            [FromQuery] DateTime? expiryTo)
        {
            var stocks = await _stockService.GetStocksAsync();
            IEnumerable<StockDto> query = stocks;

            if (!string.IsNullOrWhiteSpace(batchNumber))
                query = query.Where(s => s.BatchNumber.Contains(batchNumber, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(medicineName))
                query = query.Where(s => s.ProductName.Contains(medicineName, StringComparison.OrdinalIgnoreCase));

            if (expiryFrom.HasValue)
                query = query.Where(s => s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date >= expiryFrom.Value.Date);

            if (expiryTo.HasValue)
                query = query.Where(s => s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date <= expiryTo.Value.Date);

            var results = query
                .OrderBy(s => s.ProductName)
                .ThenBy(s => s.BatchNumber)
                .Take(50)
                .Select(MapToStockSearchResult)
                .ToList();

            return Ok(results);
        }

        private static StockSearchResult MapToStockSearchResult(StockDto stock)
        {
            var today = DateTime.Today;
            var expiryDate = stock.ExpiryDate?.Date;
            var isExpired = expiryDate.HasValue && expiryDate.Value < today;
            var isNearExpiry = expiryDate.HasValue && !isExpired && expiryDate.Value <= today.AddDays(90);

            return new StockSearchResult
            {
                ProductId = stock.ProductId,
                ProductName = stock.ProductName,
                Manufacturer = stock.Manufacturer,
                TaxName = stock.TaxName,
                BatchNumber = stock.BatchNumber,
                ExpiryDate = stock.ExpiryDate,
                Mrp = stock.Mrp,
                AvailableQty = stock.AvailableQty,
                UomName = stock.UomName,
                IsExpired = isExpired,
                IsNearExpiry = isNearExpiry
            };
        }

        // ==================== SALE ENDPOINTS ====================

        [HttpPost("sales/complete")]
        public async Task<IActionResult> CompleteSale([FromBody] CompleteSaleRequest request)
        {
            if (request.Items == null || !request.Items.Any())
                return BadRequest(new SaleResult { Success = false, Message = "No items in the sale" });

            var result = await _saleRepo.CompleteSaleAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ==================== PURCHASE ENDPOINTS ====================

        [HttpPost("purchases/complete")]
        public async Task<IActionResult> CompletePurchase([FromBody] CompletePurchaseRequest request)
        {
            if (request.Items == null || !request.Items.Any())
                return BadRequest(new PurchaseResult { Success = false, Message = "No items in the purchase" });

            var result = await _purchaseRepo.CompletePurchaseAsync(request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("sales/{id}")]
        public async Task<IActionResult> GetSale(int id)
        {
            var sale = await _saleRepo.GetByIdAsync(id);
            if (sale == null) return NotFound();
            return Ok(sale);
        }

        [HttpGet("sales/recent")]
        public async Task<IActionResult> GetRecentSales([FromQuery] int count = 10)
        {
            var sales = await _saleRepo.GetRecentSalesAsync(count);
            return Ok(sales);
        }
    }
}
