using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Media;

[ApiController]
[Route("api/circles/{circleId:int}/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IChatMediaService _chatMediaService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IChatMediaService chatMediaService, ILogger<MediaController> logger)
    {
        _chatMediaService = chatMediaService;
        _logger = logger;
    }

    [HttpGet("{mediaId:int}")]
    public async Task<IActionResult> GetMedia(int circleId, int mediaId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var (stream, contentType, fileName) = await _chatMediaService.GetMediaForStreamingAsync(circleId, mediaId, currentUserId.Value, isThumbnail: false);
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming media {MediaId} in circle {CircleId}", mediaId, circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{mediaId:int}/thumb")]
    public async Task<IActionResult> GetThumbnail(int circleId, int mediaId)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var (stream, contentType, fileName) = await _chatMediaService.GetMediaForStreamingAsync(circleId, mediaId, currentUserId.Value, isThumbnail: true);
            return File(stream, contentType);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming thumbnail for media {MediaId} in circle {CircleId}", mediaId, circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CircleChatMessageDto>> UploadPhoto(
        int circleId,
        [FromForm] IFormFile? file,
        [FromForm] string? caption)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No image file provided." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var message = await _chatMediaService.SendPhotoMessageAsync(circleId, stream, file.FileName, caption, currentUserId.Value);
            return Ok(message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
            _logger.LogError(ex, "Failed to upload photo for circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("photo-base64")]
    [Consumes("application/json")]
    public async Task<ActionResult<CircleChatMessageDto>> UploadPhotoBase64(
        int circleId,
        [FromBody] SendPhotoBase64RequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Base64Data))
        {
            return BadRequest(new { message = "No image data provided." });
        }

        try
        {
            var rawBase64 = request.Base64Data;
            var commaIdx = rawBase64.IndexOf(',');
            if (commaIdx >= 0)
            {
                rawBase64 = rawBase64.Substring(commaIdx + 1);
            }

            var bytes = Convert.FromBase64String(rawBase64);
            using var stream = new MemoryStream(bytes);
            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "photo.jpg" : request.FileName;
            var message = await _chatMediaService.SendPhotoMessageAsync(circleId, stream, fileName, request.Caption, currentUserId.Value);
            return Ok(message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
            _logger.LogError(ex, "Failed to upload base64 photo for circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("voice")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CircleChatMessageDto>> UploadVoice(
        int circleId,
        [FromForm] IFormFile? file,
        [FromForm] double durationSeconds)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No voice audio file provided." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var message = await _chatMediaService.SendVoiceMessageAsync(circleId, stream, file.FileName, durationSeconds, currentUserId.Value);
            return Ok(message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
            _logger.LogError(ex, "Failed to upload voice note for circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("voice-base64")]
    [Consumes("application/json")]
    public async Task<ActionResult<CircleChatMessageDto>> UploadVoiceBase64(
        int circleId,
        [FromBody] SendVoiceBase64RequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Base64Data))
        {
            return BadRequest(new { message = "No voice data provided." });
        }

        try
        {
            var rawBase64 = request.Base64Data;
            var commaIdx = rawBase64.IndexOf(',');
            if (commaIdx >= 0)
            {
                rawBase64 = rawBase64.Substring(commaIdx + 1);
            }

            var bytes = Convert.FromBase64String(rawBase64);
            using var stream = new MemoryStream(bytes);
            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "voice.webm" : request.FileName;
            var message = await _chatMediaService.SendVoiceMessageAsync(circleId, stream, fileName, request.DurationSeconds, currentUserId.Value);
            return Ok(message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
            _logger.LogError(ex, "Failed to upload base64 voice note for circle {CircleId}", circleId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("sticker")]
    public async Task<ActionResult<CircleChatMessageDto>> SendSticker(
        int circleId,
        [FromBody] SendStickerMessageRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        try
        {
            var message = await _chatMediaService.SendStickerMessageAsync(circleId, request.StickerId, currentUserId.Value);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sticker in circle {CircleId}", circleId);
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
