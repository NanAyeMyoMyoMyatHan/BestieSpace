using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Features.Circles;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Chat;

public interface IMessageReadService
{
    Task<int> MarkMessagesAsReadAsync(int circleId, List<int> messageIds, int userId);
    Task<MessageReadDetailsDto> GetMessageReadDetailsAsync(int messageId, int circleId, int requestingUserId);
    Task<int> GetCircleUnreadCountAsync(int circleId, int userId);
    Task<UnreadSummaryDto> GetUnreadSummaryAsync(int userId);
}

public class MessageReadService : IMessageReadService
{
    private readonly BestieDbContext _context;
    private readonly ICircleAuthorizationService _circleAuth;
    private readonly IHubContext<BestieChatHub> _hubContext;
    private readonly ILogger<MessageReadService> _logger;

    public MessageReadService(
        BestieDbContext context,
        ICircleAuthorizationService circleAuth,
        IHubContext<BestieChatHub> hubContext,
        ILogger<MessageReadService> logger)
    {
        _context = context;
        _circleAuth = circleAuth;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<int> MarkMessagesAsReadAsync(int circleId, List<int> messageIds, int userId)
    {
        if (messageIds == null || !messageIds.Any() || circleId <= 0 || userId <= 0)
        {
            return 0;
        }

        // 1. Strict Authorization check: user must be an active circle member
        var isMember = await _circleAuth.IsCircleMemberAsync(userId, circleId);
        if (!isMember)
        {
            _logger.LogWarning("User {UserId} unauthorized read receipt attempt in circle {CircleId}", userId, circleId);
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        // 2. Filter valid messages in this circle that:
        //    - belong to this circle
        //    - were NOT sent by the current user (avoid self read receipt)
        //    - are not soft-deleted
        var validMessages = await _context.ChatMessages
            .Where(m => m.CircleId == circleId &&
                        messageIds.Contains(m.Id) &&
                        m.SenderId != userId &&
                        !m.IsDeleted)
            .Select(m => m.Id)
            .ToListAsync();

        if (!validMessages.Any())
        {
            return 0;
        }

        // 3. Prevent duplicate read receipts
        var alreadyReadIds = await _context.ChatMessageReads
            .Where(r => r.UserId == userId && validMessages.Contains(r.ChatMessageId))
            .Select(r => r.ChatMessageId)
            .ToListAsync();

        var newToReadIds = validMessages.Except(alreadyReadIds).ToList();
        if (!newToReadIds.Any())
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var newReceipts = newToReadIds.Select(id => new ChatMessageRead
        {
            ChatMessageId = id,
            UserId = userId,
            ReadAt = now
        }).ToList();

        _context.ChatMessageReads.AddRange(newReceipts);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Marked {Count} messages as read for user {UserId} in circle {CircleId}", newReceipts.Count, userId, circleId);

        // 4. Fetch user details for real-time broadcast
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        var readEvent = new MessageReadEventDto
        {
            CircleId = circleId,
            MessageIds = newToReadIds,
            UserId = userId,
            DisplayName = user?.DisplayName ?? $"User {userId}",
            AvatarSticker = user?.AvatarSticker,
            ReadAt = now
        };

        try
        {
            // Broadcast read receipts to everyone active in the circle
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("MessageRead", readEvent);

            // Also broadcast updated unread count to this user's personal channel
            var newCircleUnread = await GetCircleUnreadCountAsync(circleId, userId);
            var totalUnread = await GetTotalUnreadCountAsync(userId);

            var unreadEvent = new CircleUnreadCountUpdatedEventDto
            {
                CircleId = circleId,
                UserId = userId,
                UnreadCount = newCircleUnread,
                TotalUnreadCount = totalUnread
            };

            await _hubContext.Clients.Group($"user_{userId}").SendAsync("CircleUnreadCountUpdated", unreadEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast read receipt SignalR event for circle {CircleId}", circleId);
        }

        return newReceipts.Count;
    }

    public async Task<MessageReadDetailsDto> GetMessageReadDetailsAsync(int messageId, int circleId, int requestingUserId)
    {
        // 1. Authorize: requesting user must be active member of circle
        var isMember = await _circleAuth.IsCircleMemberAsync(requestingUserId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        // 2. Fetch message ensuring it belongs to this circle
        var message = await _context.ChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.CircleId == circleId);

        if (message == null)
        {
            throw new KeyNotFoundException($"Message {messageId} not found in circle {circleId}.");
        }

        // 3. Get all active circle members
        var circleMembers = await _context.BestieCircleMembers
            .AsNoTracking()
            .Where(cm => cm.CircleId == circleId && cm.IsActive)
            .Include(cm => cm.User)
            .ToListAsync();

        // 4. Get all read receipts for this message
        var readReceipts = await _context.ChatMessageReads
            .AsNoTracking()
            .Where(r => r.ChatMessageId == messageId)
            .ToDictionaryAsync(r => r.UserId, r => r.ReadAt);

        // 5. Get all delivery receipts for this message
        var deliveryReceipts = await _context.ChatMessageDeliveries
            .AsNoTracking()
            .Where(d => d.ChatMessageId == messageId)
            .ToDictionaryAsync(d => d.UserId, d => d.DeliveredAt);

        var memberStatusList = new List<CircleMemberReadStatusDto>();
        int readCount = 0;

        foreach (var cm in circleMembers)
        {
            if (cm.User == null) continue;
            // Exclude the sender themselves from the recipient read list
            if (cm.UserId == message.SenderId) continue;

            bool hasRead = readReceipts.TryGetValue(cm.UserId, out var readAt);
            if (hasRead) readCount++;

            bool hasDelivered = deliveryReceipts.TryGetValue(cm.UserId, out var deliveredAt);

            memberStatusList.Add(new CircleMemberReadStatusDto
            {
                UserId = cm.UserId,
                Username = cm.User.Username,
                DisplayName = cm.User.DisplayName,
                AvatarSticker = cm.User.AvatarSticker,
                HasRead = hasRead,
                ReadAt = hasRead ? readAt : null,
                HasDelivered = hasDelivered,
                DeliveredAt = hasDelivered ? deliveredAt : null
            });
        }

        var totalRecipients = memberStatusList.Count;

        return new MessageReadDetailsDto
        {
            MessageId = messageId,
            CircleId = circleId,
            TotalMembers = totalRecipients,
            ReadCount = readCount,
            UnreadCount = Math.Max(0, totalRecipients - readCount),
            Members = memberStatusList.OrderByDescending(m => m.HasRead).ThenBy(m => m.DisplayName).ToList()
        };
    }

    public async Task<int> GetCircleUnreadCountAsync(int circleId, int userId)
    {
        // Messages in circleId not sent by user, not deleted, without a ChatMessageRead record for userId
        return await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CircleId == circleId &&
                        m.SenderId != userId &&
                        !m.IsDeleted &&
                        !_context.ChatMessageReads.Any(r => r.ChatMessageId == m.Id && r.UserId == userId))
            .CountAsync();
    }

    public async Task<UnreadSummaryDto> GetUnreadSummaryAsync(int userId)
    {
        // 1. Find all active circles for this user
        var memberships = await _context.BestieCircleMembers
            .AsNoTracking()
            .Where(cm => cm.UserId == userId && cm.IsActive && cm.Circle.IsActive)
            .Include(cm => cm.Circle)
            .ToListAsync();

        var circleUnreadList = new List<CircleUnreadDto>();
        int totalUnread = 0;

        foreach (var cm in memberships)
        {
            var circle = cm.Circle;
            if (circle == null) continue;

            var unreadCount = await GetCircleUnreadCountAsync(circle.CircleId, userId);
            totalUnread += unreadCount;

            // Fetch last message preview
            var lastMsg = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.CircleId == circle.CircleId && !m.IsDeleted)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();

            var preview = lastMsg?.MessageType switch
            {
                "photo" => string.IsNullOrWhiteSpace(lastMsg.Content) ? "📸 Photo" : $"📸 {lastMsg.Content}",
                "sticker" => "🌸 Sticker",
                "voice" => "🎤 Voice message",
                _ => lastMsg?.Content
            };

            circleUnreadList.Add(new CircleUnreadDto
            {
                CircleId = circle.CircleId,
                CircleName = circle.CircleName,
                CircleIcon = circle.CircleIcon,
                UnreadCount = unreadCount,
                LastMessageContent = preview,
                LastMessageSender = lastMsg?.SenderName,
                LastMessageType = lastMsg?.MessageType ?? "text",
                LastMessageTime = lastMsg?.Timestamp
            });
        }

        return new UnreadSummaryDto
        {
            TotalUnreadCount = totalUnread,
            Circles = circleUnreadList.OrderByDescending(c => c.UnreadCount > 0).ThenByDescending(c => c.LastMessageTime).ToList()
        };
    }

    private async Task<int> GetTotalUnreadCountAsync(int userId)
    {
        var summary = await GetUnreadSummaryAsync(userId);
        return summary.TotalUnreadCount;
    }
}
