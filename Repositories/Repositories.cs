using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Domain.Models;
using Rxnxt.Shared;

namespace PharmacySalesApp.Repositories
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
            .Take(20)
            .ToListAsync();
        }
    }

    public class SaleRepository : ISaleRepository
    {
        private readonly PharmacyDbContext _context;
        public SaleRepository(PharmacyDbContext context) => _context = context;

        public async Task<SaleResult> CompleteSaleAsync(CompleteSaleRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal totalTax = 0;
                decimal subTotal = 0;
                decimal totalItemDiscount = 0;
                var saleItems = new List<SaleItem>();

                foreach (var item in request.Items)
                {
                    var batch = await _context.Batches.FindAsync(item.BatchId);
                    if (batch == null)
                        return new SaleResult { Success = false, Message = $"Batch not found: {item.BatchId}" };

                    // Check stock
                    int requiredStrips = item.UnitType == "Strip" ? item.Quantity :
                        (int)Math.Ceiling((double)item.Quantity / batch.TabletPerStrip);

                    if (batch.StripQuantity < requiredStrips)
                        return new SaleResult
                        {
                            Success = false,
                            Message = $"Insufficient stock for batch {batch.BatchNumber}. Available: {batch.StripQuantity} strips"
                        };

                    decimal unitPrice = item.UnitType == "Strip" ? batch.SellingPriceStrip : batch.SellingPriceTablet;
                    decimal lineTotal = unitPrice * item.Quantity;

                    decimal discountAmt = lineTotal * item.DiscountPercent / 100;
                    decimal taxable = lineTotal - discountAmt;
                    decimal taxAmt = taxable * item.TaxPercent / 100;
                    decimal netTotal = taxable + taxAmt;

                    saleItems.Add(new SaleItem
                    {
                        BatchId = item.BatchId,
                        Quantity = item.Quantity,
                        UnitType = item.UnitType,
                        Price = unitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountAmount = discountAmt,
                        TaxPercent = item.TaxPercent,
                        TaxAmount = taxAmt,
                        Total = netTotal
                    });

                    subTotal += lineTotal;
                    totalItemDiscount += discountAmt;
                    totalTax += taxAmt;

                    // Deduct stock
                    if (item.UnitType == "Strip")
                    {
                        batch.StripQuantity -= item.Quantity;
                    }
                    else
                    {
                        int tabletsNeeded = item.Quantity;
                        int stripsToDeduct = tabletsNeeded / batch.TabletPerStrip;
                        int remainingTablets = tabletsNeeded % batch.TabletPerStrip;
                        batch.StripQuantity -= stripsToDeduct;
                        if (remainingTablets > 0)
                            batch.StripQuantity -= 1; // partial strip
                    }
                }

                decimal grandTotal = subTotal - totalItemDiscount - request.AdditionalDiscount + totalTax;

                // Generate invoice number
                var saleCount = await _context.Sales.CountAsync() + 1;
                string invoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{saleCount:D4}";

                var sale = new Sale
                {
                    CustomerId = request.CustomerId,
                    SaleDate = DateTime.Now,
                    SubTotal = subTotal,
                    ItemDiscount = totalItemDiscount,
                    AdditionalDiscount = request.AdditionalDiscount,
                    GrandTotal = grandTotal,
                    PaymentStatus = "Completed",
                    InvoiceNumber = invoiceNumber,
                    SaleItems = saleItems
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Add payments
                foreach (var payment in request.Payments)
                {
                    _context.Payments.Add(new Payment
                    {
                        SaleId = sale.Id,
                        PaymentMode = payment.PaymentMode,
                        Amount = payment.Amount,
                        Reference = payment.Reference,
                        Status = "Completed",
                        PaymentDate = DateTime.Now
                    });
                }

                // Add loyalty points if customer selected
                if (request.CustomerId.HasValue)
                {
                    var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                    if (customer != null)
                    {
                        customer.LoyaltyPoints += (int)(grandTotal / 10); // 1 point per ₹10 spent
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new SaleResult
                {
                    Success = true,
                    Message = "Sale completed successfully!",
                    SaleId = sale.Id,
                    InvoiceNumber = invoiceNumber
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new SaleResult { Success = false, Message = $"Error completing sale: {ex.Message}" };
            }
        }

        public async Task<Sale?> GetByIdAsync(int id) =>
            await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Batch).ThenInclude(b => b!.Medicine)
                .Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<List<Sale>> GetRecentSalesAsync(int count = 10) =>
            await _context.Sales
                .Include(s => s.Customer)
                .OrderByDescending(s => s.SaleDate)
                .Take(count)
                .ToListAsync();
    }
}
