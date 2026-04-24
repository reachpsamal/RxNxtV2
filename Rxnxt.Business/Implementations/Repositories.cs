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
        private readonly IConfiguration _configuration;
        public CustomerRepository(PharmacyDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<List<CustomerSearchResult>> SearchAsync(string query)
        {
            var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (salesIntegrationEnabled)
            {
                var q = query.Trim();
                var term = q.ToLowerInvariant();
                return await _context.CustomerMasters
                    .AsNoTracking()
                    .Where(c => c.ActiveStatus)
                    .Where(c =>
                        c.CustomerName.ToLower().Contains(term) ||
                        (c.MobileNumber != null && c.MobileNumber.Contains(q)))
                    .OrderBy(c => c.CustomerName)
                    .Take(10)
                    .Select(c => new CustomerSearchResult
                    {
                        Id = c.ID,
                        Name = c.CustomerName,
                        Phone = c.MobileNumber ?? string.Empty,
                        Email = null,
                        LoyaltyPoints = 0
                    })
                    .ToListAsync();
            }

            {
                var term = query.Trim().ToLowerInvariant();
                return await _context.Customers
                    .AsNoTracking()
                    .Where(c => c.Name.ToLower().Contains(term)
                              || c.Phone.Contains(term)
                              || (c.Email != null && c.Email.ToLower().Contains(term)))
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
        }

        public async Task<Customer?> GetByIdAsync(int id)
        {
            var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (salesIntegrationEnabled)
            {
                var row = await _context.CustomerMasters.AsNoTracking().FirstOrDefaultAsync(c => c.ID == id);
                if (row == null) return null;
                return new Customer
                {
                    Id = row.ID,
                    Name = row.CustomerName,
                    Phone = row.MobileNumber ?? string.Empty,
                    Email = null,
                    LoyaltyPoints = 0
                };
            }

            return await _context.Customers.FindAsync(id);
        }

        public async Task<Customer> CreateAsync(Customer customer)
        {
            var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (salesIntegrationEnabled)
            {
                var tenantId = _configuration["SalesIntegration:TenantId"] ?? string.Empty;
                var createdBy = _configuration["SalesIntegration:CreatedBy"] ?? "POS";
                var now = DateTime.Now;

                var name = (customer.Name ?? string.Empty).Trim();
                if (name.Length > 300) name = name[..300];
                var phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();

                var row = new CustomerMasterRow
                {
                    UniqueID = Guid.NewGuid().ToString(),
                    CustomerCode = "CUST-0",
                    CustomerName = name,
                    MobileNumber = phone,
                    ActiveStatus = true,
                    CreatedBy = createdBy,
                    CreatedDate = now,
                    ModifiedBy = null,
                    ModifiedDate = null,
                    TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId
                };

                _context.CustomerMasters.Add(row);
                await _context.SaveChangesAsync();
                row.CustomerCode = $"CUST-{row.ID}";
                await _context.SaveChangesAsync();

                customer.Id = row.ID;
                customer.Phone = row.MobileNumber ?? string.Empty;
                customer.CreatedDate = now;
                return customer;
            }

            customer.CreatedDate = DateTime.Now;
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<bool> PhoneExistsAsync(string phone)
        {
            var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (salesIntegrationEnabled)
            {
                var p = phone.Trim();
                return await _context.CustomerMasters.AsNoTracking().AnyAsync(c => c.MobileNumber == p);
            }

            return await _context.Customers.AnyAsync(c => c.Phone == phone);
        }
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

                var lineBases = new List<(SaleItemRequest Item, decimal Gross, decimal ItemDisc, decimal AfterItemDisc)>();

                foreach (var item in request.Items)
                {
                    if (item.Quantity <= 0)
                        return new SaleResult { Success = false, Message = "Invalid item quantity" };

                    if (item.UnitPrice <= 0)
                        return new SaleResult { Success = false, Message = "Invalid item unit price" };

                    decimal unitPrice = item.UnitPrice;
                    decimal lineTotal = unitPrice * item.Quantity;
                    decimal discountAmt = lineTotal * (item.DiscountPercent / 100);
                    decimal afterItemDisc = Math.Max(0, lineTotal - discountAmt);
                    decimal includedTax = item.TaxPercent > 0 ? (afterItemDisc * (item.TaxPercent / (100 + item.TaxPercent))) : 0;
                    decimal total = afterItemDisc;

                    subTotal += lineTotal;
                    totalItemDiscount += discountAmt;
                    totalTax += includedTax;

                    lineBases.Add((item, lineTotal, discountAmt, afterItemDisc));

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
                        TaxAmount = includedTax,
                        Total = total
                    });
                }

                var baseSum = lineBases.Sum(x => x.AfterItemDisc);
                var additionalDiscount = request.AdditionalDiscount;
                if (additionalDiscount < 0) additionalDiscount = 0;

                decimal grandTotal = 0;
                decimal totalTaxAfterAllDiscounts = 0;

                for (var i = 0; i < saleItems.Count; i++)
                {
                    var baseAfterItemDisc = lineBases[i].AfterItemDisc;
                    var share = (additionalDiscount > 0 && baseSum > 0) ? (additionalDiscount * (baseAfterItemDisc / baseSum)) : 0;
                    var afterAllDiscounts = Math.Max(0, baseAfterItemDisc - share);
                    var gst = saleItems[i].TaxPercent;
                    var includedTax = gst > 0 ? (afterAllDiscounts * (gst / (100 + gst))) : 0;
                    saleItems[i].TaxAmount = includedTax;
                    saleItems[i].Total = afterAllDiscounts;
                    totalTaxAfterAllDiscounts += includedTax;
                    grandTotal += afterAllDiscounts;
                }

                totalTax = totalTaxAfterAllDiscounts;

                var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
                if (salesIntegrationEnabled)
                {
                    var tenantId = _configuration["SalesIntegration:TenantId"] ?? string.Empty;
                    var storeId = _configuration["SalesIntegration:StoreId"] ?? string.Empty;
                    var createdBy = _configuration["SalesIntegration:CreatedBy"] ?? "POS";
                    var billType = _configuration["SalesIntegration:BillType"] ?? "Sale";

                    var now = DateTime.Now;

                    static string NormalizeBatch(string? batchNumber) => (batchNumber ?? string.Empty).Trim();

                    async Task<ProductStockRow?> FindStockAsync(string productId, string? batchNumber, DateTime? expiryDate)
                    {
                        var batchNorm = NormalizeBatch(batchNumber);
                        if (!expiryDate.HasValue) return null;
                        var exp = expiryDate.Value.Date;

                        return await _context.ProductStocks.FirstOrDefaultAsync(ps =>
                            ps.ProductID == productId &&
                            (ps.BatchNumber ?? string.Empty) == batchNorm &&
                            ps.ExpiryDate.HasValue &&
                            EF.Functions.DateDiffDay(ps.ExpiryDate.Value, exp) == 0);
                    }

                    SaleHeaderRow? existingHeader = null;
                    if (request.SaleId.HasValue && request.SaleId.Value > 0)
                    {
                        existingHeader = await _context.SaleHeaders.FirstOrDefaultAsync(h => h.ID == request.SaleId.Value);
                        if (existingHeader == null)
                        {
                            return new SaleResult { Success = false, Message = "Sale not found for update." };
                        }
                    }

                    var headerUniqueId = existingHeader?.UniqueID ?? Guid.NewGuid().ToString();

                    string customerIdStr;
                    if (request.CustomerId.HasValue && request.CustomerId.Value > 0)
                    {
                        customerIdStr = await _context.CustomerMasters
                            .AsNoTracking()
                            .Where(c => c.ID == request.CustomerId.Value)
                            .Select(c => c.UniqueID)
                            .FirstOrDefaultAsync() ?? "0";
                    }
                    else
                    {
                        customerIdStr = "0";
                    }

                    if ((string.IsNullOrWhiteSpace(customerIdStr) || customerIdStr == "0") && !string.IsNullOrWhiteSpace(request.CustomerPhone))
                    {
                        var phone = request.CustomerPhone.Trim();
                        customerIdStr = await _context.CustomerMasters
                            .AsNoTracking()
                            .Where(c => c.MobileNumber == phone)
                            .Select(c => c.UniqueID)
                            .FirstOrDefaultAsync() ?? "0";
                    }

                    if ((string.IsNullOrWhiteSpace(customerIdStr) || customerIdStr == "0") && !string.IsNullOrWhiteSpace(request.CustomerName))
                    {
                        var name = request.CustomerName.Trim();
                        customerIdStr = await _context.CustomerMasters
                            .AsNoTracking()
                            .Where(c => c.CustomerName == name)
                            .Select(c => c.UniqueID)
                            .FirstOrDefaultAsync() ?? "0";
                    }

                    if (string.IsNullOrWhiteSpace(customerIdStr) || customerIdStr == "0")
                    {
                        customerIdStr = await UpsertIntegrationCustomerAsync(request, createdBy, tenantId, now);
                    }

                    var amountBeforeTax = grandTotal - totalTax;
                    var discountAmount = totalItemDiscount + additionalDiscount;
                    var discountPerc = subTotal > 0 ? (discountAmount / subTotal) * 100 : 0;

                    SaleHeaderRow header;
                    if (existingHeader != null)
                    {
                        header = existingHeader;

                        var oldDetails = await _context.SaleDetails.Where(d => d.SaleID == headerUniqueId).ToListAsync();
                        foreach (var d in oldDetails)
                        {
                            var stockRow = await FindStockAsync(d.ProductID, d.BatchNumber, d.ExpiryDate);
                            if (stockRow == null)
                            {
                                return new SaleResult
                                {
                                    Success = false,
                                    Message = $"Stock not found to restore for ProductID {d.ProductID}, Batch {d.BatchNumber}, Exp {d.ExpiryDate:dd/MM/yyyy}" 
                                };
                            }

                            var qtyToRestore = d.Qty ?? 0m;
                            stockRow.PackQty = (stockRow.PackQty ?? 0m) + qtyToRestore;
                        }

                        header.BillType = billType;
                        header.CustomerID = customerIdStr;
                        header.Narration = null;
                        header.BillAmount = grandTotal;
                        header.TaxAmount = totalTax;
                        header.DiscountAmount = discountAmount;
                        header.ExtraAdd = 0;
                        header.ExtraLess = request.AdditionalDiscount;
                        header.ActiveStatus = true;
                        header.ModifiedBy = createdBy;
                        header.ModifiedDate = now;
                        header.TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
                        header.DiscountPerc = discountPerc;
                        header.AmountBeforeTax = amountBeforeTax;
                        header.StoreId = string.IsNullOrWhiteSpace(storeId) ? null : storeId;
                        await _context.SaveChangesAsync();

                        if (oldDetails.Count > 0) _context.SaleDetails.RemoveRange(oldDetails);

                        var oldPayments = await _context.SalePayments.Where(p => p.SaleId == headerUniqueId).ToListAsync();
                        if (oldPayments.Count > 0) _context.SalePayments.RemoveRange(oldPayments);

                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        header = new SaleHeaderRow
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
                            ExtraLess = request.AdditionalDiscount,
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
                    }

                    foreach (var item in request.Items)
                    {
                        var productIdStr = item.ProductId.ToString();
                        var stockRow = await FindStockAsync(productIdStr, item.BatchNumber, item.ExpiryDate);
                        if (stockRow == null)
                        {
                            return new SaleResult
                            {
                                Success = false,
                                Message = $"Stock not found for {item.ProductName} / {item.BatchNumber}"
                            };
                        }

                        var available = stockRow.PackQty ?? 0m;
                        var required = (decimal)item.Quantity;
                        if (available < required)
                        {
                            return new SaleResult
                            {
                                Success = false,
                                Message = $"Insufficient stock for {item.ProductName} / {item.BatchNumber}. Available {available:0.##}, Required {required:0.##}"
                            };
                        }

                        stockRow.PackQty = available - required;
                    }

                    var detailBaseSum = baseSum;

                    foreach (var tuple in lineBases)
                    {
                        var item = tuple.Item;
                        decimal unitPrice = item.UnitPrice;
                        decimal lineTotal = tuple.Gross;
                        decimal discountAmt = tuple.ItemDisc;
                        decimal afterItemDisc = tuple.AfterItemDisc;
                        var share = (additionalDiscount > 0 && detailBaseSum > 0) ? (additionalDiscount * (afterItemDisc / detailBaseSum)) : 0;
                        var afterAllDiscounts = Math.Max(0, afterItemDisc - share);
                        var gst = item.TaxPercent;
                        decimal includedTax = gst > 0 ? (afterAllDiscounts * (gst / (100 + gst))) : 0;
                        decimal taxable = afterAllDiscounts - includedTax;
                        decimal total = afterAllDiscounts;

                        var halfTax = includedTax / 2;

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
                            ItemDiscPerc = Math.Round(item.DiscountPercent, 2),
                            ItemDiscAmount = Math.Round(discountAmt, 2),
                            TaxableAmount = taxable,
                            CGSTAmount = halfTax,
                            SGSTAmount = halfTax,
                            IGSTAmount = 0,
                            TotalTaxAmount = includedTax,
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
                        SaleId = header.ID,
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

        private async Task<string> UpsertIntegrationCustomerAsync(CompleteSaleRequest request, string createdBy, string tenantId, DateTime now)
        {
            var name = (request.CustomerName ?? string.Empty).Trim();
            if (name.Length > 300) name = name[..300];
            var phone = (request.CustomerPhone ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone))
            {
                return "0";
            }

            CustomerMasterRow? existing = null;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                existing = await _context.CustomerMasters.FirstOrDefaultAsync(c => c.MobileNumber == phone);
            }

            if (existing != null)
            {
                var changed = false;
                if (!string.IsNullOrWhiteSpace(name) && !string.Equals(existing.CustomerName, name, StringComparison.OrdinalIgnoreCase))
                {
                    existing.CustomerName = name;
                    changed = true;
                }
                if (existing.ActiveStatus == false)
                {
                    existing.ActiveStatus = true;
                    changed = true;
                }
                if (changed)
                {
                    existing.ModifiedBy = createdBy;
                    existing.ModifiedDate = now;
                    await _context.SaveChangesAsync();
                }
                return existing.UniqueID;
            }

            var uniqueId = Guid.NewGuid().ToString();
            var customer = new CustomerMasterRow
            {
                UniqueID = uniqueId,
                CustomerCode = "CUST-0",
                CustomerName = string.IsNullOrWhiteSpace(name) ? "Walk-in" : name,
                MobileNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                ActiveStatus = true,
                CreatedBy = createdBy,
                CreatedDate = now,
                ModifiedBy = null,
                ModifiedDate = null,
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId
            };

            _context.CustomerMasters.Add(customer);
            await _context.SaveChangesAsync();

            customer.CustomerCode = $"CUST-{customer.ID}";
            await _context.SaveChangesAsync();

            return customer.UniqueID;
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

            var customerIdRaw = (header.CustomerID ?? string.Empty).Trim();
            Customer? customer = null;
            if (!string.IsNullOrWhiteSpace(customerIdRaw) && customerIdRaw != "0")
            {
                var cm = await _context.CustomerMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UniqueID == customerIdRaw);

                if (cm == null && int.TryParse(customerIdRaw, out var numericId))
                {
                    cm = await _context.CustomerMasters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.ID == numericId);
                }

                if (cm != null)
                {
                    customer = new Customer
                    {
                        Id = cm.ID,
                        Name = cm.CustomerName,
                        Phone = cm.MobileNumber ?? string.Empty,
                        Email = null,
                        LoyaltyPoints = 0
                    };
                }
            }

            var details = await _context.SaleDetails
                .AsNoTracking()
                .Where(d => d.SaleID == header.UniqueID)
                .OrderBy(d => d.ID)
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
                CustomerId = customer != null && customer.Id > 0 ? customer.Id : null,
                Customer = customer,
                SaleDate = header.BillDate,
                SubTotal = header.AmountBeforeTax ?? 0,
                ItemDiscount = 0,
                AdditionalDiscount = header.ExtraLess ?? 0,
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

            sale.ItemDiscount = sale.SaleItems.Sum(i => i.DiscountAmount);

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

                var headers = await headerQuery
                    .OrderByDescending(h => h.BillDate)
                    .ToListAsync();

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
                    : await _context.CustomerMasters
                        .AsNoTracking()
                        .Where(c => customerIdRawValues.Contains(c.UniqueID) || numericCustomerIds.Contains(c.ID))
                        .ToListAsync();

                var customerByUniqueId = customerMasters
                    .Where(c => !string.IsNullOrWhiteSpace(c.UniqueID))
                    .GroupBy(c => c.UniqueID.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var customerById = customerMasters
                    .GroupBy(c => c.ID)
                    .ToDictionary(g => g.Key, g => g.First());

                var sales = headers.Select(h =>
                {
                    var customerUniqueId = (h.CustomerID ?? string.Empty).Trim();
                    Customer? customer = null;

                    if (!string.IsNullOrWhiteSpace(customerUniqueId) && customerByUniqueId.TryGetValue(customerUniqueId, out var c))
                    {
                        customer = new Customer
                        {
                            Id = c.ID,
                            Name = c.CustomerName,
                            Phone = c.MobileNumber ?? string.Empty
                        };
                    }
                    else if (int.TryParse(customerUniqueId, out var numericId) && customerById.TryGetValue(numericId, out var c2))
                    {
                        customer = new Customer
                        {
                            Id = c2.ID,
                            Name = c2.CustomerName,
                            Phone = c2.MobileNumber ?? string.Empty
                        };
                    }
                    else
                    {
                        customer = new Customer
                        {
                            Id = 0,
                            Name = string.IsNullOrWhiteSpace(customerUniqueId) || customerUniqueId == "0" ? "Walk-in" : "Walk-in",
                            Phone = string.Empty
                        };
                    }

                    return new Sale
                    {
                        Id = h.ID,
                        SaleDate = h.BillDate,
                        InvoiceNumber = h.BillNo,
                        CustomerId = customer != null && customer.Id > 0 ? customer.Id : null,
                        Customer = customer,
                        SubTotal = h.AmountBeforeTax ?? 0,
                        ItemDiscount = Math.Max(0, (h.DiscountAmount ?? 0) - (h.ExtraLess ?? 0)),
                        AdditionalDiscount = h.ExtraLess ?? 0,
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
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var header = await _context.SaleHeaders.FirstOrDefaultAsync(h => h.ID == id);
                    if (header == null) return false;

                    static string NormalizeBatch(string? batchNumber) => (batchNumber ?? string.Empty).Trim();

                    async Task<ProductStockRow?> FindStockAsync(string productId, string? batchNumber, DateTime? expiryDate)
                    {
                        var batchNorm = NormalizeBatch(batchNumber);
                        if (!expiryDate.HasValue) return null;
                        var exp = expiryDate.Value.Date;

                        return await _context.ProductStocks.FirstOrDefaultAsync(ps =>
                            ps.ProductID == productId &&
                            (ps.BatchNumber ?? string.Empty) == batchNorm &&
                            ps.ExpiryDate.HasValue &&
                            EF.Functions.DateDiffDay(ps.ExpiryDate.Value, exp) == 0);
                    }

                    var details = await _context.SaleDetails.Where(d => d.SaleID == header.UniqueID).ToListAsync();
                    foreach (var d in details)
                    {
                        var stockRow = await FindStockAsync(d.ProductID, d.BatchNumber, d.ExpiryDate);
                        if (stockRow == null) return false;
                        var qtyToRestore = d.Qty ?? 0m;
                        stockRow.PackQty = (stockRow.PackQty ?? 0m) + qtyToRestore;
                    }

                    header.ActiveStatus = false;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            var sale = await _context.Sales.FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) return false;
            sale.PaymentStatus = "Cancelled";
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
