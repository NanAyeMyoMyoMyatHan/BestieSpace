using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Features.Chat;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Notifications;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(int userId, string type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null);
    Task<NotificationListResponseDto> GetUserNotificationsAsync(int userId, int pageNumber = 1, int pageSize = 20, bool? unreadOnly = null);
    Task<int> GetUnreadNotificationCountAsync(int userId);
    Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId);
    Task<int> MarkAllNotificationsAsReadAsync(int userId);
}

public class NotificationService : INotificationService
{
    private readonly BestieDbContext _context;
    private readonly IHubContext<BestieChatHub> _hubContext;
    private readonly IFcmService _fcmService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        BestieDbContext context,
        IHubContext<BestieChatHub> hubContext,
        IFcmService fcmService,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _fcmService = fcmService;
        _logger = logger;
    }

    public async Task<NotificationDto> CreateNotificationAsync(int userId, string type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null)
    {
        if (userId <= 0) throw new ArgumentException("Invalid user ID.");

        var notification = new Notification
        {
            UserId = userId,
            NotificationType = string.IsNullOrWhiteSpace(type) ? "System" : type.Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? "New Alert 🌸" : title.Trim(),
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim(),
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var dto = MapToDto(notification);

        // Broadcast real-time notification to user's personal channel
        try
        {
            var unreadCount = await GetUnreadNotificationCountAsync(userId);
            var eventDto = new NotificationEventDto
            {
                NotificationId = dto.NotificationId,
                UserId = userId,
                NotificationType = dto.NotificationType,
                Title = dto.Title,
                Message = dto.Message,
                RelatedEntityId = dto.RelatedEntityId,
                RelatedEntityType = dto.RelatedEntityType,
                CreatedAt = dto.CreatedAt,
                UnreadNotificationCount = unreadCount
            };

            await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", eventDto);
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("UnreadCountUpdated", new
            {
                userId,
                unreadNotificationCount = unreadCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast notification {Id} via SignalR to user {UserId}", notification.NotificationId, userId);
        }

        // Broadcast real-time Push Notification via Firebase Cloud Messaging (FCM)
        try
        {
            var fcmData = new Dictionary<string, string>
            {
                { "notificationId", dto.NotificationId.ToString() },
                { "type", dto.NotificationType },
                { "relatedEntityId", dto.RelatedEntityId?.ToString() ?? "" },
                { "relatedEntityType", dto.RelatedEntityType ?? "" },
                { "clickUrl", dto.TargetUrl ?? "/" }
            };

            await _fcmService.SendPushToUserAsync(userId, dto.Title, dto.Message, dto.TargetUrl ?? "/", fcmData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM push for notification {Id} to user {UserId}", notification.NotificationId, userId);
        }

        return dto;
    }

    public async Task<NotificationListResponseDto> GetUserNotificationsAsync(int userId, int pageNumber = 1, int pageSize = 20, bool? unreadOnly = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var baseQuery = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly == true)
        {
            baseQuery = baseQuery.Where(n => !n.IsRead);
        }

        var totalCount = await baseQuery.CountAsync();
        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var items = await baseQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new NotificationListResponseDto
        {
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Notifications = items.Select(MapToDto).ToList()
        };
    }

    public async Task<int> GetUnreadNotificationCountAsync(int userId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

        if (notification == null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Broadcast unread count decrement
            try
            {
                var unreadCount = await GetUnreadNotificationCountAsync(userId);
                await _hubContext.Clients.Group($"user_{userId}").SendAsync("UnreadCountUpdated", new
                {
                    userId,
                    unreadNotificationCount = unreadCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast unread notification count update for user {UserId}", userId);
            }
        }

        return true;
    }

    public async Task<int> MarkAllNotificationsAsReadAsync(int userId)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (!unreadNotifications.Any()) return 0;

        var now = DateTime.UtcNow;
        foreach (var n in unreadNotifications)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        await _context.SaveChangesAsync();

        try
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("UnreadCountUpdated", new
            {
                userId,
                unreadNotificationCount = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast zero unread notification count for user {UserId}", userId);
        }

        return unreadNotifications.Count;
    }

    private static NotificationDto MapToDto(Notification n)
    {
        string? targetUrl = n.NotificationType switch
        {
            "FriendRequestReceived" or "FriendRequestAccepted" => "/friends",
            "CircleInvitationReceived" or "CircleMemberJoined" or "CircleMemberLeft" or "CircleOwnershipTransferred" => n.RelatedEntityId.HasValue ? $"/circles/{n.RelatedEntityId.Value}" : "/circles",
            "NewMessage" => n.RelatedEntityId.HasValue ? $"/circles/{n.RelatedEntityId.Value}/chat" : "/circles",
            _ => null
        };

        return new NotificationDto
        {
            NotificationId = n.NotificationId,
            UserId = n.UserId,
            NotificationType = n.NotificationType,
            Title = n.Title,
            Message = n.Message,
            RelatedEntityId = n.RelatedEntityId,
            RelatedEntityType = n.RelatedEntityType,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            ReadAt = n.ReadAt,
            TargetUrl = targetUrl
        };
    }
}
