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
public class UserBlockController : ControllerBase
{
    private readonly IUserBlockService _blockService;
    private readonly ILogger<UserBlockController> _logger;

    public UserBlockController(
        IUserBlockService blockService,
        ILogger<UserBlockController> logger)
    {
        _blockService = blockService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    [HttpPost("{targetUserId:int}")]
    [Authorize(Policy = "USER_BLOCK")]
    public async Task<ActionResult<SocialOperationResultDto>> BlockUser(int targetUserId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _blockService.BlockUserAsync(currentUserId.Value, targetUserId);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpDelete("{targetUserId:int}")]
    [Authorize(Policy = "USER_UNBLOCK")]
    public async Task<ActionResult<SocialOperationResultDto>> UnblockUser(int targetUserId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _blockService.UnblockUserAsync(currentUserId.Value, targetUserId);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = "BLOCKED_USERS_VIEW")]
    public async Task<ActionResult<List<UserBlockDto>>> GetBlockedUsers()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var list = await _blockService.GetBlockedUsersAsync(currentUserId.Value);
        return Ok(list);
    }
}
