using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rxnxt.Services.Implementations;
using Rxnxt.Web.ViewModels;
using System.Linq;
using System.Threading;

namespace Rxnxt.Web.Controllers
{
    [Authorize]
    public sealed class PurchaseController : Controller
    {
        private readonly StockService _stockService;

        public PurchaseController(StockService stockService)
        {
            _stockService = stockService;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var stocks = await _stockService.GetStocksAsync(cancellationToken);
            var vm = new PurchaseViewModel
            {
                PrefetchedStocks = stocks.Select(StockSearchItemViewModel.FromDto).ToList()
            };
            return View(vm);
        }
    }
}
