using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BestieSpace.Api.Features.Dashboard;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard()
    {
        var data = await _dashboardService.GetDashboardDataAsync();
        return Ok(data);
    }

    [HttpPost("miss-you")]
    public async Task<ActionResult<MissYouResponse>> IncrementMissYou()
    {
        var result = await _dashboardService.IncrementMissYouCountAsync();
        return Ok(result);
    }
}
