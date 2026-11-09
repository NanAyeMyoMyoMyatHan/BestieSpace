using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Shared.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Social;

public interface IUserProfileService
{
    Task<UserProfileDto?> GetMyProfileAsync(int userId);
    Task<UserProfileDto?> GetProfileByUsernameAsync(string username, int? viewingUserId = null);
    Task<UserProfileDto?> GetProfileByIdAsync(int targetUserId, int? viewingUserId = null);
    Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateUserProfileRequestDto request, int actingUserId);
    Task<string> UploadProfilePictureAsync(int userId, Stream fileStream, string originalFileName, string contentType, long fileLength);
}

public class UserProfileService : IUserProfileService
{
    private readonly BestieDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UserProfileService> _logger;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public UserProfileService(
        BestieDbContext context,
        IWebHostEnvironment env,
        ILogger<UserProfileService> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    public async Task<UserProfileDto?> GetMyProfileAsync(int userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return null;

        var friendCount = await _context.Friendships
            .CountAsync(f => (f.UserId == userId || f.FriendUserId == userId) && f.Status == "Accepted");

        return new UserProfileDto
        {
            UserId = user.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            ProfilePicture = user.ProfilePicture,
            AvatarSticker = user.AvatarSticker,
            Bio = user.Bio,
            Email = user.Email, // Viewable on own profile
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsActive = user.IsActive,
            IsOnline = user.IsOnline,
            LastSeenAt = user.LastSeenAt,
            FriendCount = friendCount,
            FriendshipStatusWithViewer = "Self"
        };
    }

    public async Task<UserProfileDto?> GetProfileByUsernameAsync(string username, int? viewingUserId = null)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        var normalized = username.Trim().ToUpperInvariant();
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUsername == normalized);
        if (user == null || !user.IsActive) return null;

        return await BuildProfileDtoAsync(user, viewingUserId);
    }

    public async Task<UserProfileDto?> GetProfileByIdAsync(int targetUserId, int? viewingUserId = null)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == targetUserId);
        if (user == null || !user.IsActive) return null;

        return await BuildProfileDtoAsync(user, viewingUserId);
    }

    private async Task<UserProfileDto> BuildProfileDtoAsync(Infrastructure.Data.Entities.User user, int? viewingUserId)
    {
        var friendCount = await _context.Friendships
            .CountAsync(f => (f.UserId == user.UserId || f.FriendUserId == user.UserId) && f.Status == "Accepted");

        var statusWithViewer = "None";
        int? activeFriendshipId = null;

        if (viewingUserId.HasValue)
        {
            if (viewingUserId.Value == user.UserId)
            {
                statusWithViewer = "Self";
            }
            else
            {
                var vId = viewingUserId.Value;
                var tId = user.UserId;

                // Check block relationships
                var isBlockedByViewer = await _context.UserBlocks.AnyAsync(b => b.BlockerUserId == vId && b.BlockedUserId == tId);
                var isBlockedByTarget = await _context.UserBlocks.AnyAsync(b => b.BlockerUserId == tId && b.BlockedUserId == vId);

                if (isBlockedByViewer)
                {
                    statusWithViewer = "Blocked";
                }
                else if (isBlockedByTarget)
                {
                    statusWithViewer = "BlockedBy";
                }
                else
                {
                    var userA = Math.Min(vId, tId);
                    var userB = Math.Max(vId, tId);
                    var friendship = await _context.Friendships
                        .AsNoTracking()
                        .FirstOrDefaultAsync(f => f.UserA == userA && f.UserB == userB);

                    if (friendship != null)
                    {
                        activeFriendshipId = friendship.FriendshipId;
                        if (friendship.Status == "Accepted")
                        {
                            statusWithViewer = "Accepted";
                        }
                        else if (friendship.Status == "Pending")
                        {
                            statusWithViewer = friendship.UserId == vId ? "PendingSent" : "PendingReceived";
                        }
                        else if (friendship.Status == "Rejected")
                        {
                            statusWithViewer = "None";
                        }
                    }
                }
            }
        }

        return new UserProfileDto
        {
            UserId = user.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            ProfilePicture = user.ProfilePicture,
            AvatarSticker = user.AvatarSticker,
            Bio = user.Bio,
            Email = viewingUserId.HasValue && viewingUserId.Value == user.UserId ? user.Email : null, // Privacy protected
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsActive = user.IsActive,
            IsOnline = user.IsOnline,
            LastSeenAt = user.LastSeenAt,
            FriendCount = friendCount,
            FriendshipStatusWithViewer = statusWithViewer,
            ActiveFriendshipId = activeFriendshipId
        };
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateUserProfileRequestDto request, int actingUserId)
    {
        // Enforce ownership: only the user themselves can update their profile (unless administrative override)
        if (actingUserId != userId)
        {
            var isSuperAdmin = await _context.UserRoles.AnyAsync(ur => ur.UserId == actingUserId && ur.Role.RoleName == "SuperAdmin");
            if (!isSuperAdmin)
            {
                throw new UnauthorizedAccessException("You are not authorized to edit another user's profile.");
            }
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || !user.IsActive)
        {
            throw new ArgumentException("User not found or account is deactivated.");
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName.Trim();
        }

        user.Bio = request.Bio?.Trim();
        
        if (!string.IsNullOrWhiteSpace(request.AvatarSticker))
        {
            user.AvatarSticker = request.AvatarSticker.Trim();
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated profile for UserId {UserId} (DisplayName: {DisplayName})", userId, user.DisplayName);
        return (await GetMyProfileAsync(userId))!;
    }

    public async Task<string> UploadProfilePictureAsync(int userId, Stream fileStream, string originalFileName, string contentType, long fileLength)
    {
        if (fileLength <= 0 || fileStream == null)
        {
            throw new ArgumentException("Uploaded file is empty.");
        }

        if (fileLength > MaxFileSize)
        {
            throw new ArgumentException($"File size exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)} MB.");
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException($"File format '{ext}' is not supported. Please upload PNG, JPG, WEBP, or GIF.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || !user.IsActive)
        {
            throw new ArgumentException("User not found or account is deactivated.");
        }

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "profile-pictures");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var uniqueFileName = $"pfp_{userId}_{Guid.NewGuid():N}{ext}";
        var physicalPath = Path.Combine(uploadsDir, uniqueFileName);

        using (var destStream = new FileStream(physicalPath, FileMode.Create))
        {
            await fileStream.CopyToAsync(destStream);
        }

        // Clean up previous custom profile picture if exists and in uploads folder
        if (!string.IsNullOrWhiteSpace(user.ProfilePicture) && user.ProfilePicture.StartsWith("/uploads/profile-pictures/"))
        {
            try
            {
                var oldFileName = Path.GetFileName(user.ProfilePicture);
                var oldPath = Path.Combine(uploadsDir, oldFileName);
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete old profile picture for user {UserId}", userId);
            }
        }

        var relativeUrl = $"/uploads/profile-pictures/{uniqueFileName}";
        user.ProfilePicture = relativeUrl;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Profile picture updated for user {UserId}: {Url}", userId, relativeUrl);
        return relativeUrl;
    }
}
