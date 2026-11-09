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
[Route("api/circles/{circleId:int}")]
[Authorize]
public class CircleChatController : ControllerBase
{
    private readonly ICircleChatService _chatService;
    private readonly ILogger<CircleChatController> _logger;

    public CircleChatController(ICircleChatService chatService, ILogger<CircleChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpGet("messages")]
    public async Task<ActionResult<CircleChatPaginationDto>> GetMessages(
        int circleId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 30)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var result = await _chatService.GetCircleMessagesAsync(circleId, pageNumber, pageSize, currentUserId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("messages")]
    public async Task<ActionResult<CircleChatMessageDto>> SendMessage(
        int circleId,
        [FromBody] SendCircleMessageRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "Message content cannot be empty." });
        }

        try
        {
            var message = await _chatService.SendCircleMessageAsync(circleId, request.Content, request.MessageType, currentUserId.Value);
            return Ok(message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("messages/{messageId:int}")]
    public async Task<ActionResult<CircleChatMessageDto>> EditMessage(
        int circleId,
        int messageId,
        [FromBody] EditCircleMessageRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "Message content cannot be empty." });
        }

        try
        {
            var updated = await _chatService.EditCircleMessageAsync(messageId, circleId, request.Content, currentUserId.Value);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing message {MessageId} in circle {CircleId}", messageId, circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("messages/{messageId:int}")]
    public async Task<ActionResult> DeleteMessage(int circleId, int messageId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            await _chatService.DeleteCircleMessageAsync(messageId, circleId, currentUserId.Value);
            return Ok(new { success = true, messageId, circleId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId} in circle {CircleId}", messageId, circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("presence")]
    public async Task<ActionResult<List<CircleMemberPresenceEventDto>>> GetPresence(int circleId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var presence = await _chatService.GetCirclePresenceAsync(circleId, currentUserId.Value);
            return Ok(presence);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting presence for circle {CircleId}", circleId);
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
