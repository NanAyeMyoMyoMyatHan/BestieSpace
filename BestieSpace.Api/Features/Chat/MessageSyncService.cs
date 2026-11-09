using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Features.Circles;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Chat;

public interface IMessageSyncService
{
    Task<MessageSyncResponseDto> SyncMessagesAsync(int circleId, int afterMessageId, int pageSize, int requestingUserId);
}

public class MessageSyncService : IMessageSyncService
{
    private readonly BestieDbContext _context;
    private readonly ICircleAuthorizationService _circleAuth;
    private readonly ILogger<MessageSyncService> _logger;

    public MessageSyncService(
        BestieDbContext context,
        ICircleAuthorizationService circleAuth,
        ILogger<MessageSyncService> logger)
    {
        _context = context;
        _circleAuth = circleAuth;
        _logger = logger;
    }

    public async Task<MessageSyncResponseDto> SyncMessagesAsync(int circleId, int afterMessageId, int pageSize, int requestingUserId)
    {
        // 1. Strict membership authorization
        var isMember = await _circleAuth.IsCircleMemberAsync(requestingUserId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this Bestie Circle.");
        }

        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        // 2. Query messages after cursor ID in chronological order
        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CircleId == circleId && m.Id > afterMessageId && !m.IsDeleted)
            .OrderBy(m => m.Id);

        var totalAvailable = await query.CountAsync();

        var messages = await query
            .Take(pageSize)
            .Include(m => m.Sender)
            .Include(m => m.ReadReceipts)
            .Include(m => m.DeliveryReceipts)
            .ToListAsync();

        var latestMessageId = messages.Any() ? messages.Max(m => m.Id) : afterMessageId;
        var hasMore = totalAvailable > messages.Count;

        // 3. Count total members for status indicators
        var totalMembers = await _context.BestieCircleMembers
            .AsNoTracking()
            .Where(cm => cm.CircleId == circleId && cm.IsActive)
            .CountAsync();

        var totalRecipients = Math.Max(0, totalMembers - 1);

        // 4. Check moderation permission for delete privilege
        var canModerate = await _circleAuth.HasCirclePermissionAsync(requestingUserId, circleId, "CHAT_MODERATE");
        var isOwner = await _circleAuth.IsCircleOwnerAsync(requestingUserId, circleId);
        var isModerator = canModerate || isOwner;

        var dtos = messages.Select(m =>
        {
            var isOwn = m.SenderId == requestingUserId;
            var readCount = m.ReadReceipts.Count(r => r.UserId != m.SenderId);
            var deliveredCount = m.DeliveryReceipts.Count(d => d.UserId != m.SenderId);
            var isReadByMe = m.ReadReceipts.Any(r => r.UserId == requestingUserId);
            var isDeliveredToMe = m.DeliveryReceipts.Any(d => d.UserId == requestingUserId);

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
        }).ToList();

        return new MessageSyncResponseDto
        {
            CircleId = circleId,
            AfterMessageId = afterMessageId,
            HasMore = hasMore,
            LatestMessageId = latestMessageId,
            Messages = dtos
        };
    }
}
