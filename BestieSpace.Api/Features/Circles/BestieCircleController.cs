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
public class BestieCircleController : ControllerBase
{
    private readonly IBestieCircleService _circleService;
    private readonly ILogger<BestieCircleController> _logger;

    public BestieCircleController(
        IBestieCircleService circleService,
        ILogger<BestieCircleController> logger)
    {
        _circleService = circleService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : null;
    }

    [HttpPost]
    [Authorize(Policy = "CIRCLE_CREATE")]
    public async Task<ActionResult<CircleDetailDto>> CreateCircle([FromBody] CreateCircleRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var created = await _circleService.CreateCircleAsync(currentUserId.Value, request);
            return CreatedAtAction(nameof(GetCircleDetails), new { id = created.CircleId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create circle");
            return BadRequest(new { message = "Could not create circle. Please try again." });
        }
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<CircleDto>>> GetMyCircles()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var circles = await _circleService.GetMyCirclesAsync(currentUserId.Value);
        return Ok(circles);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CircleDetailDto>> GetCircleDetails(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var details = await _circleService.GetCircleDetailsAsync(id, currentUserId.Value);
        if (details == null)
        {
            return NotFound(new { message = "Circle not found or you are not a member of this private circle." });
        }

        return Ok(details);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CircleDetailDto>> UpdateCircle(int id, [FromBody] UpdateCircleRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var updated = await _circleService.UpdateCircleAsync(id, request, currentUserId.Value);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating circle {CircleId}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<CircleOperationResultDto>> DeleteCircle(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        var result = await _circleService.DeactivateOrDeleteCircleAsync(id, currentUserId.Value);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("{id:int}/member-count")]
    public async Task<ActionResult<object>> GetCircleMemberCount(int id)
    {
        var count = await _circleService.GetCircleMemberCountAsync(id);
        return Ok(new { circleId = id, memberCount = count, maxMembers = 9 });
    }
}
