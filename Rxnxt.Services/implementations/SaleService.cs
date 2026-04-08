using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;
using Rxnxt.Domain.Models;

namespace Rxnxt.Services.Implementations;

public sealed class SaleService
{
    private readonly ISaleRepository _saleRepo;

    public SaleService(ISaleRepository saleRepo)
    {
        _saleRepo = saleRepo;
    }

    public async Task<SaleResult> CompleteSaleAsync(CompleteSaleRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return new SaleResult { Success = false, Message = "No items in the sale" };
        }

        return await _saleRepo.CompleteSaleAsync(request);
    }

    public async Task<List<Sale>> SearchSalesAsync(DateTime from, DateTime to, string? q)
    {
        return await _saleRepo.SearchSalesAsync(from, to, q);
    }

    public async Task<Sale?> GetByIdAsync(int id)
    {
        return await _saleRepo.GetByIdAsync(id);
    }

    public async Task<bool> CancelSaleAsync(int id)
    {
        return await _saleRepo.CancelSaleAsync(id);
    }
}
