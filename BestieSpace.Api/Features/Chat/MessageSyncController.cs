using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Chat;

[ApiController]
[Route("api/circles/{circleId:int}/messages")]
[Authorize]
public class MessageSyncController : ControllerBase
{
    private readonly IMessageSyncService _syncService;
    private readonly IMessageDeliveryService _deliveryService;
    private readonly ILogger<MessageSyncController> _logger;

    public MessageSyncController(
        IMessageSyncService syncService,
        IMessageDeliveryService deliveryService,
        ILogger<MessageSyncController> logger)
    {
        _syncService = syncService;
        _deliveryService = deliveryService;
        _logger = logger;
    }

    [HttpGet("sync")]
    public async Task<ActionResult<MessageSyncResponseDto>> SyncMessages(
        int circleId,
        [FromQuery] int afterMessageId = 0,
        [FromQuery] int pageSize = 50)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var result = await _syncService.SyncMessagesAsync(circleId, afterMessageId, pageSize, currentUserId.Value);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing messages for circle {CircleId} after {AfterMessageId}", circleId, afterMessageId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("delivery")]
    public async Task<ActionResult<int>> AcknowledgeDelivery(
        int circleId,
        [FromBody] AcknowledgeDeliveryRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var messageIds = request?.MessageIds ?? new List<int>();
            var count = await _deliveryService.AcknowledgeDeliveryAsync(circleId, messageIds, currentUserId.Value);
            return Ok(new { acknowledgedCount = count });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging delivery for circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("userId")?.Value;

        if (int.TryParse(claim, out var uid))
        {
            return uid;
        }
        return null;
    }
}
