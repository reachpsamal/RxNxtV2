using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Rxnxt.Business.Data;
using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;

namespace Rxnxt.Business.Implementations
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly PharmacyDbContext _context;
        public CustomerRepository(PharmacyDbContext context) => _context = context;

        public async Task<List<CustomerSearchResult>> SearchAsync(string query)
        {
            query = query.Trim().ToLower();
            return await _context.Customers
                .Where(c => c.Name.ToLower().Contains(query)
                          || c.Phone.Contains(query)
                          || (c.Email != null && c.Email.ToLower().Contains(query)))
                .Select(c => new CustomerSearchResult
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Email = c.Email,
                    LoyaltyPoints = c.LoyaltyPoints
                })
                .Take(10)
                .ToListAsync();
        }

        public async Task<Customer?> GetByIdAsync(int id) =>
            await _context.Customers.FindAsync(id);

        public async Task<Customer> CreateAsync(Customer customer)
        {
            customer.CreatedDate = DateTime.Now;
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<bool> PhoneExistsAsync(string phone) =>
            await _context.Customers.AnyAsync(c => c.Phone == phone);
    }

    public class MedicineRepository : IMedicineRepository
    {
        private readonly PharmacyDbContext _context;
        public MedicineRepository(PharmacyDbContext context) => _context = context;

        public async Task<List<MedicineSearchResult>> SearchAsync(string query)
        {
            query = query.Trim().ToLower();
            return await _context.Medicines
                .Where(m => m.Name.ToLower().Contains(query)
                          || (m.GenericName != null && m.GenericName.ToLower().Contains(query))
                          || (m.Manufacturer != null && m.Manufacturer.ToLower().Contains(query)))
                .Include(m => m.Batches)
                .Select(m => new MedicineSearchResult
                {
                    Id = m.Id,
                    Name = m.Name,
                    GenericName = m.GenericName,
                    Manufacturer = m.Manufacturer,
                    Category = m.Category,
                    Batches = m.Batches.Where(b => b.StripQuantity > 0 && b.ExpiryDate > DateTime.Now)
                        .Select(b => new BatchSearchResult
                        {
                            Id = b.Id,
                            MedicineId = b.MedicineId,
                            MedicineName = m.Name,
                            GenericName = m.GenericName,
                            BatchNumber = b.BatchNumber,
                            ExpiryDate = b.ExpiryDate,
                            StripQuantity = b.StripQuantity,
                            TabletPerStrip = b.TabletPerStrip,
                            SellingPriceStrip = b.SellingPriceStrip,
                            SellingPriceTablet = b.SellingPriceTablet,
                            Manufacturer = m.Manufacturer,
                            IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),
                            IsExpired = b.ExpiryDate <= DateTime.Now,
                            TotalTablets = b.StripQuantity * b.TabletPerStrip
                        }).ToList()
                })
                .Take(10)
                .ToListAsync();
        }

        public async Task<Medicine?> GetByIdAsync(int id) =>
            await _context.Medicines.Include(m => m.Batches).FirstOrDefaultAsync(m => m.Id == id);
    }

    public class BatchRepository : IBatchRepository
    {
        private readonly PharmacyDbContext _context;
        public BatchRepository(PharmacyDbContext context) => _context = context;

        public async Task<List<BatchSearchResult>> SearchByBatchNumberAsync(string batchNumber)
        {
            batchNumber = batchNumber.Trim().ToLower();
            return await _context.Batches
                .Include(b => b.Medicine)
                .Where(b => b.BatchNumber.ToLower().Contains(batchNumber) && b.StripQuantity > 0)
                .Select(b => new BatchSearchResult
                {
                    Id = b.Id,
                    MedicineId = b.MedicineId,
                    MedicineName = b.Medicine!.Name,
                    GenericName = b.Medicine.GenericName,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    StripQuantity = b.StripQuantity,
                    TabletPerStrip = b.TabletPerStrip,
                    SellingPriceStrip = b.SellingPriceStrip,
                    SellingPriceTablet = b.SellingPriceTablet,
                    Manufacturer = b.Medicine.Manufacturer,
                    IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),
                    IsExpired = b.ExpiryDate <= DateTime.Now,
                    TotalTablets = b.StripQuantity * b.TabletPerStrip
                })
                .Take(10)
                .ToListAsync();
        }

        public async Task<List<BatchSearchResult>> SearchByMedicineAsync(string query)
        {
            query = query.Trim().ToLower();
            return await _context.Batches
                .Include(b => b.Medicine)
                .Where(b => (b.Medicine!.Name.ToLower().Contains(query)
                          || (b.Medicine.GenericName != null && b.Medicine.GenericName.ToLower().Contains(query)))
                          && b.StripQuantity > 0
                          && b.ExpiryDate > DateTime.Now)
                .Select(b => new BatchSearchResult
                {
                    Id = b.Id,
                    MedicineId = b.MedicineId,
                    MedicineName = b.Medicine!.Name,
                    GenericName = b.Medicine.GenericName,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    StripQuantity = b.StripQuantity,
                    TabletPerStrip = b.TabletPerStrip,
                    SellingPriceStrip = b.SellingPriceStrip,
                    SellingPriceTablet = b.SellingPriceTablet,
                    Manufacturer = b.Medicine.Manufacturer,
                    IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),
                    IsExpired = b.ExpiryDate <= DateTime.Now,
                    TotalTablets = b.StripQuantity * b.TabletPerStrip
                })
                .Take(15)
                .ToListAsync();
        }

        public async Task<List<BatchSearchResult>> GetBatchesByMedicineIdAsync(int medicineId)
        {
            return await _context.Batches
                .Include(b => b.Medicine)
                .Where(b => b.MedicineId == medicineId && b.StripQuantity > 0 && b.ExpiryDate > DateTime.Now)
                .Select(b => new BatchSearchResult
                {
                    Id = b.Id,
                    MedicineId = b.MedicineId,
                    MedicineName = b.Medicine!.Name,
                    GenericName = b.Medicine.GenericName,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    StripQuantity = b.StripQuantity,
                    TabletPerStrip = b.TabletPerStrip,
                    SellingPriceStrip = b.SellingPriceStrip,
                    SellingPriceTablet = b.SellingPriceTablet,
                    Manufacturer = b.Medicine.Manufacturer,
                    IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),
                    IsExpired = b.ExpiryDate <= DateTime.Now,
                    TotalTablets = b.StripQuantity * b.TabletPerStrip
                })
                .ToListAsync();
        }

        public async Task<BatchSearchResult?> GetByIdAsync(int id)
        {
            return await _context.Batches
                .Include(b => b.Medicine)
                .Where(b => b.Id == id)
                .Select(b => new BatchSearchResult
                {
                    Id = b.Id,
                    MedicineId = b.MedicineId,
                    MedicineName = b.Medicine!.Name,
                    GenericName = b.Medicine.GenericName,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    StripQuantity = b.StripQuantity,
                    TabletPerStrip = b.TabletPerStrip,
                    SellingPriceStrip = b.SellingPriceStrip,
                    SellingPriceTablet = b.SellingPriceTablet,
                    Manufacturer = b.Medicine.Manufacturer,
                    IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),
                    IsExpired = b.ExpiryDate <= DateTime.Now,
                    TotalTablets = b.StripQuantity * b.TabletPerStrip
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<BatchSearchResult>> AdvancedSearchAsync(string? batchNumber, string? medicineName, string? composition, DateTime? expiryFrom, DateTime? expiryTo)
        {
            var query = _context.Batches.Include(b => b.Medicine).Where(b => b.StripQuantity > 0);

            if (!string.IsNullOrWhiteSpace(batchNumber))
                query = query.Where(b => b.BatchNumber.ToLower().Contains(batchNumber.ToLower()));

            if (!string.IsNullOrWhiteSpace(medicineName))
                query = query.Where(b => b.Medicine!.Name.ToLower().Contains(medicineName.ToLower()));

            if (!string.IsNullOrWhiteSpace(composition))
                query = query.Where(b => b.Medicine!.GenericName != null && b.Medicine.GenericName.ToLower().Contains(composition.ToLower()));

            if (expiryFrom.HasValue)
                query = query.Where(b => b.ExpiryDate >= expiryFrom.Value);

            if (expiryTo.HasValue)
                query = query.Where(b => b.ExpiryDate <= expiryTo.Value);

            return await query.Select(b => new BatchSearchResult
            {
                Id = b.Id,
                MedicineId = b.MedicineId,
                MedicineName = b.Medicine!.Name,
                GenericName = b.Medicine.GenericName,
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                StripQuantity = b.StripQuantity,
                TabletPerStrip = b.TabletPerStrip,
                SellingPriceStrip = b.SellingPriceStrip,
                SellingPriceTablet = b.SellingPriceTablet,
                Manufacturer = b.Medicine.Manufacturer,
                IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),
                IsExpired = b.ExpiryDate <= DateTime.Now,
                TotalTablets = b.StripQuantity * b.TabletPerStrip
            })
            .ToListAsync();
        }
    }

    public class SaleRepository : ISaleRepository
    {
        private readonly PharmacyDbContext _context;
        private readonly IConfiguration _configuration;
        public SaleRepository(PharmacyDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<SaleResult> CompleteSaleAsync(CompleteSaleRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal subTotal = 0;
                decimal totalItemDiscount = 0;
                decimal totalTax = 0;
                var saleItems = new List<SaleItem>();

                foreach (var item in request.Items)
                {
                    if (item.Quantity <= 0)
                        return new SaleResult { Success = false, Message = "Invalid item quantity" };

                    if (item.UnitPrice <= 0)
                        return new SaleResult { Success = false, Message = "Invalid item unit price" };

                    decimal unitPrice = item.UnitPrice;
                    decimal lineTotal = unitPrice * item.Quantity;
                    decimal discountAmt = lineTotal * (item.DiscountPercent / 100);
                    decimal taxable = lineTotal - discountAmt;
                    decimal taxAmt = taxable * (item.TaxPercent / 100);
                    decimal total = taxable + taxAmt;

                    subTotal += lineTotal;
                    totalItemDiscount += discountAmt;
                    totalTax += taxAmt;

                    saleItems.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        BatchNumber = item.BatchNumber,
                        ExpiryDate = item.ExpiryDate,
                        UomName = item.UomName,
                        Quantity = item.Quantity,
                        UnitType = item.UnitType,
                        Price = unitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountAmount = discountAmt,
                        TaxPercent = item.TaxPercent,
                        TaxAmount = taxAmt,
                        Total = total
                    });
                }

                decimal grandTotal = subTotal - totalItemDiscount - request.AdditionalDiscount + totalTax;

                var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
                if (salesIntegrationEnabled)
                {
                    var tenantId = _configuration["SalesIntegration:TenantId"] ?? string.Empty;
                    var storeId = _configuration["SalesIntegration:StoreId"] ?? string.Empty;
                    var createdBy = _configuration["SalesIntegration:CreatedBy"] ?? "POS";
                    var billType = _configuration["SalesIntegration:BillType"] ?? "Sale";

                    var now = DateTime.Now;
                    var headerUniqueId = Guid.NewGuid().ToString();
                    var customerIdStr = request.CustomerId?.ToString() ?? "0";
                    var amountBeforeTax = subTotal - totalItemDiscount - request.AdditionalDiscount;
                    var discountAmount = totalItemDiscount + request.AdditionalDiscount;
                    var discountPerc = subTotal > 0 ? (discountAmount / subTotal) * 100 : 0;

                    var header = new SaleHeaderRow
                    {
                        UniqueID = headerUniqueId,
                        BillNo = "INV-0",
                        BillDate = now,
                        BillType = billType,
                        CustomerID = customerIdStr,
                        Narration = null,
                        BillAmount = grandTotal,
                        TaxAmount = totalTax,
                        DiscountAmount = discountAmount,
                        ExtraAdd = 0,
                        ExtraLess = 0,
                        ActiveStatus = true,
                        CreatedBy = createdBy,
                        CreatedDate = now,
                        ModifiedBy = null,
                        ModifiedDate = null,
                        TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                        DiscountPerc = discountPerc,
                        AmountBeforeTax = amountBeforeTax,
                        StoreId = string.IsNullOrWhiteSpace(storeId) ? null : storeId
                    };

                    _context.SaleHeaders.Add(header);
                    await _context.SaveChangesAsync();

                    header.BillNo = $"INV-{header.ID}";
                    await _context.SaveChangesAsync();

                    foreach (var item in request.Items)
                    {
                        decimal unitPrice = item.UnitPrice;
                        decimal lineTotal = unitPrice * item.Quantity;
                        decimal discountAmt = lineTotal * (item.DiscountPercent / 100);
                        decimal taxable = lineTotal - discountAmt;
                        decimal taxAmt = taxable * (item.TaxPercent / 100);
                        decimal total = taxable + taxAmt;

                        var halfTax = taxAmt / 2;

                        _context.SaleDetails.Add(new SaleDetailRow
                        {
                            UniqueID = Guid.NewGuid().ToString(),
                            SaleID = headerUniqueId,
                            ProductID = item.ProductId.ToString(),
                            BatchNumber = item.BatchNumber,
                            ExpiryDate = item.ExpiryDate,
                            UnitID = null,
                            PackTypeID = null,
                            MRP = unitPrice,
                            PurchasePrice = null,
                            SalePrice = unitPrice,
                            FreeQty = 0,
                            Remarks = null,
                            Qty = item.Quantity,
                            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                            BaseUOMID = null,
                            SaleUOMID = item.UomName,
                            SaleUOMQty = item.Quantity,
                            ItemDiscPerc = Math.Round(item.DiscountPercent, 0),
                            ItemDiscAmount = Math.Round(discountAmt, 0),
                            TaxableAmount = taxable,
                            CGSTAmount = halfTax,
                            SGSTAmount = halfTax,
                            IGSTAmount = 0,
                            TotalTaxAmount = taxAmt,
                            ItemTotal = total,
                            TaxPerc = item.TaxPercent
                        });
                    }

                    foreach (var payment in request.Payments)
                    {
                        _context.SalePayments.Add(new SalePaymentRow
                        {
                            PaymentId = Guid.NewGuid().ToString(),
                            SaleId = headerUniqueId,
                            PaymentMode = payment.PaymentMode,
                            Amount = payment.Amount,
                            ReferenceNo = payment.Reference,
                            PaymentDate = now
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new SaleResult
                    {
                        Success = true,
                        Message = "Sale completed successfully!",
                        SaleId = null,
                        InvoiceNumber = header.BillNo
                    };
                }

                return new SaleResult { Success = false, Message = "SalesIntegration is disabled but legacy Sales tables are not available." };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new SaleResult { Success = false, Message = $"Error completing sale: {ex.Message}" };
            }
        }

        public async Task<Sale?> GetByIdAsync(int id) =>
            string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
                ? await GetIntegrationSaleByIdAsync(id)
                : await _context.Sales
                    .Include(s => s.Customer)
                    .Include(s => s.SaleItems)
                    .Include(s => s.Payments)
                    .FirstOrDefaultAsync(s => s.Id == id);

        private async Task<Sale?> GetIntegrationSaleByIdAsync(int id)
        {
            var header = await _context.SaleHeaders.AsNoTracking().FirstOrDefaultAsync(h => h.ID == id);
            if (header == null) return null;

            var details = await _context.SaleDetails
                .AsNoTracking()
                .Where(d => d.SaleID == header.UniqueID)
                .ToListAsync();

            var productIds = details
                .Select(d => (d.ProductID ?? string.Empty).Trim())
                .Where(pid => !string.IsNullOrWhiteSpace(pid))
                .Distinct()
                .ToList();

            var productNameById = productIds.Count == 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : (await _context.ProductMasters
                        .AsNoTracking()
                        .Where(p => productIds.Contains(p.UniqueID))
                        .Select(p => new { p.UniqueID, p.ProductName })
                        .ToListAsync())
                    .Where(p => !string.IsNullOrWhiteSpace(p.UniqueID))
                    .GroupBy(p => p.UniqueID.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.ProductName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            var payments = await _context.SalePayments
                .AsNoTracking()
                .Where(p => p.SaleId == header.UniqueID)
                .ToListAsync();

            var sale = new Sale
            {
                Id = header.ID,
                CustomerId = null,
                Customer = null,
                SaleDate = header.BillDate,
                SubTotal = header.AmountBeforeTax ?? 0,
                ItemDiscount = header.DiscountAmount ?? 0,
                AdditionalDiscount = 0,
                GrandTotal = header.BillAmount ?? 0,
                PaymentStatus = header.ActiveStatus ? "Completed" : "Cancelled",
                InvoiceNumber = header.BillNo,
                SaleItems = new List<SaleItem>(),
                Payments = new List<Payment>()
            };

            foreach (var d in details)
            {
                var productId = Guid.Empty;
                _ = Guid.TryParse(d.ProductID, out productId);

                var pidKey = (d.ProductID ?? string.Empty).Trim();
                var productName = (!string.IsNullOrWhiteSpace(pidKey) && productNameById.TryGetValue(pidKey, out var pn) && !string.IsNullOrWhiteSpace(pn))
                    ? pn
                    : pidKey;

                sale.SaleItems.Add(new SaleItem
                {
                    Id = d.ID,
                    SaleId = sale.Id,
                    ProductId = productId,
                    ProductName = productName,
                    BatchNumber = d.BatchNumber ?? string.Empty,
                    ExpiryDate = d.ExpiryDate ?? DateTime.Today,
                    UomName = string.IsNullOrWhiteSpace(d.SaleUOMID) ? "PCS" : d.SaleUOMID,
                    Quantity = (int)Math.Round(d.Qty ?? 0, 0),
                    UnitType = string.IsNullOrWhiteSpace(d.SaleUOMID) ? "PCS" : d.SaleUOMID,
                    Price = d.SalePrice ?? d.MRP ?? 0,
                    DiscountPercent = d.ItemDiscPerc ?? 0,
                    DiscountAmount = d.ItemDiscAmount ?? 0,
                    TaxPercent = d.TaxPerc ?? 0,
                    TaxAmount = d.TotalTaxAmount ?? 0,
                    Total = d.ItemTotal ?? 0
                });
            }

            foreach (var p in payments)
            {
                sale.Payments.Add(new Payment
                {
                    Id = 0,
                    SaleId = sale.Id,
                    PaymentMode = p.PaymentMode,
                    Amount = p.Amount,
                    Reference = p.ReferenceNo,
                    Status = "Completed",
                    PaymentDate = p.PaymentDate
                });
            }

            return sale;
        }

        public async Task<List<Sale>> GetRecentSalesAsync(int count = 10) =>
            await _context.Sales
                .Include(s => s.Customer)
                .OrderByDescending(s => s.SaleDate)
                .Take(count)
                .ToListAsync();

        public async Task<List<Sale>> SearchSalesAsync(DateTime from, DateTime to, string? q)
        {
            var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (salesIntegrationEnabled)
            {
                var fromDt = from;
                var toDt = to;

                var term = (q ?? string.Empty).Trim();

                var headerQuery = _context.SaleHeaders
                    .AsNoTracking()
                    .Where(h => h.BillDate >= fromDt && h.BillDate <= toDt);

                headerQuery = headerQuery.Where(h => h.ActiveStatus);

                if (!string.IsNullOrWhiteSpace(term))
                {
                    headerQuery = headerQuery.Where(h =>
                        (h.BillNo != null && h.BillNo.Contains(term)) ||
                        (h.CustomerID != null && h.CustomerID.Contains(term))
                    );
                }

                var headers = await headerQuery
                    .OrderByDescending(h => h.BillDate)
                    .ToListAsync();

                var sales = headers.Select(h =>
                {
                    return new Sale
                    {
                        Id = h.ID,
                        SaleDate = h.BillDate,
                        InvoiceNumber = h.BillNo,
                        CustomerId = null,
                        Customer = null,
                        SubTotal = h.AmountBeforeTax ?? 0,
                        ItemDiscount = h.DiscountAmount ?? 0,
                        AdditionalDiscount = 0,
                        GrandTotal = h.BillAmount ?? 0,
                        PaymentStatus = h.ActiveStatus ? "Completed" : "Cancelled",
                        SaleItems = new List<SaleItem>(),
                        Payments = new List<Payment>()
                    };
                }).ToList();

                if (!string.IsNullOrWhiteSpace(term))
                {
                    sales = sales.Where(s =>
                        (s.InvoiceNumber != null && s.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (s.Customer != null && s.Customer.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (s.Customer != null && s.Customer.Phone.Contains(term, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }

                return sales;
            }

            {
                var fromDt = from;
                var toDt = to;

                var query = _context.Sales
                    .AsNoTracking()
                    .Include(s => s.Customer)
                    .Where(s => s.SaleDate >= fromDt && s.SaleDate <= toDt)
                    .Where(s => s.PaymentStatus != "Cancelled");

                var term = (q ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(term))
                {
                    query = query.Where(s =>
                        (s.InvoiceNumber != null && s.InvoiceNumber.Contains(term)) ||
                        (s.Customer != null && s.Customer.Name.Contains(term)) ||
                        (s.Customer != null && s.Customer.Phone.Contains(term))
                    );
                }

                return await query
                    .OrderByDescending(s => s.SaleDate)
                    .ToListAsync();
            }
        }

        public async Task<bool> CancelSaleAsync(int id)
        {
            var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (salesIntegrationEnabled)
            {
                var header = await _context.SaleHeaders.FirstOrDefaultAsync(h => h.ID == id);
                if (header == null) return false;
                header.ActiveStatus = false;
                await _context.SaveChangesAsync();
                return true;
            }

            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) return false;
            sale.PaymentStatus = "Cancelled";
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
