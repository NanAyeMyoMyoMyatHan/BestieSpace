using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Tasks;
using BestieSpace.Api.Features.Circles;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Chat;

public class BestieChatHub : Hub
{
    private readonly ILogger<BestieChatHub> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChatPresenceService _presenceService;
    private readonly ICircleAuthorizationService _circleAuth;

    // Preserved for legacy compatibility with Phase 1-4 clubhouse
    private static readonly ConcurrentDictionary<string, string> ConnectedRoles = new();

    public BestieChatHub(
        ILogger<BestieChatHub> logger,
        IServiceScopeFactory scopeFactory,
        IChatPresenceService presenceService,
        ICircleAuthorizationService circleAuth)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _presenceService = presenceService;
        _circleAuth = circleAuth;
    }

    public static bool IsRoleOnline(string roleKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey)) return false;
        var clean = roleKey.Contains("Bestie", StringComparison.OrdinalIgnoreCase) ? "Bestie" : "Me";
        return ConnectedRoles.Values.Any(r => r.Equals(clean, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsUserOnline(string userName) => IsRoleOnline(userName);

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR connection established: {ConnectionId}", Context.ConnectionId);

        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            // Join personal user group for targeted notifications and delivery updates
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");

            var becameOnline = await _presenceService.UserConnectedAsync(userId.Value, Context.ConnectionId);
            if (becameOnline)
            {
                // Notify connected clients that user came online
                var (displayName, avatarSticker) = await GetUserDetailsAsync(userId.Value);
                var presenceEvent = new CircleMemberPresenceEventDto
                {
                    UserId = userId.Value,
                    DisplayName = displayName,
                    AvatarSticker = avatarSticker,
                    IsOnline = true,
                    LastSeenAt = DateTime.UtcNow
                };
                await Clients.All.SendAsync("UserOnline", presenceEvent);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR connection closing: {ConnectionId}", Context.ConnectionId);

        var curUserId = GetCurrentUserId();
        if (curUserId.HasValue)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{curUserId.Value}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to remove connection {ConnectionId} from user group", Context.ConnectionId);
            }
        }

        // 1. Phase 6 multi-connection presence cleanup
        var (isOffline, userId, joinedCircles) = await _presenceService.UserDisconnectedAsync(Context.ConnectionId);

        if (isOffline && userId.HasValue)
        {
            var (displayName, avatarSticker) = await GetUserDetailsAsync(userId.Value);
            var presenceEvent = new CircleMemberPresenceEventDto
            {
                UserId = userId.Value,
                DisplayName = displayName,
                AvatarSticker = avatarSticker,
                IsOnline = false,
                LastSeenAt = DateTime.UtcNow
            };
            await Clients.All.SendAsync("UserOffline", presenceEvent);
        }

        // 2. Legacy Role-based cleanup (preserved for backwards compatibility)
        if (ConnectedRoles.TryRemove(Context.ConnectionId, out var roleKey))
        {
            var stillOnline = ConnectedRoles.Values.Any(r => r == roleKey);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestieDbContext>();
                var user = await db.Users.FirstOrDefaultAsync(u => u.RoleKey == roleKey);
                if (user != null)
                {
                    user.IsOnline = stillOnline;
                    user.LastSeenAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                var presence = new UserPresenceDto
                {
                    RoleKey = roleKey,
                    DisplayName = user?.DisplayName ?? (roleKey == "Bestie" ? "Sweet Bestie 🌸" : "Your Bestie 🎀"),
                    IsOnline = stillOnline,
                    LastSeenAt = DateTime.UtcNow
                };
                await Clients.All.SendAsync("UserPresenceChanged", presence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed legacy role cleanup on disconnect for '{RoleKey}'", roleKey);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a private circle's SignalR group after strict server-side authorization check.
    /// </summary>
    public async Task JoinCircle(int circleId)
    {
        var userId = GetRequiredUserId();

        // 1. Strict server-side verification: user must be active member of this circle
        var isMember = await _circleAuth.IsCircleMemberAsync(userId, circleId);
        if (!isMember)
        {
            _logger.LogWarning("User {UserId} attempted unauthorized join to circle {CircleId}", userId, circleId);
            throw new HubException("Access Denied: You are not an active member of this Bestie Circle.");
        }

        var groupName = $"circle_{circleId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _presenceService.AddConnectionToCircle(Context.ConnectionId, circleId);

        _logger.LogInformation("User {UserId} ({ConnectionId}) joined group {GroupName}", userId, Context.ConnectionId, groupName);

        var (displayName, _) = await GetUserDetailsAsync(userId);
        var joinEvent = new CircleMemberJoinedEventDto
        {
            CircleId = circleId,
            UserId = userId,
            DisplayName = displayName,
            JoinedAt = DateTime.UtcNow
        };

        await Clients.Group(groupName).SendAsync("UserJoinedCircle", joinEvent);
    }

    /// <summary>
    /// Leave a private circle's SignalR group.
    /// </summary>
    public async Task LeaveCircle(int circleId)
    {
        var userId = GetCurrentUserId();
        var groupName = $"circle_{circleId}";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _presenceService.RemoveConnectionFromCircle(Context.ConnectionId, circleId);

        _logger.LogInformation("Connection {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);

        if (userId.HasValue)
        {
            var (displayName, _) = await GetUserDetailsAsync(userId.Value);
            var leaveEvent = new CircleMemberLeftEventDto
            {
                CircleId = circleId,
                UserId = userId.Value,
                DisplayName = displayName
            };
            await Clients.Group(groupName).SendAsync("UserLeftCircle", leaveEvent);
        }
    }

    /// <summary>
    /// Client-to-server SendMessage event.
    /// </summary>
    public async Task<CircleChatMessageDto> SendMessage(SendCircleMessageRequestDto request)
    {
        var userId = GetRequiredUserId();
        using var scope = _scopeFactory.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<ICircleChatService>();
        return await chatService.SendCircleMessageAsync(request.CircleId, request.Content, request.MessageType, userId);
    }

    /// <summary>
    /// Client-to-server EditMessage event.
    /// </summary>
    public async Task<CircleChatMessageDto> EditMessage(EditCircleMessageRequestDto request)
    {
        var userId = GetRequiredUserId();
        using var scope = _scopeFactory.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<ICircleChatService>();
        return await chatService.EditCircleMessageAsync(request.MessageId, request.CircleId, request.Content, userId);
    }

    /// <summary>
    /// Client-to-server DeleteMessage event.
    /// </summary>
    public async Task<bool> DeleteMessage(DeleteCircleMessageRequestDto request)
    {
        var userId = GetRequiredUserId();
        using var scope = _scopeFactory.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<ICircleChatService>();
        return await chatService.DeleteCircleMessageAsync(request.MessageId, request.CircleId, userId);
    }

    /// <summary>
    /// Client-to-server SendSticker event.
    /// </summary>
    public async Task<CircleChatMessageDto> SendSticker(SendStickerMessageRequestDto request)
    {
        var userId = GetRequiredUserId();
        using var scope = _scopeFactory.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<BestieSpace.Api.Features.Media.IChatMediaService>();
        return await mediaService.SendStickerMessageAsync(request.CircleId, request.StickerId, userId);
    }

    /// <summary>
    /// Client-to-server MarkMessagesRead event for per-user read receipts.
    /// </summary>
    public async Task<int> MarkMessagesRead(MarkMessagesReadRequestDto request)
    {
        var userId = GetRequiredUserId();
        using var scope = _scopeFactory.CreateScope();
        var readService = scope.ServiceProvider.GetRequiredService<IMessageReadService>();
        return await readService.MarkMessagesAsReadAsync(request.CircleId, request.MessageIds, userId);
    }

    /// <summary>
    /// Client-to-server AcknowledgeDelivery event for message delivery tracking.
    /// </summary>
    public async Task<int> AcknowledgeDelivery(AcknowledgeDeliveryRequestDto request)
    {
        var userId = GetRequiredUserId();
        using var scope = _scopeFactory.CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<IMessageDeliveryService>();
        return await deliveryService.AcknowledgeDeliveryAsync(request.CircleId, request.MessageIds, userId);
    }

    // =========================================================================
    // Legacy Hub methods preserved for Phase 1-4 Clubhouse Compatibility
    // =========================================================================

    public async Task RegisterUser(string userName)
    {
        var roleKey = userName.Contains("Bestie", StringComparison.OrdinalIgnoreCase) ? "Bestie" : "Me";
        ConnectedRoles[Context.ConnectionId] = roleKey;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestieDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.RoleKey == roleKey);
            var now = DateTime.UtcNow;

            if (user != null)
            {
                user.IsOnline = true;
                user.LastSeenAt = now;
                await db.SaveChangesAsync();
            }

            var presence = new UserPresenceDto
            {
                RoleKey = roleKey,
                DisplayName = user?.DisplayName ?? (roleKey == "Bestie" ? "Sweet Bestie 🌸" : "Your Bestie 🎀"),
                IsOnline = true,
                LastSeenAt = now
            };

            await Clients.All.SendAsync("UserPresenceChanged", presence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed legacy RegisterUser for '{RoleKey}'", roleKey);
        }
    }

    public async Task BroadcastChatMessage(ChatMessageDto message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }

    public async Task BroadcastMessagesRead(MessagesReadNotification notification)
    {
        await Clients.All.SendAsync("MessagesRead", notification);
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private int? GetCurrentUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? Context.User?.FindFirst("sub")?.Value
                    ?? Context.User?.FindFirst("userId")?.Value;

        if (int.TryParse(claim, out var uid))
        {
            return uid;
        }
        return null;
    }

    private int GetRequiredUserId()
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue)
        {
            throw new HubException("Unauthorized: Authentication token is missing or invalid.");
        }
        return uid.Value;
    }

    private async Task<(string displayName, string? avatarSticker)> GetUserDetailsAsync(int userId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BestieDbContext>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            return (user?.DisplayName ?? $"User {userId}", user?.AvatarSticker);
        }
        catch
        {
            return ($"User {userId}", null);
        }
    }
}
