using System;
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
public class FriendshipController : ControllerBase
{
    private readonly IFriendshipService _friendshipService;
    private readonly ILogger<FriendshipController> _logger;

    public FriendshipController(
        IFriendshipService friendshipService,
        ILogger<FriendshipController> logger)
    {
        _friendshipService = friendshipService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    [HttpPost("request")]
    [Authorize(Policy = "FRIEND_REQUEST_SEND")]
    public async Task<ActionResult<SocialOperationResultDto>> SendFriendRequest([FromBody] SendFriendRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (request.TargetUserId <= 0)
        {
            return BadRequest(new SocialOperationResultDto { IsSuccess = false, Message = "Invalid target user ID." });
        }

        var result = await _friendshipService.SendFriendRequestAsync(currentUserId.Value, request.TargetUserId);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = "FRIEND_REQUEST_ACCEPT")]
    public async Task<ActionResult<SocialOperationResultDto>> AcceptFriendRequest(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _friendshipService.AcceptFriendRequestAsync(currentUserId.Value, id);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "FRIEND_REQUEST_REJECT")]
    public async Task<ActionResult<SocialOperationResultDto>> RejectFriendRequest(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _friendshipService.RejectFriendRequestAsync(currentUserId.Value, id);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpDelete("{id:int}/cancel")]
    [Authorize(Policy = "FRIEND_REQUEST_CANCEL")]
    public async Task<ActionResult<SocialOperationResultDto>> CancelFriendRequest(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _friendshipService.CancelFriendRequestAsync(currentUserId.Value, id);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpDelete("{id:int}/remove")]
    [Authorize(Policy = "FRIEND_REMOVE")]
    public async Task<ActionResult<SocialOperationResultDto>> RemoveFriend(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _friendshipService.RemoveFriendAsync(currentUserId.Value, id);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("incoming")]
    [Authorize(Policy = "PROFILE_VIEW")]
    public async Task<ActionResult<List<FriendshipDto>>> GetIncomingFriendRequests()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var requests = await _friendshipService.GetIncomingFriendRequestsAsync(currentUserId.Value);
        return Ok(requests);
    }

    [HttpGet("outgoing")]
    [Authorize(Policy = "PROFILE_VIEW")]
    public async Task<ActionResult<List<FriendshipDto>>> GetOutgoingFriendRequests()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var requests = await _friendshipService.GetOutgoingFriendRequestsAsync(currentUserId.Value);
        return Ok(requests);
    }

    [HttpGet("friends")]
    [Authorize(Policy = "PROFILE_VIEW")]
    public async Task<ActionResult<List<FriendshipDto>>> GetFriendsList([FromQuery] string? query = null)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var friends = await _friendshipService.GetFriendsListAsync(currentUserId.Value, query);
        return Ok(friends);
    }

    [HttpGet("status/{targetUserId:int}")]
    [Authorize(Policy = "PROFILE_VIEW")]
    public async Task<ActionResult<object>> GetFriendshipStatus(int targetUserId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var status = await _friendshipService.GetFriendshipStatusAsync(currentUserId.Value, targetUserId);
        return Ok(new { status });
    }
}
