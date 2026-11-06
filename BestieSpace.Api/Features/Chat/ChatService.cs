using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.Chat;

public interface IChatService
{
    Task<List<ChatMessageDto>> GetMessagesAsync(string? viewer = null);
    Task<ChatMessageDto> SendTextOrStickerAsync(SendTextOrStickerRequest request);
    Task<ChatMessageDto> UploadPhotoAsync(IFormFile photo, string senderName);
    Task<ChatMessageDto> UploadVoiceAsync(IFormFile voiceAudio, string senderName, double? durationSeconds);
    Task<List<int>> MarkMessagesReadAsync(MarkMessagesReadRequest request);
    Task<UserPresenceDto> GetPresenceAsync(string roleKey);
}

public class ChatService : IChatService
{
    private readonly BestieDbContext _context;
    private readonly IHubContext<BestieChatHub> _hubContext;
    private readonly IWebHostEnvironment _environment;

    public ChatService(
        BestieDbContext context,
        IHubContext<BestieChatHub> hubContext,
        IWebHostEnvironment environment)
    {
        _context = context;
        _hubContext = hubContext;
        _environment = environment;
    }

    public async Task<List<ChatMessageDto>> GetMessagesAsync(string? viewer = null)
    {
        return await _context.ChatMessages
            .OrderBy(m => m.Timestamp)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderName = m.SenderName,
                MessageType = m.MessageType,
                Content = m.Content,
                MediaUrl = m.MediaUrl,
                VoiceDurationSeconds = m.VoiceDurationSeconds,
                Timestamp = m.Timestamp,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt
            })
            .ToListAsync();
    }

    public async Task<ChatMessageDto> SendTextOrStickerAsync(SendTextOrStickerRequest request)
    {
        var msgType = request.MessageType?.ToLowerInvariant() == "sticker" ? "sticker" : "text";

        var chatMessage = new ChatMessage
        {
            SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "Me 🎀" : request.SenderName.Trim(),
            MessageType = msgType,
            Content = request.Content?.Trim(),
            MediaUrl = null,
            VoiceDurationSeconds = null,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            ReadAt = null
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        var dto = MapToDto(chatMessage);
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", dto);
        return dto;
    }

    public async Task<ChatMessageDto> UploadPhotoAsync(IFormFile photo, string senderName)
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "chat-photos");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(photo.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await photo.CopyToAsync(stream);
        }

        var chatMessage = new ChatMessage
        {
            SenderName = string.IsNullOrWhiteSpace(senderName) ? "Me 🎀" : senderName.Trim(),
            MessageType = "photo",
            Content = "Shared a cute photo 📸",
            MediaUrl = $"/uploads/chat-photos/{fileName}",
            VoiceDurationSeconds = null,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            ReadAt = null
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        var dto = MapToDto(chatMessage);
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", dto);
        return dto;
    }

    public async Task<ChatMessageDto> UploadVoiceAsync(IFormFile voiceAudio, string senderName, double? durationSeconds)
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "chat-voices");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(voiceAudio.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".webm";
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await voiceAudio.CopyToAsync(stream);
        }

        var chatMessage = new ChatMessage
        {
            SenderName = string.IsNullOrWhiteSpace(senderName) ? "Me 🎀" : senderName.Trim(),
            MessageType = "voice",
            Content = "Voice note 🎤",
            MediaUrl = $"/uploads/chat-voices/{fileName}",
            VoiceDurationSeconds = durationSeconds ?? 0,
            Timestamp = DateTime.UtcNow,
            IsRead = false,
            ReadAt = null
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        var dto = MapToDto(chatMessage);
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", dto);
        return dto;
    }

    public async Task<List<int>> MarkMessagesReadAsync(MarkMessagesReadRequest request)
    {
        if (request.MessageIds == null || !request.MessageIds.Any())
        {
            return new List<int>();
        }

        var now = DateTime.UtcNow;
        var messages = await _context.ChatMessages
            .Where(m => request.MessageIds.Contains(m.Id) && !m.IsRead)
            .ToListAsync();

        if (!messages.Any())
        {
            return new List<int>();
        }

        var updatedIds = new List<int>();
        foreach (var m in messages)
        {
            m.IsRead = true;
            m.ReadAt = now;
            updatedIds.Add(m.Id);
        }

        await _context.SaveChangesAsync();

        var notification = new MessagesReadNotification
        {
            MessageIds = updatedIds,
            ReaderName = request.ReaderName,
            ReadAt = now
        };

        await _hubContext.Clients.All.SendAsync("MessagesRead", notification);
        return updatedIds;
    }

    public async Task<UserPresenceDto> GetPresenceAsync(string roleKey)
    {
        var clean = roleKey.Contains("Bestie", StringComparison.OrdinalIgnoreCase) ? "Bestie" : "Me";
        var user = await _context.Users.FirstOrDefaultAsync(u => u.RoleKey == clean);
        var isOnline = BestieChatHub.IsRoleOnline(clean);

        return new UserPresenceDto
        {
            RoleKey = clean,
            DisplayName = user?.DisplayName ?? (clean == "Bestie" ? "Sweet Bestie 🌸" : "Your Bestie 🎀"),
            IsOnline = isOnline || (user?.IsOnline ?? false),
            LastSeenAt = user?.LastSeenAt
        };
    }

    private static ChatMessageDto MapToDto(ChatMessage m) => new()
    {
        Id = m.Id,
        SenderName = m.SenderName,
        MessageType = m.MessageType,
        Content = m.Content,
        MediaUrl = m.MediaUrl,
        VoiceDurationSeconds = m.VoiceDurationSeconds,
        Timestamp = m.Timestamp,
        IsRead = m.IsRead,
        ReadAt = m.ReadAt
    };
}
