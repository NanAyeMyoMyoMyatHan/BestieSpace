using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Social;

public interface IUserBlockService
{
    Task<SocialOperationResultDto> BlockUserAsync(int blockerUserId, int targetUserId);
    Task<SocialOperationResultDto> UnblockUserAsync(int blockerUserId, int targetUserId);
    Task<List<UserBlockDto>> GetBlockedUsersAsync(int blockerUserId);
    Task<bool> IsBlockedAsync(int userA, int userB);
}

public class UserBlockService : IUserBlockService
{
    private readonly BestieDbContext _context;
    private readonly ILogger<UserBlockService> _logger;

    public UserBlockService(
        BestieDbContext context,
        ILogger<UserBlockService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SocialOperationResultDto> BlockUserAsync(int blockerUserId, int targetUserId)
    {
        if (blockerUserId == targetUserId)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "You cannot block yourself."
            };
        }

        var targetUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == targetUserId);
        if (targetUser == null)
        {
            return new SocialOperationResultDto { IsSuccess = false, Message = "User not found." };
        }

        var alreadyBlocked = await _context.UserBlocks
            .AnyAsync(b => b.BlockerUserId == blockerUserId && b.BlockedUserId == targetUserId);

        if (!alreadyBlocked)
        {
            _context.UserBlocks.Add(new UserBlock
            {
                BlockerUserId = blockerUserId,
                BlockedUserId = targetUserId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Dissolve any existing active or pending friendship between them upon blocking
        var userA = Math.Min(blockerUserId, targetUserId);
        var userB = Math.Max(blockerUserId, targetUserId);
        var existingFriendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.UserA == userA && f.UserB == userB);

        if (existingFriendship != null)
        {
            _context.Friendships.Remove(existingFriendship);
            _logger.LogInformation("Removed friendship {FriendshipId} due to block by user {BlockerId} on {TargetId}",
                existingFriendship.FriendshipId, blockerUserId, targetUserId);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {BlockerId} blocked user {TargetId}", blockerUserId, targetUserId);

        return new SocialOperationResultDto
        {
            IsSuccess = true,
            Message = $"User @{targetUser.Username} has been blocked.",
            FriendshipStatus = "Blocked"
        };
    }

    public async Task<SocialOperationResultDto> UnblockUserAsync(int blockerUserId, int targetUserId)
    {
        var block = await _context.UserBlocks
            .FirstOrDefaultAsync(b => b.BlockerUserId == blockerUserId && b.BlockedUserId == targetUserId);

        if (block == null)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "This user is not currently in your blocked list."
            };
        }

        _context.UserBlocks.Remove(block);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {BlockerId} unblocked user {TargetId}", blockerUserId, targetUserId);

        return new SocialOperationResultDto
        {
            IsSuccess = true,
            Message = "User has been unblocked.",
            FriendshipStatus = "None"
        };
    }

    public async Task<List<UserBlockDto>> GetBlockedUsersAsync(int blockerUserId)
    {
        var blocks = await _context.UserBlocks
            .AsNoTracking()
            .Include(b => b.BlockedUser)
            .Where(b => b.BlockerUserId == blockerUserId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return blocks.Select(b => new UserBlockDto
        {
            UserBlockId = b.UserBlockId,
            BlockedUserId = b.BlockedUserId,
            BlockedUsername = b.BlockedUser.Username,
            BlockedDisplayName = b.BlockedUser.DisplayName,
            BlockedProfilePicture = b.BlockedUser.ProfilePicture,
            BlockedAvatarSticker = b.BlockedUser.AvatarSticker,
            CreatedAt = b.CreatedAt
        }).ToList();
    }

    public async Task<bool> IsBlockedAsync(int userA, int userB)
    {
        return await _context.UserBlocks
            .AnyAsync(b => (b.BlockerUserId == userA && b.BlockedUserId == userB) ||
                           (b.BlockerUserId == userB && b.BlockedUserId == userA));
    }
}
