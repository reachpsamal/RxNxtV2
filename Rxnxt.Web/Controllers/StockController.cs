//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
//using Rxnxt.Business.DTOs;
//using Rxnxt.Services.Implementations;

//namespace Rxnxt.Web.Controllers;

//public sealed class StockController : Controller
//{
//    private readonly StockService _stockService;

//    public StockController(StockService stockService)
//    {
//        _stockService = stockService;
//    }

//    public async Task<IActionResult> Index()
//    {
//        try
//        {
//            var stocks = await _stockService.GetStocksAsync();
//            var ordered = stocks
//                .OrderBy(s => s.ProductName)
//                .ToList();

//            return View(ordered);
//        }
//        catch (Exception ex)
//        {
//            ViewBag.ErrorMessage = ex.Message;
//            return View(new List<StockDto>());
//        }
//    }
//}

using Microsoft.AspNetCore.Mvc;
using Rxnxt.Services.Implementations;

namespace Rxnxt.Web.Controllers;

[Route("api/stocks")]
[ApiController]
public class StockController : ControllerBase
{
    private readonly StockService _stockService;

    public StockController(StockService stockService)
    {
        _stockService = stockService;
    }

    // GET: api/stocks
    [HttpGet]
    public async Task<IActionResult> GetStocks()
    {
        var stocks = await _stockService.GetStocksAsync();
        return Ok(stocks);
    }

    // GET: api/stocks/search?term=para
    [HttpGet("search")]
    public async Task<IActionResult> Search(string term)
    {
        var stocks = await _stockService.GetStocksAsync();

        var result = stocks
            .Where(x => x.ProductName != null &&
                        x.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        return Ok(result);
    }

    // GET: api/stocks/batch?batch=123
    [HttpGet("batch")]
    public async Task<IActionResult> SearchByBatch(string batch)
    {
        var stocks = await _stockService.GetStocksAsync();

        var result = stocks
            .Where(x => x.BatchNumber != null &&
                        x.BatchNumber.Contains(batch, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        return Ok(result);
    }
}