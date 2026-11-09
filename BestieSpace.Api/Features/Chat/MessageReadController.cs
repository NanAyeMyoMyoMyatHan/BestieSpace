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
[Route("api/circles")]
[Authorize]
public class MessageReadController : ControllerBase
{
    private readonly IMessageReadService _readService;
    private readonly ILogger<MessageReadController> _logger;

    public MessageReadController(IMessageReadService readService, ILogger<MessageReadController> logger)
    {
        _readService = readService;
        _logger = logger;
    }

    [HttpPost("{circleId:int}/messages/read")]
    public async Task<ActionResult<int>> MarkMessagesRead(int circleId, [FromBody] MarkMessagesReadRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var messageIds = request?.MessageIds ?? new List<int>();
            var count = await _readService.MarkMessagesAsReadAsync(circleId, messageIds, currentUserId.Value);
            return Ok(new { markedCount = count });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking messages read for circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{circleId:int}/messages/{messageId:int}/read-receipts")]
    public async Task<ActionResult<MessageReadDetailsDto>> GetReadReceipts(int circleId, int messageId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var details = await _readService.GetMessageReadDetailsAsync(messageId, circleId, currentUserId.Value);
            return Ok(details);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving read receipts for message {MessageId}", messageId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("unread-summary")]
    public async Task<ActionResult<UnreadSummaryDto>> GetUnreadSummary()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var summary = await _readService.GetUnreadSummaryAsync(currentUserId.Value);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread message summary for user {UserId}", currentUserId.Value);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{circleId:int}/unread-count")]
    public async Task<ActionResult<int>> GetCircleUnreadCount(int circleId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var count = await _readService.GetCircleUnreadCountAsync(circleId, currentUserId.Value);
            return Ok(new { circleId, unreadCount = count });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread count for circle {CircleId}", circleId);
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
