using Microsoft.EntityFrameworkCore;
using Rxnxt.Business.Data;
using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;

namespace Rxnxt.Business.Implementations
{
    public sealed class SupplierRepository : ISupplierRepository
    {
        private readonly PharmacyDbContext _context;

        public SupplierRepository(PharmacyDbContext context)
        {
            _context = context;
        }

        public async Task<List<SupplierSearchResult>> SearchAsync(string query)
        {
            var term = (query ?? string.Empty).Trim();
            if (term.Length < 2) return new List<SupplierSearchResult>();
            var lower = term.ToLowerInvariant();

            return await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.Name.ToLower().Contains(lower) || (s.Phone != null && s.Phone.Contains(term)))
                .OrderBy(s => s.Name)
                .Take(20)
                .Select(s => new SupplierSearchResult
                {
                    Id = s.Id,
                    Name = s.Name,
                    Phone = s.Phone,
                    Gstin = s.Gstin,
                    Address = s.Address
                })
                .ToListAsync();
        }

        public async Task<Supplier> CreateAsync(Supplier supplier)
        {
            supplier.Name = (supplier.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(supplier.Name))
                throw new InvalidOperationException("Supplier name is required");

            supplier.Phone = string.IsNullOrWhiteSpace(supplier.Phone) ? null : supplier.Phone.Trim();
            supplier.Gstin = string.IsNullOrWhiteSpace(supplier.Gstin) ? null : supplier.Gstin.Trim();
            supplier.Address = string.IsNullOrWhiteSpace(supplier.Address) ? null : supplier.Address.Trim();
            supplier.CreatedDate = DateTime.Now;

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return supplier;
        }

        public async Task<Supplier?> GetByIdAsync(int id)
        {
            return await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        }
    }
}
