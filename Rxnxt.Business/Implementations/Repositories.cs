using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Rxnxt.Business.Data;
using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;

namespace Rxnxt.Business.Implementations
{
    // public class CustomerRepository : ICustomerRepository

    // {

    //     private readonly PharmacyDbContext _context;

    //     private readonly IConfiguration _configuration;

    //     public CustomerRepository(PharmacyDbContext context, IConfiguration configuration)

    //     {

    //         _context = context;

    //         _configuration = configuration;

    //     }



    //     public async Task<List<CustomerSearchResult>> SearchAsync(string query)

    //     {

    //         var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

    //         if (salesIntegrationEnabled)

    //         {

    //             var q = query.Trim();

    //             var term = q.ToLowerInvariant();

    //             return await _context.CustomerMasters

    //                 .AsNoTracking()

    //                 .Where(c => c.ActiveStatus)

    //                 .Where(c =>

    //                     c.CustomerName.ToLower().Contains(term) ||

    //                     (c.MobileNumber != null && c.MobileNumber.Contains(q)))

    //                 .OrderBy(c => c.CustomerName)

    //                 .Take(10)

    //                 .Select(c => new CustomerSearchResult

    //                 {

    //                     Id = c.ID,

    //                     Name = c.CustomerName,

    //                     Phone = c.MobileNumber ?? string.Empty,

    //                     Email = null,

    //                     LoyaltyPoints = 0

    //                 })

    //                 .ToListAsync();

    //         }



    //         {

    //             var term = query.Trim().ToLowerInvariant();

    //             return await _context.Customers

    //                 .AsNoTracking()

    //                 .Where(c => c.Name.ToLower().Contains(term)

    //                           || c.Phone.Contains(term)

    //                           || (c.Email != null && c.Email.ToLower().Contains(term)))

    //                 .Select(c => new CustomerSearchResult

    //                 {

    //                     Id = c.Id,

    //                     Name = c.Name,

    //                     Phone = c.Phone,

    //                     Email = c.Email,

    //                     LoyaltyPoints = c.LoyaltyPoints

    //                 })

    //                 .Take(10)

    //                 .ToListAsync();

    //         }

    //     }



    //     public async Task<Customer?> GetByIdAsync(int id)

    //     {

    //         var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

    //         if (salesIntegrationEnabled)

    //         {

    //             var row = await _context.CustomerMasters.AsNoTracking().FirstOrDefaultAsync(c => c.ID == id);

    //             if (row == null) return null;

    //             return new Customer

    //             {

    //                 Id = row.ID,

    //                 Name = row.CustomerName,

    //                 Phone = row.MobileNumber ?? string.Empty,

    //                 Email = null,

    //                 LoyaltyPoints = 0

    //             };

    //         }



    //         return await _context.Customers.FindAsync(id);

    //     }



    //     public async Task<Customer> CreateAsync(Customer customer)

    //     {

    //         var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

    //         if (salesIntegrationEnabled)

    //         {

    //             var tenantId = _configuration["SalesIntegration:TenantId"] ?? string.Empty;

    //             var createdBy = _configuration["SalesIntegration:CreatedBy"] ?? "POS";

    //             var now = DateTime.Now;



    //             var name = (customer.Name ?? string.Empty).Trim();

    //             if (name.Length > 300) name = name[..300];

    //             var phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();



    //             var row = new CustomerMasterRow

    //             {

    //                 UniqueID = Guid.NewGuid().ToString(),

    //                 CustomerCode = "CUST-0",

    //                 CustomerName = name,

    //                 MobileNumber = phone,

    //                 ActiveStatus = true,

    //                 CreatedBy = createdBy,

    //                 CreatedDate = now,

    //                 ModifiedBy = null,

    //                 ModifiedDate = null,

    //                 TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId

    //             };



    //             _context.CustomerMasters.Add(row);

    //             await _context.SaveChangesAsync();

    //             row.CustomerCode = $"CUST-{row.ID}";

    //             await _context.SaveChangesAsync();



    //             customer.Id = row.ID;

    //             customer.Phone = row.MobileNumber ?? string.Empty;

    //             customer.CreatedDate = now;

    //             return customer;

    //         }



    //         customer.CreatedDate = DateTime.Now;

    //         _context.Customers.Add(customer);

    //         await _context.SaveChangesAsync();

    //         return customer;

    //     }



    //     public async Task<bool> PhoneExistsAsync(string phone)

    //     {

    //         var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

    //         if (salesIntegrationEnabled)

    //         {

    //             var p = phone.Trim();

    //             return await _context.CustomerMasters.AsNoTracking().AnyAsync(c => c.MobileNumber == p);

    //         }



    //         return await _context.Customers.AnyAsync(c => c.Phone == phone);

    //     }

    // }



    // public class MedicineRepository : IMedicineRepository

    // {

    //     private readonly PharmacyDbContext _context;

    //     public MedicineRepository(PharmacyDbContext context) => _context = context;



    //     public async Task<List<MedicineSearchResult>> SearchAsync(string query)

    //     {

    //         query = query.Trim().ToLower();

    //         return await _context.Medicines

    //             .Where(m => m.Name.ToLower().Contains(query)

    //                       || (m.GenericName != null && m.GenericName.ToLower().Contains(query))

    //                       || (m.Manufacturer != null && m.Manufacturer.ToLower().Contains(query)))

    //             .Include(m => m.Batches)

    //             .Select(m => new MedicineSearchResult

    //             {

    //                 Id = m.Id,

    //                 Name = m.Name,

    //                 GenericName = m.GenericName,

    //                 Manufacturer = m.Manufacturer,

    //                 Category = m.Category,

    //                 Batches = m.Batches.Where(b => b.StripQuantity > 0 && b.ExpiryDate > DateTime.Now)

    //                     .Select(b => new BatchSearchResult

    //                     {

    //                         Id = b.Id,

    //                         MedicineId = b.MedicineId,

    //                         MedicineName = m.Name,

    //                         GenericName = m.GenericName,

    //                         BatchNumber = b.BatchNumber,

    //                         ExpiryDate = b.ExpiryDate,

    //                         StripQuantity = b.StripQuantity,

    //                         TabletPerStrip = b.TabletPerStrip,

    //                         SellingPriceStrip = b.SellingPriceStrip,

    //                         SellingPriceTablet = b.SellingPriceTablet,

    //                         Manufacturer = m.Manufacturer,

    //                         IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),

    //                         IsExpired = b.ExpiryDate <= DateTime.Now,

    //                         TotalTablets = b.StripQuantity * b.TabletPerStrip

    //                     }).ToList()

    //             })

    //             .Take(10)

    //             .ToListAsync();

    //     }



    //     public async Task<Medicine?> GetByIdAsync(int id) =>

    //         await _context.Medicines.Include(m => m.Batches).FirstOrDefaultAsync(m => m.Id == id);

    // }



    // public class BatchRepository : IBatchRepository

    // {

    //     private readonly PharmacyDbContext _context;

    //     public BatchRepository(PharmacyDbContext context) => _context = context;



    //     public async Task<List<BatchSearchResult>> SearchByBatchNumberAsync(string batchNumber)

    //     {

    //         batchNumber = batchNumber.Trim().ToLower();

    //         return await _context.Batches

    //             .Include(b => b.Medicine)

    //             .Where(b => b.BatchNumber.ToLower().Contains(batchNumber) && b.StripQuantity > 0)

    //             .Select(b => new BatchSearchResult

    //             {

    //                 Id = b.Id,

    //                 MedicineId = b.MedicineId,

    //                 MedicineName = b.Medicine!.Name,

    //                 GenericName = b.Medicine.GenericName,

    //                 BatchNumber = b.BatchNumber,

    //                 ExpiryDate = b.ExpiryDate,

    //                 StripQuantity = b.StripQuantity,

    //                 TabletPerStrip = b.TabletPerStrip,

    //                 SellingPriceStrip = b.SellingPriceStrip,

    //                 SellingPriceTablet = b.SellingPriceTablet,

    //                 Manufacturer = b.Medicine.Manufacturer,

    //                 IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),

    //                 IsExpired = b.ExpiryDate <= DateTime.Now,

    //                 TotalTablets = b.StripQuantity * b.TabletPerStrip

    //             })

    //             .Take(10)

    //             .ToListAsync();

    //     }



    //     public async Task<List<BatchSearchResult>> SearchByMedicineAsync(string query)

    //     {

    //         query = query.Trim().ToLower();

    //         return await _context.Batches

    //             .Include(b => b.Medicine)

    //             .Where(b => (b.Medicine!.Name.ToLower().Contains(query)

    //                       || (b.Medicine.GenericName != null && b.Medicine.GenericName.ToLower().Contains(query)))

    //                       && b.StripQuantity > 0

    //                       && b.ExpiryDate > DateTime.Now)

    //             .Select(b => new BatchSearchResult

    //             {

    //                 Id = b.Id,

    //                 MedicineId = b.MedicineId,

    //                 MedicineName = b.Medicine!.Name,

    //                 GenericName = b.Medicine.GenericName,

    //                 BatchNumber = b.BatchNumber,

    //                 ExpiryDate = b.ExpiryDate,

    //                 StripQuantity = b.StripQuantity,

    //                 TabletPerStrip = b.TabletPerStrip,

    //                 SellingPriceStrip = b.SellingPriceStrip,

    //                 SellingPriceTablet = b.SellingPriceTablet,

    //                 Manufacturer = b.Medicine.Manufacturer,

    //                 IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),

    //                 IsExpired = b.ExpiryDate <= DateTime.Now,

    //                 TotalTablets = b.StripQuantity * b.TabletPerStrip

    //             })

    //             .Take(15)

    //             .ToListAsync();

    //     }



    //     public async Task<List<BatchSearchResult>> GetBatchesByMedicineIdAsync(int medicineId)

    //     {

    //         return await _context.Batches

    //             .Include(b => b.Medicine)

    //             .Where(b => b.MedicineId == medicineId && b.StripQuantity > 0 && b.ExpiryDate > DateTime.Now)

    //             .Select(b => new BatchSearchResult

    //             {

    //                 Id = b.Id,

    //                 MedicineId = b.MedicineId,

    //                 MedicineName = b.Medicine!.Name,

    //                 GenericName = b.Medicine.GenericName,

    //                 BatchNumber = b.BatchNumber,

    //                 ExpiryDate = b.ExpiryDate,

    //                 StripQuantity = b.StripQuantity,

    //                 TabletPerStrip = b.TabletPerStrip,

    //                 SellingPriceStrip = b.SellingPriceStrip,

    //                 SellingPriceTablet = b.SellingPriceTablet,

    //                 Manufacturer = b.Medicine.Manufacturer,

    //                 IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),

    //                 IsExpired = b.ExpiryDate <= DateTime.Now,

    //                 TotalTablets = b.StripQuantity * b.TabletPerStrip

    //             })

    //             .ToListAsync();

    //     }



    //     public async Task<BatchSearchResult?> GetByIdAsync(int id)

    //     {

    //         return await _context.Batches

    //             .Include(b => b.Medicine)

    //             .Where(b => b.Id == id)

    //             .Select(b => new BatchSearchResult

    //             {

    //                 Id = b.Id,

    //                 MedicineId = b.MedicineId,

    //                 MedicineName = b.Medicine!.Name,

    //                 GenericName = b.Medicine.GenericName,

    //                 BatchNumber = b.BatchNumber,

    //                 ExpiryDate = b.ExpiryDate,

    //                 StripQuantity = b.StripQuantity,

    //                 TabletPerStrip = b.TabletPerStrip,

    //                 SellingPriceStrip = b.SellingPriceStrip,

    //                 SellingPriceTablet = b.SellingPriceTablet,

    //                 Manufacturer = b.Medicine.Manufacturer,

    //                 IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),

    //                 IsExpired = b.ExpiryDate <= DateTime.Now,

    //                 TotalTablets = b.StripQuantity * b.TabletPerStrip

    //             })

    //             .FirstOrDefaultAsync();

    //     }



    //     public async Task<List<BatchSearchResult>> AdvancedSearchAsync(string? batchNumber, string? medicineName, string? composition, DateTime? expiryFrom, DateTime? expiryTo)

    //     {

    //         var query = _context.Batches.Include(b => b.Medicine).Where(b => b.StripQuantity > 0);



    //         if (!string.IsNullOrWhiteSpace(batchNumber))

    //             query = query.Where(b => b.BatchNumber.ToLower().Contains(batchNumber.ToLower()));



    //         if (!string.IsNullOrWhiteSpace(medicineName))

    //             query = query.Where(b => b.Medicine!.Name.ToLower().Contains(medicineName.ToLower()));



    //         if (!string.IsNullOrWhiteSpace(composition))

    //             query = query.Where(b => b.Medicine!.GenericName != null && b.Medicine.GenericName.ToLower().Contains(composition.ToLower()));



    //         if (expiryFrom.HasValue)

    //             query = query.Where(b => b.ExpiryDate >= expiryFrom.Value);



    //         if (expiryTo.HasValue)

    //             query = query.Where(b => b.ExpiryDate <= expiryTo.Value);



    //         return await query.Select(b => new BatchSearchResult

    //         {

    //             Id = b.Id,

    //             MedicineId = b.MedicineId,

    //             MedicineName = b.Medicine!.Name,

    //             GenericName = b.Medicine.GenericName,

    //             BatchNumber = b.BatchNumber,

    //             ExpiryDate = b.ExpiryDate,

    //             StripQuantity = b.StripQuantity,

    //             TabletPerStrip = b.TabletPerStrip,

    //             SellingPriceStrip = b.SellingPriceStrip,

    //             SellingPriceTablet = b.SellingPriceTablet,

    //             Manufacturer = b.Medicine.Manufacturer,

    //             IsNearExpiry = b.ExpiryDate <= DateTime.Now.AddMonths(3),

    //             IsExpired = b.ExpiryDate <= DateTime.Now,

    //             TotalTablets = b.StripQuantity * b.TabletPerStrip

    //         })

    //         .ToListAsync();

    //     }

    // }



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
                    includedTax = Math.Round(includedTax, 2, MidpointRounding.AwayFromZero);
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
                        UomName = string.IsNullOrWhiteSpace(item.SaleUomName) ? item.UomName : item.SaleUomName,
                        Quantity = item.Quantity,
                        UnitType = string.IsNullOrWhiteSpace(item.SaleUomName) ? item.UnitType : item.SaleUomName,
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
                    includedTax = Math.Round(includedTax, 2, MidpointRounding.AwayFromZero);
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
                        var oldPayments = await _context.SalePayments.Where(p => p.SaleId == headerUniqueId).ToListAsync();

                        if (request.ReturnMode)
                        {
                            static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

                            static bool Matches(string a, string b) =>
                                !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                                string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

                            var uomIdCacheByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            if (!string.Equals((header.CustomerID ?? string.Empty).Trim(), (customerIdStr ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                return new SaleResult { Success = false, Message = "Return mode: customer cannot be changed." };
                            }

                            if (header.ExtraLess != request.AdditionalDiscount)
                            {
                                return new SaleResult { Success = false, Message = "Return mode: additional discount cannot be changed." };
                            }

                            var oldKeySet = oldDetails
                                .Select(d => $"{(d.ProductID ?? string.Empty).Trim().ToLowerInvariant()}|{NormalizeBatch(d.BatchNumber).Trim().ToLowerInvariant()}|{d.ExpiryDate?.Date:yyyyMMdd}")
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

                            var newKeySet = request.Items
                                .Select(i => $"{i.ProductId.ToString().Trim().ToLowerInvariant()}|{NormalizeBatch(i.BatchNumber).Trim().ToLowerInvariant()}|{i.ExpiryDate.Date:yyyyMMdd}")
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

                            if (!oldKeySet.SetEquals(newKeySet))
                            {
                                return new SaleResult { Success = false, Message = "Return mode: items cannot be added/removed/changed." };
                            }

                            // Enforce cash-only refund on return
                            var nonZeroPayments = request.Payments
                                .Where(p => p != null && p.Amount != 0)
                                .ToList();
                            if (nonZeroPayments.Count != 1 || !string.Equals((nonZeroPayments[0].PaymentMode ?? string.Empty).Trim(), "Cash", StringComparison.OrdinalIgnoreCase))
                            {
                                return new SaleResult { Success = false, Message = "Return mode: refund must be Cash only." };
                            }

                            foreach (var i in request.Items)
                            {
                                var newUnit = (i.SaleUomName ?? i.UomName ?? string.Empty).Trim();
                                var old = oldDetails.FirstOrDefault(d =>
                                    string.Equals((d.ProductID ?? string.Empty).Trim(), i.ProductId.ToString().Trim(), StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(NormalizeBatch(d.BatchNumber), NormalizeBatch(i.BatchNumber), StringComparison.OrdinalIgnoreCase) &&
                                    d.ExpiryDate.HasValue &&
                                    d.ExpiryDate.Value.Date == i.ExpiryDate.Date);

                                if (old == null)
                                {
                                    return new SaleResult { Success = false, Message = "Return mode: invalid item." };
                                }

                                bool UnitMatches(string a, string b) =>
                                    !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                                    string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

                                var oldUnit = (old.SaleUOMID ?? string.Empty).Trim();
                                var oldUnitPrice = old.SalePrice ?? old.MRP ?? 0m;

                                // Price is locked in return mode, except when the user switches unit.
                                // In that case, allow the unit price to change only by the expected conversion factor.
                                if (UnitMatches(oldUnit, newUnit))
                                {
                                    if (Math.Abs(oldUnitPrice - i.UnitPrice) > 0.01m)
                                    {
                                        return new SaleResult { Success = false, Message = "Return mode: price cannot be changed." };
                                    }
                                }
                                else
                                {
                                    var productIdStr = i.ProductId.ToString();
                                    var pm = await _context.ProductMasters
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(p => p.UniqueID == productIdStr);

                                    if (pm == null)
                                    {
                                        return new SaleResult { Success = false, Message = "Return mode: price cannot be changed." };
                                    }

                                    var baseUomId = (pm.UOMID ?? string.Empty).Trim();
                                    var otherUomId = (pm.OtherUOMID ?? string.Empty).Trim();
                                    var factor = pm.ConversionFactor.GetValueOrDefault(1m);
                                    if (factor <= 0) factor = 1m;

                                    var uomIds = new[] { baseUomId, otherUomId }
                                        .Where(x => !string.IsNullOrWhiteSpace(x))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                                    var uomNameById = uomIds.Count == 0
                                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                        : await _context.UomMasters
                                            .AsNoTracking()
                                            .Where(u => uomIds.Contains(u.UniqueID))
                                            .Select(u => new { u.UniqueID, u.UOMName })
                                            .ToDictionaryAsync(x => x.UniqueID, x => x.UOMName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                                    var baseName = (!string.IsNullOrWhiteSpace(baseUomId) && uomNameById.TryGetValue(baseUomId, out var bn)) ? bn : string.Empty;
                                    var otherName = (!string.IsNullOrWhiteSpace(otherUomId) && uomNameById.TryGetValue(otherUomId, out var on)) ? on : string.Empty;

                                    // Heuristic: treat the larger unit as base for conversion (same as frontend).
                                    if (!string.IsNullOrWhiteSpace(baseName) && !string.IsNullOrWhiteSpace(otherName) &&
                                        string.Equals(baseName.Trim(), "PCS", StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(otherName.Trim(), "PCS", StringComparison.OrdinalIgnoreCase))
                                    {
                                        (baseName, otherName) = (otherName, baseName);
                                    }

                                    decimal expected;
                                    var canConvert = !string.IsNullOrWhiteSpace(baseName) && !string.IsNullOrWhiteSpace(otherName) && factor > 0m;
                                    if (!canConvert)
                                    {
                                        return new SaleResult { Success = false, Message = "Return mode: price cannot be changed." };
                                    }

                                    if (UnitMatches(oldUnit, baseName) && UnitMatches(newUnit, otherName))
                                    {
                                        expected = oldUnitPrice / factor;
                                    }
                                    else if (UnitMatches(oldUnit, otherName) && UnitMatches(newUnit, baseName))
                                    {
                                        expected = oldUnitPrice * factor;
                                    }
                                    else
                                    {
                                        return new SaleResult { Success = false, Message = "Return mode: price cannot be changed." };
                                    }

                                    expected = Math.Round(expected, 2, MidpointRounding.AwayFromZero);
                                    if (Math.Abs(expected - i.UnitPrice) > 0.02m)
                                    {
                                        return new SaleResult { Success = false, Message = "Return mode: price cannot be changed." };
                                    }
                                }

                                if (Math.Abs((old.ItemDiscPerc ?? 0m) - i.DiscountPercent) > 0.01m)
                                {
                                    return new SaleResult { Success = false, Message = "Return mode: discount cannot be changed." };
                                }

                                if (Math.Abs((old.TaxPerc ?? 0m) - i.TaxPercent) > 0.01m)
                                {
                                    return new SaleResult { Success = false, Message = "Return mode: tax cannot be changed." };
                                }

                                // Only allow decreasing quantity (in base UOM)
                                var oldBaseQty = old.Qty ?? 0m;
                                if (oldBaseQty < 0) oldBaseQty = 0m;

                                var productIdStrForQty = i.ProductId.ToString();
                                var pmForQty = await _context.ProductMasters
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(p => p.UniqueID == productIdStrForQty);

                                decimal newRequiredBaseQty;
                                if (pmForQty == null)
                                {
                                    newRequiredBaseQty = i.Quantity;
                                }
                                else
                                {
                                    var baseUomId2 = (pmForQty.UOMID ?? string.Empty).Trim();
                                    var otherUomId2 = (pmForQty.OtherUOMID ?? string.Empty).Trim();
                                    var factor2 = pmForQty.ConversionFactor.GetValueOrDefault(1m);
                                    if (factor2 <= 0) factor2 = 1m;

                                    var uomIds2 = new[] { baseUomId2, otherUomId2 }
                                        .Where(x => !string.IsNullOrWhiteSpace(x))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                                    var uomNameById2 = uomIds2.Count == 0
                                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                        : await _context.UomMasters
                                            .AsNoTracking()
                                            .Where(u => uomIds2.Contains(u.UniqueID))
                                            .Select(u => new { u.UniqueID, u.UOMName })
                                            .ToDictionaryAsync(x => x.UniqueID, x => x.UOMName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                                    var baseName2 = (!string.IsNullOrWhiteSpace(baseUomId2) && uomNameById2.TryGetValue(baseUomId2, out var bn2)) ? bn2 : string.Empty;
                                    var otherName2 = (!string.IsNullOrWhiteSpace(otherUomId2) && uomNameById2.TryGetValue(otherUomId2, out var on2)) ? on2 : string.Empty;

                                    var isPcsStripPair2 =
                                        (Matches(baseName2, "PCS") && Matches(otherName2, "STRIP")) ||
                                        (Matches(baseName2, "STRIP") && Matches(otherName2, "PCS"));

                                    if (isPcsStripPair2)
                                    {
                                        if (Matches(baseName2, "PCS"))
                                        {
                                            if (Matches(newUnit, "PCS")) newRequiredBaseQty = i.Quantity;
                                            else if (Matches(newUnit, "STRIP")) newRequiredBaseQty = i.Quantity * factor2;
                                            else return new SaleResult { Success = false, Message = $"Invalid unit '{newUnit}' for {i.ProductName}." };
                                        }
                                        else if (Matches(baseName2, "STRIP"))
                                        {
                                            if (Matches(newUnit, "STRIP")) newRequiredBaseQty = i.Quantity;
                                            else if (Matches(newUnit, "PCS")) newRequiredBaseQty = i.Quantity / factor2;
                                            else return new SaleResult { Success = false, Message = $"Invalid unit '{newUnit}' for {i.ProductName}." };
                                        }
                                        else
                                        {
                                            return new SaleResult { Success = false, Message = $"Conversion not possible for {i.ProductName}. Please contact admin to fix UOM mapping." };
                                        }
                                    }
                                    else
                                    {
                                        if (Matches(newUnit, baseName2) || Matches(newUnit, i.UomName))
                                        {
                                            newRequiredBaseQty = i.Quantity;
                                        }
                                        else if (!string.IsNullOrWhiteSpace(otherName2) && Matches(newUnit, otherName2))
                                        {
                                            var baseIsPcs2 = string.Equals(baseName2?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                            var otherIsPcs2 = string.Equals(otherName2?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                            var mappingReversed2 = baseIsPcs2 && !otherIsPcs2;
                                            newRequiredBaseQty = mappingReversed2 ? (i.Quantity * factor2) : (i.Quantity / factor2);
                                        }
                                        else
                                        {
                                            return new SaleResult { Success = false, Message = $"Invalid unit '{newUnit}' for {i.ProductName}." };
                                        }
                                    }
                                }

                                if (newRequiredBaseQty > oldBaseQty + 0.0001m)
                                {
                                    return new SaleResult { Success = false, Message = "Return mode: quantity can only be decreased." };
                                }
                            }

                            // Persist return separately: do NOT modify original sale header/details/payments.
                            var returnUniqueId = Guid.NewGuid().ToString();
                            var returnBillType = "SalesReturn";

                            var returnDetailRows = new List<Rxnxt.Business.Data.SalesReturnDetailRow>();

                            decimal refundBillAmount = 0m;
                            decimal refundTaxAmount = 0m;
                            decimal refundDiscountAmount = 0m;
                            decimal refundAmountBeforeTax = 0m;

                            foreach (var i in request.Items)
                            {
                                var newUnit = (i.SaleUomName ?? i.UomName ?? string.Empty).Trim();
                                var old = oldDetails.FirstOrDefault(d =>
                                    string.Equals((d.ProductID ?? string.Empty).Trim(), i.ProductId.ToString().Trim(), StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(NormalizeBatch(d.BatchNumber), NormalizeBatch(i.BatchNumber), StringComparison.OrdinalIgnoreCase) &&
                                    d.ExpiryDate.HasValue &&
                                    d.ExpiryDate.Value.Date == i.ExpiryDate.Date);

                                if (old == null) continue;

                                var oldBaseQty = old.Qty ?? 0m;
                                if (oldBaseQty <= 0) continue;

                                // Use already-computed conversion logic for new required base qty
                                var productIdStr = i.ProductId.ToString();
                                var stockRow = await FindStockAsync(productIdStr, i.BatchNumber, i.ExpiryDate);
                                if (stockRow == null)
                                {
                                    return new SaleResult { Success = false, Message = $"Stock not found for {i.ProductName} / {i.BatchNumber}" };
                                }

                                var pm = await _context.ProductMasters
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(p => p.UniqueID == productIdStr);

                                decimal newRequiredBaseQty;
                                if (pm == null)
                                {
                                    newRequiredBaseQty = i.Quantity;
                                }
                                else
                                {
                                    var baseUomId = (pm.UOMID ?? string.Empty).Trim();
                                    var otherUomId = (pm.OtherUOMID ?? string.Empty).Trim();
                                    var factor = pm.ConversionFactor.GetValueOrDefault(1m);
                                    if (factor <= 0) factor = 1m;

                                    var uomIds = new[] { baseUomId, otherUomId }
                                        .Where(x => !string.IsNullOrWhiteSpace(x))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                                    var uomNameById = uomIds.Count == 0
                                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                        : await _context.UomMasters
                                            .AsNoTracking()
                                            .Where(u => uomIds.Contains(u.UniqueID))
                                            .Select(u => new { u.UniqueID, u.UOMName })
                                            .ToDictionaryAsync(x => x.UniqueID, x => x.UOMName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                                    var baseName = (!string.IsNullOrWhiteSpace(baseUomId) && uomNameById.TryGetValue(baseUomId, out var bn)) ? bn : string.Empty;
                                    var otherName = (!string.IsNullOrWhiteSpace(otherUomId) && uomNameById.TryGetValue(otherUomId, out var on)) ? on : string.Empty;

                                    var isPcsStripPair =
                                        (Matches(baseName, "PCS") && Matches(otherName, "STRIP")) ||
                                        (Matches(baseName, "STRIP") && Matches(otherName, "PCS"));

                                    if (isPcsStripPair)
                                    {
                                        if (Matches(baseName, "PCS"))
                                        {
                                            if (Matches(newUnit, "PCS")) newRequiredBaseQty = i.Quantity;
                                            else if (Matches(newUnit, "STRIP")) newRequiredBaseQty = i.Quantity * factor;
                                            else return new SaleResult { Success = false, Message = $"Invalid unit '{newUnit}' for {i.ProductName}." };
                                        }
                                        else if (Matches(baseName, "STRIP"))
                                        {
                                            if (Matches(newUnit, "STRIP")) newRequiredBaseQty = i.Quantity;
                                            else if (Matches(newUnit, "PCS")) newRequiredBaseQty = i.Quantity / factor;
                                            else return new SaleResult { Success = false, Message = $"Invalid unit '{newUnit}' for {i.ProductName}." };
                                        }
                                        else
                                        {
                                            return new SaleResult { Success = false, Message = $"Conversion not possible for {i.ProductName}. Please contact admin to fix UOM mapping." };
                                        }
                                    }
                                    else
                                    {
                                        if (Matches(newUnit, baseName) || Matches(newUnit, i.UomName))
                                        {
                                            newRequiredBaseQty = i.Quantity;
                                        }
                                        else if (!string.IsNullOrWhiteSpace(otherName) && Matches(newUnit, otherName))
                                        {
                                            var baseIsPcs = string.Equals(baseName?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                            var otherIsPcs = string.Equals(otherName?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                            var mappingReversed = baseIsPcs && !otherIsPcs;
                                            newRequiredBaseQty = mappingReversed ? (i.Quantity * factor) : (i.Quantity / factor);
                                        }
                                        else
                                        {
                                            return new SaleResult { Success = false, Message = $"Invalid unit '{newUnit}' for {i.ProductName}." };
                                        }
                                    }
                                }

                                var returnedBaseQty = oldBaseQty - newRequiredBaseQty;
                                if (returnedBaseQty <= 0) continue;

                                // Add stock back
                                stockRow.PackQty = (stockRow.PackQty ?? 0m) + returnedBaseQty;

                                var oldItemTotal = old.ItemTotal ?? 0m;
                                var oldTax = old.TotalTaxAmount ?? 0m;
                                var oldTaxable = old.TaxableAmount ?? 0m;
                                var oldDisc = old.ItemDiscAmount ?? 0m;

                                var ratio = returnedBaseQty / oldBaseQty;
                                var returnLineTotal = Round2(oldItemTotal * ratio);
                                var returnTax = Round2(oldTax * ratio);
                                var returnTaxable = Round2(oldTaxable * ratio);
                                var returnDisc = Round2(oldDisc * ratio);

                                refundBillAmount += returnLineTotal;
                                refundTaxAmount += returnTax;
                                refundAmountBeforeTax += returnTaxable;
                                refundDiscountAmount += returnDisc;

                                string? resolvedSaleUomId = null;
                                if (!string.IsNullOrWhiteSpace(newUnit))
                                {
                                    if (!uomIdCacheByName.TryGetValue(newUnit, out var cachedUomId))
                                    {
                                        var newUnitNorm = newUnit.Trim().ToLower();
                                        var uom = await _context.UomMasters
                                            .AsNoTracking()
                                            .FirstOrDefaultAsync(u => u.UOMName != null && u.UOMName.Trim().ToLower() == newUnitNorm);
                                        cachedUomId = uom?.UniqueID ?? string.Empty;
                                        uomIdCacheByName[newUnit] = cachedUomId;
                                    }
                                    if (!string.IsNullOrWhiteSpace(cachedUomId))
                                    {
                                        resolvedSaleUomId = cachedUomId;
                                    }
                                }

                                // Prefer old detail's UOM IDs to match DB expectations
                                resolvedSaleUomId ??= old.SaleUOMID;
                                var resolvedBaseUomId = old.BaseUOMID;

                                returnDetailRows.Add(new Rxnxt.Business.Data.SalesReturnDetailRow
                                {
                                    UniqueID = Guid.NewGuid().ToString(),
                                    SaleID = returnUniqueId,
                                    ProductID = productIdStr,
                                    BatchNumber = i.BatchNumber,
                                    ExpiryDate = i.ExpiryDate,
                                    UnitID = null,
                                    PackTypeID = null,
                                    MRP = i.UnitPrice,
                                    PurchasePrice = null,
                                    SalePrice = i.UnitPrice,
                                    FreeQty = 0m,
                                    Remarks = null,
                                    Qty = returnedBaseQty,
                                    TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                                    BaseUOMID = resolvedBaseUomId,
                                    SaleUOMID = resolvedSaleUomId,
                                    SaleUOMQty = null
                                });
                            }

                            refundBillAmount = Round2(refundBillAmount);
                            refundTaxAmount = Round2(refundTaxAmount);
                            refundAmountBeforeTax = Round2(refundAmountBeforeTax);
                            refundDiscountAmount = Round2(refundDiscountAmount);

                            var refundRounded = Math.Round(refundBillAmount, 0, MidpointRounding.AwayFromZero);
                            var refundRoundOff = refundRounded - refundBillAmount;

                            var returnHeader = new Rxnxt.Business.Data.SalesReturnHeaderRow
                            {
                                UniqueID = returnUniqueId,
                                BillNo = "SR-0",
                                BillDate = now,
                                BillType = returnBillType,
                                CustomerID = header.CustomerID ?? customerIdStr,
                                Narration = null,
                                BillAmount = refundBillAmount,
                                TaxAmount = refundTaxAmount,
                                DiscountAmount = refundDiscountAmount,
                                ExtraAdd = 0m,
                                ExtraLess = 0m,
                                ActiveStatus = true,
                                CreatedBy = createdBy,
                                CreatedDate = now,
                                ModifiedBy = null,
                                ModifiedDate = null,
                                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                                DiscountPerc = 0m,
                                AmountBeforeTax = refundAmountBeforeTax,
                                SaleId = headerUniqueId,
                                RoundOff = refundRoundOff
                            };

                            _context.SalesReturnHeaders.Add(returnHeader);
                            await _context.SaveChangesAsync();

                            if (returnDetailRows.Count > 0)
                            {
                                _context.SalesReturnDetails.AddRange(returnDetailRows);
                                await _context.SaveChangesAsync();
                            }

                            returnHeader.BillNo = $"SR-{returnHeader.ID}";
                            await _context.SaveChangesAsync();

                            await transaction.CommitAsync();

                            return new SaleResult
                            {
                                Success = true,
                                Message = "Return saved successfully!",
                                SaleId = header.ID,
                                InvoiceNumber = returnHeader.BillNo
                            };
                        }

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

                        var selectedUomName = (item.SaleUomName ?? item.UomName ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(selectedUomName))
                        {
                            return new SaleResult
                            {
                                Success = false,
                                Message = $"Unit is missing for {item.ProductName} / {item.BatchNumber}"
                            };
                        }

                        var pm = await _context.ProductMasters
                            .AsNoTracking()
                            .FirstOrDefaultAsync(p => p.UniqueID == productIdStr);

                        decimal requiredBaseQty;
                        if (pm == null)
                        {
                            // Fallback: no conversion info, assume API unit matches stock unit.
                            requiredBaseQty = item.Quantity;
                        }
                        else
                        {
                            var baseUomId = (pm.UOMID ?? string.Empty).Trim();
                            var otherUomId = (pm.OtherUOMID ?? string.Empty).Trim();
                            var factor = pm.ConversionFactor.GetValueOrDefault(1m);
                            if (factor <= 0) factor = 1m;

                            var uomIds = new[] { baseUomId, otherUomId }
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            var uomNameById = uomIds.Count == 0
                                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                : await _context.UomMasters
                                    .AsNoTracking()
                                    .Where(u => uomIds.Contains(u.UniqueID))
                                    .Select(u => new { u.UniqueID, u.UOMName })
                                    .ToDictionaryAsync(x => x.UniqueID, x => x.UOMName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                            var baseName = (!string.IsNullOrWhiteSpace(baseUomId) && uomNameById.TryGetValue(baseUomId, out var bn)) ? bn : string.Empty;
                            var otherName = (!string.IsNullOrWhiteSpace(otherUomId) && uomNameById.TryGetValue(otherUomId, out var on)) ? on : string.Empty;

                            bool Matches(string a, string b) =>
                                !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                                string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

                            var isPcsStripPair =
                                (Matches(baseName, "PCS") && Matches(otherName, "STRIP")) ||
                                (Matches(baseName, "STRIP") && Matches(otherName, "PCS"));

                            if (isPcsStripPair)
                            {
                                if (Matches(baseName, "PCS"))
                                {
                                    if (Matches(selectedUomName, "PCS"))
                                    {
                                        requiredBaseQty = item.Quantity;
                                    }
                                    else if (Matches(selectedUomName, "STRIP"))
                                    {
                                        requiredBaseQty = item.Quantity * factor;
                                    }
                                    else
                                    {
                                        return new SaleResult
                                        {
                                            Success = false,
                                            Message = $"Invalid unit '{selectedUomName}' for {item.ProductName}."
                                        };
                                    }
                                }
                                else if (Matches(baseName, "STRIP"))
                                {
                                    if (Matches(selectedUomName, "STRIP"))
                                    {
                                        requiredBaseQty = item.Quantity;
                                    }
                                    else if (Matches(selectedUomName, "PCS"))
                                    {
                                        if (factor <= 0)
                                        {
                                            return new SaleResult
                                            {
                                                Success = false,
                                                Message = $"Conversion not possible for {item.ProductName}. Please contact admin to fix conversion factor."
                                            };
                                        }
                                        requiredBaseQty = item.Quantity / factor;
                                    }
                                    else
                                    {
                                        return new SaleResult
                                        {
                                            Success = false,
                                            Message = $"Invalid unit '{selectedUomName}' for {item.ProductName}."
                                        };
                                    }
                                }
                                else
                                {
                                    return new SaleResult
                                    {
                                        Success = false,
                                        Message = $"Conversion not possible for {item.ProductName}. Please contact admin to fix UOM mapping."
                                    };
                                }
                            }
                            else
                            {

                                if (Matches(selectedUomName, baseName) || Matches(selectedUomName, item.UomName))
                                {
                                    requiredBaseQty = item.Quantity;
                                }
                                else if (!string.IsNullOrWhiteSpace(otherName) && Matches(selectedUomName, otherName))
                                {
                                    if (factor != 1m && !string.IsNullOrWhiteSpace(baseUomId) && Matches(baseUomId, otherUomId))
                                    {
                                        return new SaleResult
                                        {
                                            Success = false,
                                            Message = $"Conversion not possible for {item.ProductName}. Please contact admin to fix UOM mapping."
                                        };
                                    }

                                    var baseIsPcs = string.Equals(baseName?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                    var otherIsPcs = string.Equals(otherName?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                    var mappingReversed = baseIsPcs && !otherIsPcs;
                                    requiredBaseQty = mappingReversed ? (item.Quantity * factor) : (item.Quantity / factor);
                                }
                                else
                                {
                                    return new SaleResult
                                    {
                                        Success = false,
                                        Message = $"Invalid unit '{selectedUomName}' for {item.ProductName}."
                                    };
                                }
                            }
                        }

                        var available = stockRow.PackQty ?? 0m;
                        if (available < requiredBaseQty)
                        {
                            return new SaleResult
                            {
                                Success = false,
                                Message = $"Insufficient stock for {item.ProductName} / {item.BatchNumber}. Available {available:0.##}, Required {requiredBaseQty:0.##}"
                            };
                        }

                        stockRow.PackQty = available - requiredBaseQty;
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
                        includedTax = Math.Round(includedTax, 2, MidpointRounding.AwayFromZero);
                        decimal taxable = afterAllDiscounts - includedTax;
                        decimal total = afterAllDiscounts;

                        var selectedUomName = (item.SaleUomName ?? item.UomName ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(selectedUomName)) selectedUomName = item.UomName;

                        var productIdStr = item.ProductId.ToString();
                        var pm = await _context.ProductMasters
                            .AsNoTracking()
                            .FirstOrDefaultAsync(p => p.UniqueID == productIdStr);

                        decimal requiredBaseQty;
                        if (pm == null)
                        {
                            requiredBaseQty = item.Quantity;
                        }
                        else
                        {
                            var baseUomId = (pm.UOMID ?? string.Empty).Trim();
                            var otherUomId = (pm.OtherUOMID ?? string.Empty).Trim();
                            var factor = pm.ConversionFactor.GetValueOrDefault(1m);
                            if (factor <= 0) factor = 1m;

                            var uomIds = new[] { baseUomId, otherUomId }
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            var uomNameById = uomIds.Count == 0
                                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                : await _context.UomMasters
                                    .AsNoTracking()
                                    .Where(u => uomIds.Contains(u.UniqueID))
                                    .Select(u => new { u.UniqueID, u.UOMName })
                                    .ToDictionaryAsync(x => x.UniqueID, x => x.UOMName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                            var baseName = (!string.IsNullOrWhiteSpace(baseUomId) && uomNameById.TryGetValue(baseUomId, out var bn)) ? bn : string.Empty;
                            var otherName = (!string.IsNullOrWhiteSpace(otherUomId) && uomNameById.TryGetValue(otherUomId, out var on)) ? on : string.Empty;

                            bool Matches(string a, string b) =>
                                !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                                string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

                            var isPcsStripPair =
                                (Matches(baseName, "PCS") && Matches(otherName, "STRIP")) ||
                                (Matches(baseName, "STRIP") && Matches(otherName, "PCS"));

                            if (isPcsStripPair)
                            {
                                if (Matches(baseName, "PCS"))
                                {
                                    if (Matches(selectedUomName, "PCS"))
                                    {
                                        requiredBaseQty = item.Quantity;
                                    }
                                    else if (Matches(selectedUomName, "STRIP"))
                                    {
                                        requiredBaseQty = item.Quantity * factor;
                                    }
                                    else
                                    {
                                        requiredBaseQty = item.Quantity;
                                    }
                                }
                                else if (Matches(baseName, "STRIP"))
                                {
                                    if (Matches(selectedUomName, "STRIP"))
                                    {
                                        requiredBaseQty = item.Quantity;
                                    }
                                    else if (Matches(selectedUomName, "PCS"))
                                    {
                                        requiredBaseQty = (factor > 0) ? (item.Quantity / factor) : item.Quantity;
                                    }
                                    else
                                    {
                                        requiredBaseQty = item.Quantity;
                                    }
                                }
                                else
                                {
                                    requiredBaseQty = item.Quantity;
                                }
                            }
                            else
                            {

                                if (Matches(selectedUomName, baseName) || Matches(selectedUomName, item.UomName))
                                {
                                    requiredBaseQty = item.Quantity;
                                }
                                else if (!string.IsNullOrWhiteSpace(otherName) && Matches(selectedUomName, otherName))
                                {
                                    if (factor != 1m && !string.IsNullOrWhiteSpace(baseUomId) && Matches(baseUomId, otherUomId))
                                    {
                                        return new SaleResult
                                        {
                                            Success = false,
                                            Message = $"Conversion not possible for {item.ProductName}. Please contact admin to fix UOM mapping."
                                        };
                                    }

                                    var baseIsPcs = string.Equals(baseName?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                    var otherIsPcs = string.Equals(otherName?.Trim(), "PCS", StringComparison.OrdinalIgnoreCase);
                                    var mappingReversed = baseIsPcs && !otherIsPcs;
                                    requiredBaseQty = mappingReversed ? (item.Quantity * factor) : (item.Quantity / factor);
                                }
                                else
                                {
                                    requiredBaseQty = item.Quantity;
                                }
                            }
                        }

                        var halfTax = Math.Round(includedTax / 2, 2, MidpointRounding.AwayFromZero);
                        var otherHalfTax = Math.Round(includedTax - halfTax, 2, MidpointRounding.AwayFromZero);

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
                            Qty = requiredBaseQty,
                            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                            BaseUOMID = null,
                            SaleUOMID = selectedUomName,
                            SaleUOMQty = item.Quantity,
                            ItemDiscPerc = Math.Round(item.DiscountPercent, 2),
                            ItemDiscAmount = Math.Round(discountAmt, 2),
                            TaxableAmount = taxable,
                            CGSTAmount = halfTax,
                            SGSTAmount = otherHalfTax,
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
                var inner = ex.InnerException?.Message;
                return new SaleResult { Success = false, Message = $"Error completing sale: {ex.Message}{(string.IsNullOrWhiteSpace(inner) ? string.Empty : " | " + inner)}" };
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
                    Quantity = (int)Math.Round(d.SaleUOMQty ?? d.Qty ?? 0, 0),
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
