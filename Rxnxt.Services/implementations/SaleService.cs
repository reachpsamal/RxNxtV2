using Rxnxt.Business.DTOs;
using Rxnxt.Business.Interfaces;

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
}
