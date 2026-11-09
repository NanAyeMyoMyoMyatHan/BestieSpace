using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Social;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserDiscoveryController : ControllerBase
{
    private readonly IUserDiscoveryService _discoveryService;
    private readonly ILogger<UserDiscoveryController> _logger;

    public UserDiscoveryController(
        IUserDiscoveryService discoveryService,
        ILogger<UserDiscoveryController> logger)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    [HttpGet]
    [Authorize(Policy = "USER_SEARCH")]
    public async Task<ActionResult<PagedResultDto<UserDiscoveryCardDto>>> DiscoverUsers(
        [FromQuery] string? query = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _discoveryService.DiscoverUsersAsync(currentUserId.Value, query, pageNumber, pageSize);
        return Ok(result);
    }

    [HttpGet("suggested")]
    [Authorize(Policy = "USER_SEARCH")]
    public async Task<ActionResult<List<UserDiscoveryCardDto>>> GetSuggestedUsers([FromQuery] int limit = 6)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _discoveryService.GetSuggestedUsersAsync(currentUserId.Value, limit);
        return Ok(result);
    }
}
