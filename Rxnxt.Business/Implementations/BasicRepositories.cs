using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Rxnxt.Business.Data;
using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;

namespace Rxnxt.Business.Implementations;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly PharmacyDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;

    public CustomerRepository(PharmacyDbContext context, ITenantProvider tenantProvider, IConfiguration configuration)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _configuration = configuration;
    }

    public async Task<List<CustomerSearchResult>> SearchAsync(string query)
    {
        var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        if (salesIntegrationEnabled)
        {
            var q = (query ?? string.Empty).Trim();
            if (q.Length < 2) return new List<CustomerSearchResult>();
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

        var localTerm = (query ?? string.Empty).Trim().ToLowerInvariant();
        if (localTerm.Length < 2) return new List<CustomerSearchResult>();

        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.Name.ToLower().Contains(localTerm)
                     || c.Phone.Contains(localTerm)
                     || (c.Email != null && c.Email.ToLower().Contains(localTerm)))
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

        return await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        if (salesIntegrationEnabled)
        {
            var tenantId = _tenantProvider.GetTenantId();
            var createdBy = _configuration["SalesIntegration:CreatedBy"] ?? "POS";
            var now = DateTime.Now;

            var name = (customer.Name ?? string.Empty).Trim();
            if (name.Length > 300) name = name[..300];
            var phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();

            var row = new CustomerMasterRow
            {
                UniqueID = Guid.NewGuid().ToString(),
                CustomerCode = "CUST-0",
                CustomerName = string.IsNullOrWhiteSpace(name) ? "Walk-in" : name,
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
        var p = (phone ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(p)) return false;

        var salesIntegrationEnabled = string.Equals(_configuration["SalesIntegration:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        if (salesIntegrationEnabled)
        {
            return await _context.CustomerMasters.AsNoTracking().AnyAsync(c => c.MobileNumber == p);
        }

        return await _context.Customers.AsNoTracking().AnyAsync(c => c.Phone == p);
    }
}

public sealed class MedicineRepository : IMedicineRepository
{
    private readonly PharmacyDbContext _context;

    public MedicineRepository(PharmacyDbContext context) => _context = context;

    public async Task<List<MedicineSearchResult>> SearchAsync(string query)
    {
        query = (query ?? string.Empty).Trim().ToLowerInvariant();
        if (query.Length < 2) return new List<MedicineSearchResult>();

        return await _context.Medicines
            .AsNoTracking()
            .Where(m => m.Name.ToLower().Contains(query)
                     || (m.GenericName != null && m.GenericName.ToLower().Contains(query))
                     || (m.Manufacturer != null && m.Manufacturer.ToLower().Contains(query)))
            .OrderBy(m => m.Name)
            .Select(m => new MedicineSearchResult
            {
                Id = m.Id,
                Name = m.Name,
                GenericName = m.GenericName,
                Manufacturer = m.Manufacturer,
                Category = m.Category,
                Batches = new List<BatchSearchResult>()
            })
            .Take(10)
            .ToListAsync();
    }

    public async Task<Medicine?> GetByIdAsync(int id) =>
        await _context.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
}

public sealed class BatchRepository : IBatchRepository
{
    private readonly PharmacyDbContext _context;

    public BatchRepository(PharmacyDbContext context) => _context = context;

    private static BatchSearchResult ToDto(Batch b, Medicine? m)
    {
        var today = DateTime.Today;
        return new BatchSearchResult
        {
            Id = b.Id,
            MedicineId = b.MedicineId,
            MedicineName = m?.Name ?? string.Empty,
            GenericName = m?.GenericName,
            BatchNumber = b.BatchNumber,
            ExpiryDate = b.ExpiryDate,
            StripQuantity = b.StripQuantity,
            TabletPerStrip = b.TabletPerStrip,
            SellingPriceStrip = b.SellingPriceStrip,
            SellingPriceTablet = b.SellingPriceTablet,
            Manufacturer = m?.Manufacturer,
            IsExpired = b.ExpiryDate.Date < today,
            IsNearExpiry = b.ExpiryDate.Date >= today && b.ExpiryDate.Date <= today.AddDays(90),
            TotalTablets = b.StripQuantity * b.TabletPerStrip
        };
    }

    public async Task<List<BatchSearchResult>> SearchByBatchNumberAsync(string batchNumber)
    {
        var q = (batchNumber ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length < 2) return new List<BatchSearchResult>();

        return await _context.Batches
            .AsNoTracking()
            .Include(b => b.Medicine)
            .Where(b => b.BatchNumber.ToLower().Contains(q))
            .OrderBy(b => b.BatchNumber)
            .Select(b => ToDto(b, b.Medicine))
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<BatchSearchResult>> SearchByMedicineAsync(string query)
    {
        var q = (query ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length < 2) return new List<BatchSearchResult>();

        return await _context.Batches
            .AsNoTracking()
            .Include(b => b.Medicine)
            .Where(b => b.Medicine != null && b.Medicine.Name.ToLower().Contains(q))
            .OrderBy(b => b.BatchNumber)
            .Select(b => ToDto(b, b.Medicine))
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<BatchSearchResult>> GetBatchesByMedicineIdAsync(int medicineId)
    {
        if (medicineId <= 0) return new List<BatchSearchResult>();

        return await _context.Batches
            .AsNoTracking()
            .Include(b => b.Medicine)
            .Where(b => b.MedicineId == medicineId)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => ToDto(b, b.Medicine))
            .ToListAsync();
    }

    public async Task<BatchSearchResult?> GetByIdAsync(int id)
    {
        if (id <= 0) return null;

        var b = await _context.Batches
            .AsNoTracking()
            .Include(x => x.Medicine)
            .FirstOrDefaultAsync(x => x.Id == id);

        return b == null ? null : ToDto(b, b.Medicine);
    }

    public async Task<List<BatchSearchResult>> AdvancedSearchAsync(
        string? batchNumber,
        string? medicineName,
        string? composition,
        DateTime? expiryFrom,
        DateTime? expiryTo)
    {
        var qBatch = (batchNumber ?? string.Empty).Trim().ToLowerInvariant();
        var qMed = (medicineName ?? string.Empty).Trim().ToLowerInvariant();
        var qComp = (composition ?? string.Empty).Trim().ToLowerInvariant();

        var query = _context.Batches
            .AsNoTracking()
            .Include(b => b.Medicine)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(qBatch))
            query = query.Where(b => b.BatchNumber.ToLower().Contains(qBatch));

        if (!string.IsNullOrWhiteSpace(qMed))
            query = query.Where(b => b.Medicine != null && b.Medicine.Name.ToLower().Contains(qMed));

        if (!string.IsNullOrWhiteSpace(qComp))
            query = query.Where(b => b.Medicine != null && b.Medicine.GenericName != null && b.Medicine.GenericName.ToLower().Contains(qComp));

        if (expiryFrom.HasValue)
            query = query.Where(b => b.ExpiryDate.Date >= expiryFrom.Value.Date);

        if (expiryTo.HasValue)
            query = query.Where(b => b.ExpiryDate.Date <= expiryTo.Value.Date);

        return await query
            .OrderBy(b => b.ExpiryDate)
            .Take(50)
            .Select(b => ToDto(b, b.Medicine))
            .ToListAsync();
    }
}
