using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Chat;

public interface IChatPresenceService
{
    Task<bool> UserConnectedAsync(int userId, string connectionId);
    Task<(bool isOffline, int? userId, List<int> joinedCircles)> UserDisconnectedAsync(string connectionId);
    bool IsUserOnline(int userId);
    HashSet<int> FilterOnlineUserIds(IEnumerable<int> userIds);
    void AddConnectionToCircle(string connectionId, int circleId);
    void RemoveConnectionFromCircle(string connectionId, int circleId);
    List<int> GetCirclesForConnection(string connectionId);
    int? GetUserIdForConnection(string connectionId);
}

public class ChatPresenceService : IChatPresenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatPresenceService> _logger;

    // UserId -> Set of active ConnectionIds
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _userConnections = new();

    // ConnectionId -> UserId
    private readonly ConcurrentDictionary<string, int> _connectionToUser = new();

    // ConnectionId -> Set of joined CircleIds
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _connectionCircles = new();

    public ChatPresenceService(IServiceScopeFactory scopeFactory, ILogger<ChatPresenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> UserConnectedAsync(int userId, string connectionId)
    {
        _connectionToUser[connectionId] = userId;

        var connections = _userConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        connections[connectionId] = 1;

        var isFirstConnection = connections.Count == 1;

        if (isFirstConnection)
        {
            _logger.LogInformation("User {UserId} came online with connection {ConnectionId}", userId, connectionId);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestieDbContext>();
                var user = await db.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsOnline = true;
                    user.LastSeenAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update User.IsOnline status in DB for User {UserId}", userId);
            }
        }

        return isFirstConnection;
    }

    public async Task<(bool isOffline, int? userId, List<int> joinedCircles)> UserDisconnectedAsync(string connectionId)
    {
        _connectionCircles.TryRemove(connectionId, out var circleSet);
        var joinedCircles = circleSet?.Keys.ToList() ?? new List<int>();

        if (!_connectionToUser.TryRemove(connectionId, out var userId))
        {
            return (false, null, joinedCircles);
        }

        if (_userConnections.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);

            if (connections.IsEmpty)
            {
                _userConnections.TryRemove(userId, out _);
                _logger.LogInformation("User {UserId} went offline (all connections closed)", userId);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<BestieDbContext>();
                    var user = await db.Users.FindAsync(userId);
                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.LastSeenAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update User.IsOnline status on disconnect for User {UserId}", userId);
                }

                return (true, userId, joinedCircles);
            }
        }

        return (false, userId, joinedCircles);
    }

    public bool IsUserOnline(int userId)
    {
        return _userConnections.TryGetValue(userId, out var connections) && !connections.IsEmpty;
    }

    public HashSet<int> FilterOnlineUserIds(IEnumerable<int> userIds)
    {
        var onlineSet = new HashSet<int>();
        foreach (var id in userIds)
        {
            if (IsUserOnline(id))
            {
                onlineSet.Add(id);
            }
        }
        return onlineSet;
    }

    public void AddConnectionToCircle(string connectionId, int circleId)
    {
        var circles = _connectionCircles.GetOrAdd(connectionId, _ => new ConcurrentDictionary<int, byte>());
        circles[circleId] = 1;
    }

    public void RemoveConnectionFromCircle(string connectionId, int circleId)
    {
        if (_connectionCircles.TryGetValue(connectionId, out var circles))
        {
            circles.TryRemove(circleId, out _);
        }
    }

    public List<int> GetCirclesForConnection(string connectionId)
    {
        if (_connectionCircles.TryGetValue(connectionId, out var circles))
        {
            return circles.Keys.ToList();
        }
        return new List<int>();
    }

    public int? GetUserIdForConnection(string connectionId)
    {
        if (_connectionToUser.TryGetValue(connectionId, out var userId))
        {
            return userId;
        }
        return null;
    }
}
