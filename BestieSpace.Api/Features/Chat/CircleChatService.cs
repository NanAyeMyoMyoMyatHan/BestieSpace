using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Features.Circles;
using BestieSpace.Api.Features.Notifications;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Chat;

public interface ICircleChatService
{
    Task<CircleChatPaginationDto> GetCircleMessagesAsync(int circleId, int pageNumber, int pageSize, int requestingUserId);
    Task<CircleChatMessageDto> SendCircleMessageAsync(int circleId, string content, string? messageType, int senderUserId);
    Task<CircleChatMessageDto> EditCircleMessageAsync(int messageId, int circleId, string newContent, int requestingUserId);
    Task<bool> DeleteCircleMessageAsync(int messageId, int circleId, int requestingUserId);
    Task<List<CircleMemberPresenceEventDto>> GetCirclePresenceAsync(int circleId, int requestingUserId);
}

public class CircleChatService : ICircleChatService
{
    private readonly BestieDbContext _context;
    private readonly IHubContext<BestieChatHub> _hubContext;
    private readonly ICircleAuthorizationService _circleAuth;
    private readonly IChatPresenceService _presenceService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CircleChatService> _logger;

    public CircleChatService(
        BestieDbContext context,
        IHubContext<BestieChatHub> hubContext,
        ICircleAuthorizationService circleAuth,
        IChatPresenceService presenceService,
        INotificationService notificationService,
        ILogger<CircleChatService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _circleAuth = circleAuth;
        _presenceService = presenceService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<CircleChatPaginationDto> GetCircleMessagesAsync(int circleId, int pageNumber, int pageSize, int requestingUserId)
    {
        // 1. Verify circle exists and is active
        var circle = await _context.BestieCircles.AsNoTracking().FirstOrDefaultAsync(c => c.CircleId == circleId);
        if (circle == null || !circle.IsActive)
        {
            throw new KeyNotFoundException($"Circle {circleId} not found or inactive.");
        }

        // 2. Strict privacy check: only active members can view circle messages
        var isMember = await _circleAuth.IsCircleMemberAsync(requestingUserId, circleId);
        if (!isMember)
        {
            _logger.LogWarning("Unauthorized attempt by user {UserId} to read messages of circle {CircleId}", requestingUserId, circleId);
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        // 3. Check moderation privileges for delete permissions
        var canModerate = await _circleAuth.HasCirclePermissionAsync(requestingUserId, circleId, "CHAT_MODERATE");
        var isOwner = await _circleAuth.IsCircleOwnerAsync(requestingUserId, circleId);
        var isModerator = canModerate || isOwner;

        // 4. Paginate
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 30;

        var baseQuery = _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CircleId == circleId);

        var totalCount = await baseQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var totalMembers = await _context.BestieCircleMembers
            .AsNoTracking()
            .Where(cm => cm.CircleId == circleId && cm.IsActive)
            .CountAsync();
        var totalRecipients = Math.Max(0, totalMembers - 1);

        var rawMessages = await baseQuery
            .OrderByDescending(m => m.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Sender)
            .Include(m => m.ReadReceipts)
            .Include(m => m.DeliveryReceipts)
            .ToListAsync();

        // Reverse to return in chronological order (oldest to newest)
        rawMessages.Reverse();

        var messageDtos = rawMessages.Select(m => MapToDto(m, requestingUserId, isModerator, totalRecipients)).ToList();

        return new CircleChatPaginationDto
        {
            CircleId = circleId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasMore = pageNumber < totalPages,
            Messages = messageDtos
        };
    }

    public async Task<CircleChatMessageDto> SendCircleMessageAsync(int circleId, string content, string? messageType, int senderUserId)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty.");
        }

        var trimmed = content.Trim();
        if (trimmed.Length > 2000)
        {
            throw new ArgumentException("Message length cannot exceed 2000 characters.");
        }

        // 2. Verify active circle
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            throw new KeyNotFoundException($"Circle {circleId} not found or inactive.");
        }

        // 3. Privacy & Permission check: must be active member
        var isMember = await _circleAuth.IsCircleMemberAsync(senderUserId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        var canSend = await _circleAuth.HasCirclePermissionAsync(senderUserId, circleId, "CHAT_SEND");
        if (!canSend)
        {
            throw new UnauthorizedAccessException("You do not have permission to send messages in this circle.");
        }

        // 4. Fetch sender
        var sender = await _context.Users.FindAsync(senderUserId);
        if (sender == null)
        {
            throw new KeyNotFoundException($"User {senderUserId} not found.");
        }

        // 5. Persist message to SQL Server first
        var chatMessage = new ChatMessage
        {
            CircleId = circleId,
            SenderId = senderUserId,
            SenderName = sender.DisplayName,
            MessageType = string.IsNullOrWhiteSpace(messageType) ? "text" : messageType.Trim().ToLowerInvariant(),
            Content = trimmed,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Message {MessageId} saved for circle {CircleId} by user {UserId}", chatMessage.Id, circleId, senderUserId);

        // 6. Broadcast to SignalR group circle_{circleId}
        var canModerate = await _circleAuth.HasCirclePermissionAsync(senderUserId, circleId, "CHAT_MODERATE");
        var isOwner = await _circleAuth.IsCircleOwnerAsync(senderUserId, circleId);
        chatMessage.Sender = sender;

        var totalMembers = await _context.BestieCircleMembers
            .AsNoTracking()
            .Where(cm => cm.CircleId == circleId && cm.IsActive)
            .CountAsync();
        var totalRecipients = Math.Max(0, totalMembers - 1);

        var dto = MapToDto(chatMessage, senderUserId, canModerate || isOwner, totalRecipients);

        try
        {
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("ReceiveMessage", dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast message {MessageId} to group circle_{CircleId}", chatMessage.Id, circleId);
        }

        // 7. Create offline notifications for other active circle members
        try
        {
            var otherMemberUserIds = await _context.BestieCircleMembers
                .Where(cm => cm.CircleId == circleId && cm.IsActive && cm.UserId != senderUserId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            var snippet = trimmed.Length > 80 ? trimmed[..77] + "..." : trimmed;
            foreach (var recipientUid in otherMemberUserIds)
            {
                await _notificationService.CreateNotificationAsync(
                    recipientUid,
                    "NewMessage",
                    $"{circle.CircleName} 💬",
                    $"{sender.DisplayName}: {snippet}",
                    circleId,
                    "Circle");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create message notifications for circle {CircleId}", circleId);
        }

        return dto;
    }

    public async Task<CircleChatMessageDto> EditCircleMessageAsync(int messageId, int circleId, string newContent, int requestingUserId)
    {
        if (string.IsNullOrWhiteSpace(newContent))
        {
            throw new ArgumentException("Message content cannot be empty.");
        }

        var trimmed = newContent.Trim();
        if (trimmed.Length > 2000)
        {
            throw new ArgumentException("Message length cannot exceed 2000 characters.");
        }

        // 1. Verify membership
        var isMember = await _circleAuth.IsCircleMemberAsync(requestingUserId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        // 2. Find message
        var message = await _context.ChatMessages
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.CircleId == circleId);

        if (message == null)
        {
            throw new KeyNotFoundException($"Message {messageId} not found in circle {circleId}.");
        }

        if (message.IsDeleted)
        {
            throw new InvalidOperationException("Cannot edit a deleted message.");
        }

        // 3. Authorization check: sender can edit, or authorized moderator
        var canModerate = await _circleAuth.HasCirclePermissionAsync(requestingUserId, circleId, "CHAT_MODERATE");
        var isOwner = await _circleAuth.IsCircleOwnerAsync(requestingUserId, circleId);
        var isSender = message.SenderId == requestingUserId;

        if (!isSender && !canModerate && !isOwner)
        {
            throw new UnauthorizedAccessException("You can only edit your own messages.");
        }

        // 4. Update and persist
        message.Content = trimmed;
        message.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(message, requestingUserId, canModerate || isOwner);

        // 5. Broadcast to group
        try
        {
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("MessageUpdated", dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast MessageUpdated for {MessageId} in circle_{CircleId}", messageId, circleId);
        }

        return dto;
    }

    public async Task<bool> DeleteCircleMessageAsync(int messageId, int circleId, int requestingUserId)
    {
        // 1. Verify membership
        var isMember = await _circleAuth.IsCircleMemberAsync(requestingUserId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        // 2. Find message
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.CircleId == circleId);

        if (message == null)
        {
            throw new KeyNotFoundException($"Message {messageId} not found in circle {circleId}.");
        }

        if (message.IsDeleted)
        {
            return true; // Already deleted
        }

        // 3. Authorization check: sender or moderator/owner
        var canModerate = await _circleAuth.HasCirclePermissionAsync(requestingUserId, circleId, "CHAT_MODERATE");
        var isOwner = await _circleAuth.IsCircleOwnerAsync(requestingUserId, circleId);
        var isSender = message.SenderId == requestingUserId;

        if (!isSender && !canModerate && !isOwner)
        {
            throw new UnauthorizedAccessException("You can only delete your own messages unless you are a circle moderator/owner.");
        }

        // 4. Soft delete
        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        message.Content = "🌸 This message was deleted";
        await _context.SaveChangesAsync();

        // 5. Broadcast deletion event
        var deletedEvent = new CircleMessageDeletedEventDto
        {
            MessageId = messageId,
            CircleId = circleId,
            DeletedByUserId = requestingUserId,
            DeletedAt = DateTime.UtcNow
        };

        try
        {
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("MessageDeleted", deletedEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast MessageDeleted for {MessageId} in circle_{CircleId}", messageId, circleId);
        }

        return true;
    }

    public async Task<List<CircleMemberPresenceEventDto>> GetCirclePresenceAsync(int circleId, int requestingUserId)
    {
        var isMember = await _circleAuth.IsCircleMemberAsync(requestingUserId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        var members = await _context.BestieCircleMembers
            .AsNoTracking()
            .Where(m => m.CircleId == circleId && m.IsActive)
            .Include(m => m.User)
            .ToListAsync();

        var list = new List<CircleMemberPresenceEventDto>();
        foreach (var m in members)
        {
            if (m.User == null) continue;

            var isOnline = _presenceService.IsUserOnline(m.UserId);
            list.Add(new CircleMemberPresenceEventDto
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName,
                AvatarSticker = m.User.AvatarSticker,
                IsOnline = isOnline,
                LastSeenAt = m.User.LastSeenAt,
                CircleId = circleId
            });
        }

        return list;
    }

    private static CircleChatMessageDto MapToDto(ChatMessage m, int requestingUserId, bool isModerator, int totalRecipients = 0)
    {
        var isOwn = m.SenderId == requestingUserId;
        var readCount = m.ReadReceipts?.Count(r => r.UserId != m.SenderId) ?? 0;
        var deliveredCount = m.DeliveryReceipts?.Count(d => d.UserId != m.SenderId) ?? 0;
        var isReadByMe = m.ReadReceipts?.Any(r => r.UserId == requestingUserId) ?? false;
        var isDeliveredToMe = m.DeliveryReceipts?.Any(d => d.UserId == requestingUserId) ?? false;

        string deliveryStatus;
        if (readCount > 0)
        {
            deliveryStatus = "read";
        }
        else if (deliveredCount > 0)
        {
            deliveryStatus = "delivered";
        }
        else
        {
            deliveryStatus = "sent";
        }

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
            DeliveryStatus = deliveryStatus,
            ReadCount = readCount,
            DeliveredCount = deliveredCount,
            TotalRecipients = totalRecipients,
            IsReadByMe = isReadByMe,
            IsDeliveredToMe = isDeliveredToMe
        };
    }
}
