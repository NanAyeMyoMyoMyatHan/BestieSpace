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

public interface IMessageDeliveryService
{
    Task<int> AcknowledgeDeliveryAsync(int circleId, List<int> messageIds, int userId);
}

public class MessageDeliveryService : IMessageDeliveryService
{
    private readonly BestieDbContext _context;
    private readonly ICircleAuthorizationService _circleAuth;
    private readonly IHubContext<BestieChatHub> _hubContext;
    private readonly ILogger<MessageDeliveryService> _logger;

    public MessageDeliveryService(
        BestieDbContext context,
        ICircleAuthorizationService circleAuth,
        IHubContext<BestieChatHub> hubContext,
        ILogger<MessageDeliveryService> logger)
    {
        _context = context;
        _circleAuth = circleAuth;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<int> AcknowledgeDeliveryAsync(int circleId, List<int> messageIds, int userId)
    {
        if (messageIds == null || !messageIds.Any() || circleId <= 0 || userId <= 0)
        {
            return 0;
        }

        // 1. Verify active circle membership
        var isMember = await _circleAuth.IsCircleMemberAsync(userId, circleId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not an active member of this circle.");
        }

        // 2. Validate messages exist in this circle and were not sent by the acknowledging user
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

        // 3. Prevent duplicate delivery receipts
        var alreadyDeliveredIds = await _context.ChatMessageDeliveries
            .Where(d => d.UserId == userId && validMessages.Contains(d.ChatMessageId))
            .Select(d => d.ChatMessageId)
            .ToListAsync();

        var newDeliveryIds = validMessages.Except(alreadyDeliveredIds).ToList();
        if (!newDeliveryIds.Any())
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var deliveryReceipts = newDeliveryIds.Select(id => new ChatMessageDelivery
        {
            ChatMessageId = id,
            UserId = userId,
            DeliveredAt = now
        }).ToList();

        _context.ChatMessageDeliveries.AddRange(deliveryReceipts);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Acknowledged delivery of {Count} messages for user {UserId} in circle {CircleId}", deliveryReceipts.Count, userId, circleId);

        // 4. SignalR broadcast to the circle that messages have been delivered to this user
        var deliveredEvent = new MessageDeliveredEventDto
        {
            CircleId = circleId,
            MessageIds = newDeliveryIds,
            UserId = userId,
            DeliveredAt = now
        };

        try
        {
            await _hubContext.Clients.Group($"circle_{circleId}").SendAsync("MessageDelivered", deliveredEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast MessageDelivered SignalR event for circle {CircleId}", circleId);
        }

        return deliveryReceipts.Count;
    }
}
