using Rxnxt.Domain.Models;
using Rxnxt.Business.DTOs;

namespace Rxnxt.Business.Interfaces
{
    public interface ICustomerRepository
    {
        Task<List<CustomerSearchResult>> SearchAsync(string query);
        Task<Customer?> GetByIdAsync(int id);
        Task<Customer> CreateAsync(Customer customer);
        Task<bool> PhoneExistsAsync(string phone);
    }

    public interface IMedicineRepository
    {
        Task<List<MedicineSearchResult>> SearchAsync(string query);
        Task<Medicine?> GetByIdAsync(int id);
    }

    public interface IBatchRepository
    {
        Task<List<BatchSearchResult>> SearchByBatchNumberAsync(string batchNumber);
        Task<List<BatchSearchResult>> SearchByMedicineAsync(string query);
        Task<List<BatchSearchResult>> GetBatchesByMedicineIdAsync(int medicineId);
        Task<BatchSearchResult?> GetByIdAsync(int id);
        Task<List<BatchSearchResult>> AdvancedSearchAsync(string? batchNumber, string? medicineName, string? composition, DateTime? expiryFrom, DateTime? expiryTo);
    }

    public interface ISaleRepository
    {
        Task<SaleResult> CompleteSaleAsync(CompleteSaleRequest request);
        Task<Sale?> GetByIdAsync(int id);
        Task<List<Sale>> GetRecentSalesAsync(int count = 10);
        Task<List<Sale>> SearchSalesAsync(DateTime from, DateTime to, string? q);
        Task<bool> CancelSaleAsync(int id);
    }
}
