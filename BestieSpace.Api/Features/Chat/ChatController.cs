using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BestieSpace.Api.Features.Chat;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet("messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages([FromQuery] string? viewer = null)
    {
        var messages = await _chatService.GetMessagesAsync(viewer);
        return Ok(messages);
    }

    [HttpGet("presence/{roleKey}")]
    public async Task<ActionResult<UserPresenceDto>> GetPresence(string roleKey)
    {
        var presence = await _chatService.GetPresenceAsync(roleKey);
        return Ok(presence);
    }

    [HttpPost("mark-read")]
    public async Task<ActionResult> MarkRead([FromBody] MarkMessagesReadRequest request)
    {
        if (request == null || request.MessageIds == null || !request.MessageIds.Any())
        {
            return BadRequest(new { message = "At least one message ID must be provided." });
        }

        var updatedIds = await _chatService.MarkMessagesReadAsync(request);
        return Ok(new { success = true, markedCount = updatedIds.Count, updatedIds });
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatMessageDto>> SendTextOrSticker([FromBody] SendTextOrStickerRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "Message content cannot be empty." });
        }

        var message = await _chatService.SendTextOrStickerAsync(request);
        return Ok(message);
    }

    [HttpPost("upload-photo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ChatMessageDto>> UploadPhoto(
        [FromForm] IFormFile? photo,
        [FromForm] string? senderName)
    {
        if (photo == null || photo.Length == 0)
        {
            return BadRequest(new { message = "Photo file is required." });
        }

        var message = await _chatService.UploadPhotoAsync(photo, senderName ?? "Me 🎀");
        return Ok(message);
    }

    [HttpPost("upload-voice")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ChatMessageDto>> UploadVoice(
        [FromForm] IFormFile? voice,
        [FromForm] string? senderName,
        [FromForm] double? durationSeconds)
    {
        if (voice == null || voice.Length == 0)
        {
            return BadRequest(new { message = "Voice audio file is required." });
        }

        var message = await _chatService.UploadVoiceAsync(voice, senderName ?? "Me 🎀", durationSeconds);
        return Ok(message);
    }
}
