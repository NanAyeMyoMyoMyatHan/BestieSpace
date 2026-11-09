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
public class CircleMemberController : ControllerBase
{
    private readonly ICircleMemberService _memberService;
    private readonly ILogger<CircleMemberController> _logger;

    public CircleMemberController(
        ICircleMemberService memberService,
        ILogger<CircleMemberController> logger)
    {
        _memberService = memberService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    [HttpGet("circle/{circleId:int}")]
    public async Task<ActionResult<List<CircleMemberDto>>> GetCircleMembers(int circleId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var members = await _memberService.GetCircleMembersAsync(circleId, currentUserId.Value);
        return Ok(members);
    }

    [HttpDelete("circle/{circleId:int}/user/{userId:int}")]
    public async Task<ActionResult<CircleOperationResultDto>> RemoveMember(int circleId, int userId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _memberService.RemoveMemberAsync(circleId, userId, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("circle/{circleId:int}/leave")]
    public async Task<ActionResult<CircleOperationResultDto>> LeaveCircle(int circleId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _memberService.LeaveCircleAsync(circleId, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPut("circle/{circleId:int}/role")]
    public async Task<ActionResult<CircleOperationResultDto>> ChangeMemberRole(int circleId, [FromBody] ChangeMemberRoleRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _memberService.ChangeMemberRoleAsync(circleId, request, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("circle/{circleId:int}/transfer-ownership")]
    public async Task<ActionResult<CircleOperationResultDto>> TransferOwnership(int circleId, [FromBody] TransferCircleOwnershipRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _memberService.TransferOwnershipAsync(circleId, request, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
