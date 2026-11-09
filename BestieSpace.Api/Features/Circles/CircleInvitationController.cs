using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Circles;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CircleInvitationController : ControllerBase
{
    private readonly ICircleInvitationService _invitationService;
    private readonly ILogger<CircleInvitationController> _logger;

    public CircleInvitationController(
        ICircleInvitationService invitationService,
        ILogger<CircleInvitationController> logger)
    {
        _invitationService = invitationService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    [HttpPost]
    public async Task<ActionResult<CircleOperationResultDto>> SendInvitation([FromBody] SendCircleInvitationRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _invitationService.SendInvitationAsync(currentUserId.Value, request);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:int}/accept")]
    public async Task<ActionResult<CircleOperationResultDto>> AcceptInvitation(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _invitationService.AcceptInvitationAsync(id, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<CircleOperationResultDto>> RejectInvitation(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _invitationService.RejectInvitationAsync(id, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpDelete("{id:int}/cancel")]
    public async Task<ActionResult<CircleOperationResultDto>> CancelInvitation(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _invitationService.CancelInvitationAsync(id, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("incoming")]
    public async Task<ActionResult<List<CircleInvitationDto>>> GetIncomingInvitations()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var invites = await _invitationService.GetIncomingInvitationsAsync(currentUserId.Value);
        return Ok(invites);
    }

    [HttpGet("circle/{circleId:int}")]
    public async Task<ActionResult<List<CircleInvitationDto>>> GetCircleInvitations(int circleId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var invites = await _invitationService.GetOutgoingInvitationsAsync(circleId, currentUserId.Value);
        return Ok(invites);
    }
}
