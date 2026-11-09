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

public interface IFriendshipService
{
    Task<SocialOperationResultDto> SendFriendRequestAsync(int requesterUserId, int addresseeUserId);
    Task<SocialOperationResultDto> AcceptFriendRequestAsync(int currentUserId, int friendshipId);
    Task<SocialOperationResultDto> RejectFriendRequestAsync(int currentUserId, int friendshipId);
    Task<SocialOperationResultDto> CancelFriendRequestAsync(int currentUserId, int friendshipId);
    Task<SocialOperationResultDto> RemoveFriendAsync(int currentUserId, int friendshipId);
    Task<List<FriendshipDto>> GetIncomingFriendRequestsAsync(int currentUserId);
    Task<List<FriendshipDto>> GetOutgoingFriendRequestsAsync(int currentUserId);
    Task<List<FriendshipDto>> GetFriendsListAsync(int currentUserId, string? query = null);
    Task<string> GetFriendshipStatusAsync(int currentUserId, int targetUserId);
}

public class FriendshipService : IFriendshipService
{
    private readonly BestieDbContext _context;
    private readonly ILogger<FriendshipService> _logger;

    public FriendshipService(
        BestieDbContext context,
        ILogger<FriendshipService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SocialOperationResultDto> SendFriendRequestAsync(int requesterUserId, int addresseeUserId)
    {
        if (requesterUserId == addresseeUserId)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "You cannot send a friend request to yourself! 🌸",
                FriendshipStatus = "Self"
            };
        }

        var addressee = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == addresseeUserId);
        if (addressee == null || !addressee.IsActive)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "User not found or account is deactivated.",
                FriendshipStatus = "None"
            };
        }

        // Check if either user has blocked the other
        var isBlocked = await _context.UserBlocks
            .AnyAsync(b => (b.BlockerUserId == requesterUserId && b.BlockedUserId == addresseeUserId) ||
                           (b.BlockerUserId == addresseeUserId && b.BlockedUserId == requesterUserId));
        if (isBlocked)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "Unable to send friend request to this user.",
                FriendshipStatus = "Blocked"
            };
        }

        // Canonical user pairing lookup: UserA <= UserB guaranteed in DB
        var userA = Math.Min(requesterUserId, addresseeUserId);
        var userB = Math.Max(requesterUserId, addresseeUserId);

        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => f.UserA == userA && f.UserB == userB);

        if (existing == null)
        {
            var newFriendship = new Friendship
            {
                UserId = requesterUserId,
                FriendUserId = addresseeUserId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Friendships.Add(newFriendship);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Friend request sent from {RequesterId} to {AddresseeId} (ID: {FriendshipId})",
                requesterUserId, addresseeUserId, newFriendship.FriendshipId);

            return new SocialOperationResultDto
            {
                IsSuccess = true,
                Message = $"Friend request sent to {addressee.DisplayName}! 💌",
                FriendshipStatus = "PendingSent",
                FriendshipId = newFriendship.FriendshipId
            };
        }

        if (existing.Status == "Accepted")
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = $"You and {addressee.DisplayName} are already besties! 🌸",
                FriendshipStatus = "Accepted",
                FriendshipId = existing.FriendshipId
            };
        }

        if (existing.Status == "Pending")
        {
            if (existing.UserId == requesterUserId)
            {
                return new SocialOperationResultDto
                {
                    IsSuccess = false,
                    Message = "Friend request is already sent and pending! 💌",
                    FriendshipStatus = "PendingSent",
                    FriendshipId = existing.FriendshipId
                };
            }
            else
            {
                // The addressee had already sent you a request! Auto-accept to complete the mutual friendship!
                existing.Status = "Accepted";
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Mutual friend request auto-accepted between {RequesterId} and {AddresseeId}",
                    requesterUserId, addresseeUserId);

                return new SocialOperationResultDto
                {
                    IsSuccess = true,
                    Message = $"Mutual friend request! You and {addressee.DisplayName} are now besties! ✨",
                    FriendshipStatus = "Accepted",
                    FriendshipId = existing.FriendshipId
                };
            }
        }

        if (existing.Status == "Rejected")
        {
            // Allow re-requesting after previous rejection
            existing.UserId = requesterUserId;
            existing.FriendUserId = addresseeUserId;
            existing.Status = "Pending";
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new SocialOperationResultDto
            {
                IsSuccess = true,
                Message = $"Friend request re-sent to {addressee.DisplayName}! 💌",
                FriendshipStatus = "PendingSent",
                FriendshipId = existing.FriendshipId
            };
        }

        return new SocialOperationResultDto
        {
            IsSuccess = false,
            Message = "Unable to process friend request.",
            FriendshipStatus = "None"
        };
    }

    public async Task<SocialOperationResultDto> AcceptFriendRequestAsync(int currentUserId, int friendshipId)
    {
        var friendship = await _context.Friendships
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId);

        if (friendship == null)
        {
            return new SocialOperationResultDto { IsSuccess = false, Message = "Friend request not found." };
        }

        // Security check: only the addressee (recipient) can accept the request
        if (friendship.FriendUserId != currentUserId)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "You can only accept friend requests sent directly to you."
            };
        }

        if (friendship.Status != "Pending")
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = $"Request is already {friendship.Status.ToLower()}."
            };
        }

        friendship.Status = "Accepted";
        friendship.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} accepted friend request {FriendshipId} from {RequesterId}",
            currentUserId, friendshipId, friendship.UserId);

        return new SocialOperationResultDto
        {
            IsSuccess = true,
            Message = $"You are now besties with {friendship.User.DisplayName}! 💖",
            FriendshipStatus = "Accepted",
            FriendshipId = friendship.FriendshipId
        };
    }

    public async Task<SocialOperationResultDto> RejectFriendRequestAsync(int currentUserId, int friendshipId)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId);

        if (friendship == null)
        {
            return new SocialOperationResultDto { IsSuccess = false, Message = "Friend request not found." };
        }

        // Security check: only the recipient can reject
        if (friendship.FriendUserId != currentUserId)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "You can only reject friend requests addressed to you."
            };
        }

        friendship.Status = "Rejected";
        friendship.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} rejected friend request {FriendshipId}", currentUserId, friendshipId);

        return new SocialOperationResultDto
        {
            IsSuccess = true,
            Message = "Friend request declined.",
            FriendshipStatus = "None",
            FriendshipId = friendshipId
        };
    }

    public async Task<SocialOperationResultDto> CancelFriendRequestAsync(int currentUserId, int friendshipId)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId);

        if (friendship == null)
        {
            return new SocialOperationResultDto { IsSuccess = false, Message = "Friend request not found." };
        }

        // Security check: only the sender can cancel their own pending request
        if (friendship.UserId != currentUserId)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "You can only cancel friend requests sent by you."
            };
        }

        if (friendship.Status != "Pending")
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "Only pending friend requests can be cancelled."
            };
        }

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} cancelled sent friend request {FriendshipId}", currentUserId, friendshipId);

        return new SocialOperationResultDto
        {
            IsSuccess = true,
            Message = "Friend request cancelled.",
            FriendshipStatus = "None"
        };
    }

    public async Task<SocialOperationResultDto> RemoveFriendAsync(int currentUserId, int friendshipId)
    {
        var friendship = await _context.Friendships
            .Include(f => f.User)
            .Include(f => f.FriendUser)
            .FirstOrDefaultAsync(f => f.FriendshipId == friendshipId);

        if (friendship == null)
        {
            return new SocialOperationResultDto { IsSuccess = false, Message = "Friendship record not found." };
        }

        // Security check: current user must be one of the members of the friendship
        if (friendship.UserId != currentUserId && friendship.FriendUserId != currentUserId)
        {
            return new SocialOperationResultDto
            {
                IsSuccess = false,
                Message = "You cannot remove another user's friendship."
            };
        }

        var friendUser = friendship.UserId == currentUserId ? friendship.FriendUser : friendship.User;

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} dissolved friendship {FriendshipId} with {FriendUserId}",
            currentUserId, friendshipId, friendUser.UserId);

        return new SocialOperationResultDto
        {
            IsSuccess = true,
            Message = $"Removed {friendUser.DisplayName} from your besties list.",
            FriendshipStatus = "None"
        };
    }

    public async Task<List<FriendshipDto>> GetIncomingFriendRequestsAsync(int currentUserId)
    {
        var requests = await _context.Friendships
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.FriendUser)
            .Where(f => f.FriendUserId == currentUserId && f.Status == "Pending")
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return requests.Select(MapToDto).ToList();
    }

    public async Task<List<FriendshipDto>> GetOutgoingFriendRequestsAsync(int currentUserId)
    {
        var requests = await _context.Friendships
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.FriendUser)
            .Where(f => f.UserId == currentUserId && f.Status == "Pending")
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return requests.Select(MapToDto).ToList();
    }

    public async Task<List<FriendshipDto>> GetFriendsListAsync(int currentUserId, string? query = null)
    {
        var q = _context.Friendships
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.FriendUser)
            .Where(f => (f.UserId == currentUserId || f.FriendUserId == currentUserId) && f.Status == "Accepted");

        var friendships = await q.OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt).ToListAsync();

        var dtos = friendships.Select(MapToDto);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            dtos = dtos.Where(f =>
                f.FriendUsername(currentUserId).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                f.FriendDisplayName(currentUserId).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return dtos.ToList();
    }

    public async Task<string> GetFriendshipStatusAsync(int currentUserId, int targetUserId)
    {
        if (currentUserId == targetUserId) return "Self";

        // Check blocks
        var isBlockedByViewer = await _context.UserBlocks.AnyAsync(b => b.BlockerUserId == currentUserId && b.BlockedUserId == targetUserId);
        if (isBlockedByViewer) return "Blocked";

        var isBlockedByTarget = await _context.UserBlocks.AnyAsync(b => b.BlockerUserId == targetUserId && b.BlockedUserId == currentUserId);
        if (isBlockedByTarget) return "BlockedBy";

        var userA = Math.Min(currentUserId, targetUserId);
        var userB = Math.Max(currentUserId, targetUserId);

        var f = await _context.Friendships.AsNoTracking().FirstOrDefaultAsync(x => x.UserA == userA && x.UserB == userB);
        if (f == null || f.Status == "Rejected") return "None";

        if (f.Status == "Accepted") return "Accepted";

        if (f.Status == "Pending")
        {
            return f.UserId == currentUserId ? "PendingSent" : "PendingReceived";
        }

        return "None";
    }

    private static FriendshipDto MapToDto(Friendship f)
    {
        return new FriendshipDto
        {
            FriendshipId = f.FriendshipId,
            RequesterUserId = f.UserId,
            RequesterUsername = f.User.Username,
            RequesterDisplayName = f.User.DisplayName,
            RequesterProfilePicture = f.User.ProfilePicture,
            RequesterAvatarSticker = f.User.AvatarSticker,
            AddresseeUserId = f.FriendUserId,
            AddresseeUsername = f.FriendUser.Username,
            AddresseeDisplayName = f.FriendUser.DisplayName,
            AddresseeProfilePicture = f.FriendUser.ProfilePicture,
            AddresseeAvatarSticker = f.FriendUser.AvatarSticker,
            Status = f.Status,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }
}
