using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rxnxt.Services.Dtos;
using Rxnxt.Services.Implementations;

namespace Rxnxt.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var data = await _dashboardService.GetDashboardDataAsync();
        ViewData["Title"] = "Dashboard";
        return View(data);
    }
}
