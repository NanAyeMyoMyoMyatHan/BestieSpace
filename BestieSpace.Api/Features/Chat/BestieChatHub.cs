using System.Collections.Concurrent;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.Chat;

public class BestieChatHub : Hub
{
    private readonly ILogger<BestieChatHub> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly ConcurrentDictionary<string, string> ConnectedRoles = new();

    public BestieChatHub(ILogger<BestieChatHub> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public static bool IsRoleOnline(string roleKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey)) return false;
        var clean = roleKey.Contains("Bestie", StringComparison.OrdinalIgnoreCase) ? "Bestie" : "Me";
        return ConnectedRoles.Values.Any(r => r.Equals(clean, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsUserOnline(string userName)
    {
        return IsRoleOnline(userName);
    }

    public async Task RegisterUser(string userName)
    {
        var roleKey = userName.Contains("Bestie", StringComparison.OrdinalIgnoreCase) ? "Bestie" : "Me";
        ConnectedRoles[Context.ConnectionId] = roleKey;
        _logger.LogInformation("Connection {ConnectionId} registered as role '{RoleKey}' (name: {UserName})", 
            Context.ConnectionId, roleKey, userName);

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
            _logger.LogError(ex, "Failed to update online presence for role '{RoleKey}'", roleKey);
        }
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR Client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedRoles.TryRemove(Context.ConnectionId, out var roleKey))
        {
            var stillOnline = ConnectedRoles.Values.Any(r => r == roleKey);
            _logger.LogInformation("Connection {ConnectionId} disconnected (Role: {RoleKey}, StillOnline: {StillOnline})", 
                Context.ConnectionId, roleKey, stillOnline);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestieDbContext>();
                var user = await db.Users.FirstOrDefaultAsync(u => u.RoleKey == roleKey);
                var now = DateTime.UtcNow;

                if (user != null)
                {
                    user.IsOnline = stillOnline;
                    user.LastSeenAt = now;
                    await db.SaveChangesAsync();
                }

                var presence = new UserPresenceDto
                {
                    RoleKey = roleKey,
                    DisplayName = user?.DisplayName ?? (roleKey == "Bestie" ? "Sweet Bestie 🌸" : "Your Bestie 🎀"),
                    IsOnline = stillOnline,
                    LastSeenAt = now
                };

                await Clients.All.SendAsync("UserPresenceChanged", presence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update presence on disconnect for role '{RoleKey}'", roleKey);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task BroadcastChatMessage(ChatMessageDto message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }

    public async Task BroadcastMessagesRead(MessagesReadNotification notification)
    {
        await Clients.All.SendAsync("MessagesRead", notification);
    }
}
