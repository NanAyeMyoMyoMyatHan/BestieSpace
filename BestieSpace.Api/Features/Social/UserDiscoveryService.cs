using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Social;

public interface IUserDiscoveryService
{
    Task<PagedResultDto<UserDiscoveryCardDto>> DiscoverUsersAsync(int currentUserId, string? query, int pageNumber = 1, int pageSize = 10);
    Task<List<UserDiscoveryCardDto>> GetSuggestedUsersAsync(int currentUserId, int limit = 6);
}

public class UserDiscoveryService : IUserDiscoveryService
{
    private readonly BestieDbContext _context;
    private readonly ILogger<UserDiscoveryService> _logger;

    public UserDiscoveryService(
        BestieDbContext context,
        ILogger<UserDiscoveryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResultDto<UserDiscoveryCardDto>> DiscoverUsersAsync(
        int currentUserId,
        string? query,
        int pageNumber = 1,
        int pageSize = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        // Retrieve all user IDs blocked in either direction to exclude from discovery
        var blockedUserIds = await _context.UserBlocks
            .AsNoTracking()
            .Where(b => b.BlockerUserId == currentUserId || b.BlockedUserId == currentUserId)
            .Select(b => b.BlockerUserId == currentUserId ? b.BlockedUserId : b.BlockerUserId)
            .Distinct()
            .ToListAsync();

        var baseQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.UserId != currentUserId && u.IsActive && !blockedUserIds.Contains(u.UserId));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var cleanQuery = query.Trim();
            baseQuery = baseQuery.Where(u =>
                u.Username.Contains(cleanQuery) ||
                u.DisplayName.Contains(cleanQuery));
        }

        var totalCount = await baseQuery.CountAsync();

        var users = await baseQuery
            .OrderByDescending(u => u.IsOnline)
            .ThenByDescending(u => u.LastSeenAt)
            .ThenBy(u => u.Username)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = users.Select(u => u.UserId).ToList();

        // Fetch relationships for these users in a single efficient query
        var relationships = await _context.Friendships
            .AsNoTracking()
            .Where(f => (f.UserId == currentUserId && userIds.Contains(f.FriendUserId)) ||
                        (f.FriendUserId == currentUserId && userIds.Contains(f.UserId)))
            .ToListAsync();

        var items = users.Select(u =>
        {
            var rel = relationships.FirstOrDefault(f => f.UserId == u.UserId || f.FriendUserId == u.UserId);
            var status = "None";
            int? friendshipId = null;

            if (rel != null)
            {
                friendshipId = rel.FriendshipId;
                if (rel.Status == "Accepted")
                {
                    status = "Accepted";
                }
                else if (rel.Status == "Pending")
                {
                    status = rel.UserId == currentUserId ? "PendingSent" : "PendingReceived";
                }
            }

            return new UserDiscoveryCardDto
            {
                UserId = u.UserId,
                Username = u.Username,
                DisplayName = u.DisplayName,
                ProfilePicture = u.ProfilePicture,
                AvatarSticker = u.AvatarSticker,
                Bio = u.Bio,
                IsOnline = u.IsOnline,
                LastSeenAt = u.LastSeenAt,
                FriendshipStatus = status,
                ActiveFriendshipId = friendshipId
            };
        }).ToList();

        return new PagedResultDto<UserDiscoveryCardDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<UserDiscoveryCardDto>> GetSuggestedUsersAsync(int currentUserId, int limit = 6)
    {
        if (limit < 1) limit = 6;
        if (limit > 20) limit = 20;

        // Retrieve blocked users
        var blockedUserIds = await _context.UserBlocks
            .AsNoTracking()
            .Where(b => b.BlockerUserId == currentUserId || b.BlockedUserId == currentUserId)
            .Select(b => b.BlockerUserId == currentUserId ? b.BlockedUserId : b.BlockerUserId)
            .Distinct()
            .ToListAsync();

        // Retrieve already accepted friends
        var friendUserIds = await _context.Friendships
            .AsNoTracking()
            .Where(f => (f.UserId == currentUserId || f.FriendUserId == currentUserId) && f.Status == "Accepted")
            .Select(f => f.UserId == currentUserId ? f.FriendUserId : f.UserId)
            .ToListAsync();

        var excludeIds = blockedUserIds.Concat(friendUserIds).Append(currentUserId).Distinct().ToList();

        var suggested = await _context.Users
            .AsNoTracking()
            .Where(u => !excludeIds.Contains(u.UserId) && u.IsActive)
            .OrderByDescending(u => u.IsOnline)
            .ThenByDescending(u => u.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var suggestedIds = suggested.Select(u => u.UserId).ToList();

        // Check any pending requests
        var pendings = await _context.Friendships
            .AsNoTracking()
            .Where(f => ((f.UserId == currentUserId && suggestedIds.Contains(f.FriendUserId)) ||
                         (f.FriendUserId == currentUserId && suggestedIds.Contains(f.UserId))) && f.Status == "Pending")
            .ToListAsync();

        return suggested.Select(u =>
        {
            var p = pendings.FirstOrDefault(f => f.UserId == u.UserId || f.FriendUserId == u.UserId);
            var status = "None";
            int? friendshipId = null;

            if (p != null)
            {
                friendshipId = p.FriendshipId;
                status = p.UserId == currentUserId ? "PendingSent" : "PendingReceived";
            }

            return new UserDiscoveryCardDto
            {
                UserId = u.UserId,
                Username = u.Username,
                DisplayName = u.DisplayName,
                ProfilePicture = u.ProfilePicture,
                AvatarSticker = u.AvatarSticker,
                Bio = u.Bio,
                IsOnline = u.IsOnline,
                LastSeenAt = u.LastSeenAt,
                FriendshipStatus = status,
                ActiveFriendshipId = friendshipId
            };
        }).ToList();
    }
}
