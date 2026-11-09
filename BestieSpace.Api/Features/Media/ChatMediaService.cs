using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Features.Chat;
using BestieSpace.Api.Features.Circles;
using BestieSpace.Api.Features.Notifications;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Media;

public interface IChatMediaService
{
    Task<CircleChatMessageDto> SendPhotoMessageAsync(int circleId, Stream imageStream, string originalFileName, string? caption, int senderUserId);
    Task<CircleChatMessageDto> SendVoiceMessageAsync(int circleId, Stream voiceStream, string originalFileName, double durationSeconds, int senderUserId);
    Task<CircleChatMessageDto> SendStickerMessageAsync(int circleId, int stickerId, int senderUserId);
    Task<(Stream stream, string contentType, string fileName)> GetMediaForStreamingAsync(int circleId, int mediaId, int requestingUserId, bool isThumbnail = false);
}

public class ChatMediaService : IChatMediaService
{
    private readonly BestieDbContext _context;
    private readonly IMediaStorageService _storageService;
    private readonly IStickerService _stickerService;
    private readonly ICircleAuthorizationService _circleAuth;
    private readonly IHubContext<BestieChatHub> _hubContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ChatMediaService> _logger;

    public ChatMediaService(
        BestieDbContext context,
        IMediaStorageService storageService,
        IStickerService stickerService,
        ICircleAuthorizationService circleAuth,
        IHubContext<BestieChatHub> hubContext,
        INotificationService notificationService,
        ILogger<ChatMediaService> logger)
    {
        _context = context;
        _storageService = storageService;
        _stickerService = stickerService;
        _circleAuth = circleAuth;
        _hubContext = hubContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<CircleChatMessageDto> SendPhotoMessageAsync(int circleId, Stream imageStream, string originalFileName, string? caption, int senderUserId)
    {
        // 1. Verify active membership & permission
        await ValidateCircleAndPermissionAsync(circleId, senderUserId, "CHAT_SEND_IMAGE");

        // 2. Save physical image and generate thumbnail
        var (storageKey, thumbStorageKey, contentType, fileSize) = await _storageService.SaveImageAsync(imageStream, originalFileName, circleId);

        // 3. Create MediaFile record
        var mediaFile = new MediaFile
        {
            CircleId = circleId,
            UploadedByUserId = senderUserId,
            StorageKey = storageKey,
            ThumbnailStorageKey = thumbStorageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            MediaType = "image",
            FileSize = fileSize,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.MediaFiles.Add(mediaFile);
        await _context.SaveChangesAsync();

        var mediaUrl = $"/api/circles/{circleId}/media/{mediaFile.MediaId}";
        var thumbUrl = $"/api/circles/{circleId}/media/{mediaFile.MediaId}/thumb";

        // 4. Create ChatMessage record
        var sender = await _context.Users.FindAsync(senderUserId);
        var chatMessage = new ChatMessage
        {
            CircleId = circleId,
            SenderId = senderUserId,
            SenderName = sender?.DisplayName ?? $"User {senderUserId}",
            MessageType = "photo",
            Content = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
            MediaUrl = mediaUrl,
            ThumbnailUrl = thumbUrl,
            FileName = originalFileName,
            ContentType = contentType,
            FileSize = fileSize,
            MediaId = mediaFile.MediaId,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        chatMessage.Sender = sender;
        var isModerator = await IsUserModeratorAsync(circleId, senderUserId);
        var dto = MapToDto(chatMessage, senderUserId, isModerator);

        // 5. Broadcast to SignalR group circle_{circleId}
        try
        {
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("ReceiveMessage", dto);
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("MediaUploaded", new
            {
                mediaId = mediaFile.MediaId,
                circleId,
                mediaUrl,
                thumbnailUrl = thumbUrl,
                senderUserId,
                messageId = chatMessage.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast photo message {MessageId} in circle {CircleId}", chatMessage.Id, circleId);
        }

        // 6. Notify offline circle members
        try
        {
            var otherMembers = await _context.BestieCircleMembers
                .Where(cm => cm.CircleId == circleId && cm.IsActive && cm.UserId != senderUserId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            var cap = string.IsNullOrWhiteSpace(caption) ? "📸 Photo" : $"📸 {caption}";
            foreach (var uid in otherMembers)
            {
                await _notificationService.CreateNotificationAsync(uid, "NewMessage", "New Photo Shared 📸", $"{dto.SenderDisplayName}: {cap}", circleId, "Circle");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify members of photo message in circle {CircleId}", circleId);
        }

        return dto;
    }

    public async Task<CircleChatMessageDto> SendVoiceMessageAsync(int circleId, Stream voiceStream, string originalFileName, double durationSeconds, int senderUserId)
    {
        // 1. Verify active membership & permission
        await ValidateCircleAndPermissionAsync(circleId, senderUserId, "CHAT_SEND_VOICE");

        if (durationSeconds <= 0) durationSeconds = 1;
        if (durationSeconds > 120)
        {
            throw new ArgumentException("Voice messages cannot exceed 120 seconds (2 minutes).");
        }

        // 2. Save physical voice file
        var (storageKey, contentType, fileSize) = await _storageService.SaveVoiceAsync(voiceStream, originalFileName, circleId);

        // 3. Create MediaFile record
        var mediaFile = new MediaFile
        {
            CircleId = circleId,
            UploadedByUserId = senderUserId,
            StorageKey = storageKey,
            ThumbnailStorageKey = null,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            MediaType = "voice",
            FileSize = fileSize,
            DurationSeconds = durationSeconds,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.MediaFiles.Add(mediaFile);
        await _context.SaveChangesAsync();

        var mediaUrl = $"/api/circles/{circleId}/media/{mediaFile.MediaId}";

        // 4. Create ChatMessage record
        var sender = await _context.Users.FindAsync(senderUserId);
        var chatMessage = new ChatMessage
        {
            CircleId = circleId,
            SenderId = senderUserId,
            SenderName = sender?.DisplayName ?? $"User {senderUserId}",
            MessageType = "voice",
            Content = "🎤 Voice message",
            MediaUrl = mediaUrl,
            VoiceDurationSeconds = durationSeconds,
            DurationSeconds = durationSeconds,
            FileName = originalFileName,
            ContentType = contentType,
            FileSize = fileSize,
            MediaId = mediaFile.MediaId,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        chatMessage.Sender = sender;
        var isModerator = await IsUserModeratorAsync(circleId, senderUserId);
        var dto = MapToDto(chatMessage, senderUserId, isModerator);

        // 5. Broadcast to SignalR group circle_{circleId}
        try
        {
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("ReceiveMessage", dto);
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("VoiceMessageSent", new
            {
                mediaId = mediaFile.MediaId,
                circleId,
                mediaUrl,
                durationSeconds,
                senderUserId,
                messageId = chatMessage.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast voice message {MessageId} in circle {CircleId}", chatMessage.Id, circleId);
        }

        // 6. Notify offline circle members
        try
        {
            var otherMembers = await _context.BestieCircleMembers
                .Where(cm => cm.CircleId == circleId && cm.IsActive && cm.UserId != senderUserId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            foreach (var uid in otherMembers)
            {
                await _notificationService.CreateNotificationAsync(uid, "NewMessage", "New Voice Note 🎤", $"{dto.SenderDisplayName} sent a voice message ({durationSeconds:0}s)", circleId, "Circle");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify members of voice message in circle {CircleId}", circleId);
        }

        return dto;
    }

    public async Task<CircleChatMessageDto> SendStickerMessageAsync(int circleId, int stickerId, int senderUserId)
    {
        // 1. Verify active membership & permission
        await ValidateCircleAndPermissionAsync(circleId, senderUserId, "CHAT_SEND_STICKER");

        // 2. Fetch sticker
        var sticker = await _stickerService.GetActiveStickerByIdAsync(stickerId);
        if (sticker == null)
        {
            throw new KeyNotFoundException($"Active sticker {stickerId} not found.");
        }

        // 3. Create ChatMessage record
        var sender = await _context.Users.FindAsync(senderUserId);
        var chatMessage = new ChatMessage
        {
            CircleId = circleId,
            SenderId = senderUserId,
            SenderName = sender?.DisplayName ?? $"User {senderUserId}",
            MessageType = "sticker",
            Content = sticker.ImageUrl, // e.g. "🌸" or image url
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        chatMessage.Sender = sender;
        var isModerator = await IsUserModeratorAsync(circleId, senderUserId);
        var dto = MapToDto(chatMessage, senderUserId, isModerator);

        // 4. Broadcast to SignalR group circle_{circleId}
        try
        {
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("ReceiveMessage", dto);
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("StickerSent", new
            {
                circleId,
                stickerId,
                stickerName = sticker.StickerName,
                imageUrl = sticker.ImageUrl,
                senderUserId,
                messageId = chatMessage.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast sticker message {MessageId} in circle {CircleId}", chatMessage.Id, circleId);
        }

        // 5. Notify offline circle members
        try
        {
            var otherMembers = await _context.BestieCircleMembers
                .Where(cm => cm.CircleId == circleId && cm.IsActive && cm.UserId != senderUserId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            foreach (var uid in otherMembers)
            {
                await _notificationService.CreateNotificationAsync(uid, "NewMessage", "New Sticker 🌸", $"{dto.SenderDisplayName} sent a sticker ({sticker.StickerName})", circleId, "Circle");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify members of sticker message in circle {CircleId}", circleId);
        }

        return dto;
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetMediaForStreamingAsync(int circleId, int mediaId, int requestingUserId, bool isThumbnail = false)
    {
        // 1. Strict server-side verification: user must be active member of circle
        var isMember = await _circleAuth.IsCircleMemberAsync(requestingUserId, circleId);
        if (!isMember)
        {
            _logger.LogWarning("Unauthorized attempt by user {UserId} to access media {MediaId} in circle {CircleId}", requestingUserId, mediaId, circleId);
            throw new UnauthorizedAccessException("You are not an active member of this Bestie Circle.");
        }

        // 2. Fetch media record ensuring circle ownership and active status
        var mediaFile = await _context.MediaFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MediaId == mediaId && m.CircleId == circleId && !m.IsDeleted);

        if (mediaFile == null)
        {
            throw new KeyNotFoundException($"Media {mediaId} not found in circle {circleId}.");
        }

        var storageKey = (isThumbnail && !string.IsNullOrEmpty(mediaFile.ThumbnailStorageKey))
            ? mediaFile.ThumbnailStorageKey
            : mediaFile.StorageKey;

        var result = await _storageService.GetMediaStreamAsync(storageKey, mediaFile.OriginalFileName, mediaFile.ContentType);
        if (!result.HasValue)
        {
            throw new FileNotFoundException($"Media file content could not be located.");
        }

        return result.Value;
    }

    private async Task ValidateCircleAndPermissionAsync(int circleId, int userId, string permissionCode)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            throw new KeyNotFoundException($"Circle {circleId} not found or inactive.");
        }

        var isMember = await _circleAuth.IsCircleMemberAsync(userId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        var hasPerm = await _circleAuth.HasCirclePermissionAsync(userId, circleId, permissionCode);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException($"You do not have permission ({permissionCode}) to perform this action.");
        }
    }

    private async Task<bool> IsUserModeratorAsync(int circleId, int userId)
    {
        var canModerate = await _circleAuth.HasCirclePermissionAsync(userId, circleId, "CHAT_MODERATE");
        var isOwner = await _circleAuth.IsCircleOwnerAsync(userId, circleId);
        return canModerate || isOwner;
    }

    private static CircleChatMessageDto MapToDto(ChatMessage m, int requestingUserId, bool isModerator)
    {
        var isOwn = m.SenderId == requestingUserId;
        return new CircleChatMessageDto
        {
            Id = m.Id,
            CircleId = m.CircleId ?? 0,
            SenderId = m.SenderId ?? 0,
            SenderUsername = m.Sender?.Username ?? m.SenderName,
            SenderDisplayName = m.Sender?.DisplayName ?? m.SenderName,
            SenderAvatarSticker = m.Sender?.AvatarSticker,
            SenderProfilePicture = m.Sender?.ProfilePicture,
            MessageType = m.MessageType,
            Content = m.IsDeleted ? "🌸 This message was deleted" : m.Content,
            MediaId = m.MediaId,
            MediaUrl = m.MediaUrl,
            ThumbnailUrl = m.ThumbnailUrl,
            FileName = m.FileName,
            ContentType = m.ContentType,
            FileSize = m.FileSize,
            DurationSeconds = m.VoiceDurationSeconds ?? m.DurationSeconds,
            CreatedAt = m.Timestamp,
            UpdatedAt = m.UpdatedAt,
            IsDeleted = m.IsDeleted,
            IsOwnMessage = isOwn,
            CanEdit = !m.IsDeleted && isOwn && (m.MessageType == "text"),
            CanDelete = !m.IsDeleted && (isOwn || isModerator),
            DeliveryStatus = "sent",
            ReadCount = 0,
            DeliveredCount = 0,
            TotalRecipients = 0,
            IsReadByMe = false,
            IsDeliveredToMe = false
        };
    }
}
