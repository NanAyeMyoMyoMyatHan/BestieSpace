using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Circles;

public interface IBestieCircleService
{
    Task<CircleDetailDto> CreateCircleAsync(int ownerUserId, CreateCircleRequestDto request);
    Task<List<CircleDto>> GetMyCirclesAsync(int userId);
    Task<CircleDetailDto?> GetCircleDetailsAsync(int circleId, int viewingUserId);
    Task<CircleDetailDto> UpdateCircleAsync(int circleId, UpdateCircleRequestDto request, int actingUserId);
    Task<CircleOperationResultDto> DeactivateOrDeleteCircleAsync(int circleId, int actingUserId);
    Task<int> GetCircleMemberCountAsync(int circleId);
}

public class BestieCircleService : IBestieCircleService
{
    private readonly BestieDbContext _context;
    private readonly ICircleAuthorizationService _circleAuthService;
    private readonly ILogger<BestieCircleService> _logger;

    public BestieCircleService(
        BestieDbContext context,
        ICircleAuthorizationService circleAuthService,
        ILogger<BestieCircleService> logger)
    {
        _context = context;
        _circleAuthService = circleAuthService;
        _logger = logger;
    }

    public async Task<CircleDetailDto> CreateCircleAsync(int ownerUserId, CreateCircleRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CircleName))
        {
            throw new ArgumentException("Circle name cannot be empty.");
        }

        var ownerUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == ownerUserId && u.IsActive);
        if (ownerUser == null)
        {
            throw new ArgumentException("User not found or account is deactivated.");
        }

        // Find CircleOwner role (fallback to Owner role if needed)
        var ownerRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleName == "CircleOwner" || r.RoleName == "Owner");
        if (ownerRole == null)
        {
            ownerRole = new Role
            {
                RoleName = "CircleOwner",
                Description = "Sole owner and creator of a Bestie Circle",
                IsSystemRole = false
            };
            _context.Roles.Add(ownerRole);
            await _context.SaveChangesAsync();
        }

        // Use database transaction to create circle and owner membership atomically
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var newCircle = new BestieCircle
            {
                CircleName = request.CircleName.Trim(),
                Description = request.Description?.Trim(),
                CircleIcon = string.IsNullOrWhiteSpace(request.CircleIcon) ? "🌸" : request.CircleIcon.Trim(),
                CircleImage = request.CircleImage?.Trim(),
                OwnerUserId = ownerUserId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.BestieCircles.Add(newCircle);
            await _context.SaveChangesAsync();

            // Automatically add owner as the first member with CircleOwner role
            var ownerMembership = new BestieCircleMember
            {
                CircleId = newCircle.CircleId,
                UserId = ownerUserId,
                RoleId = ownerRole.RoleId,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.BestieCircleMembers.Add(ownerMembership);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Bestie Circle '{CircleName}' (ID: {CircleId}) created by Owner UserId: {OwnerId}",
                newCircle.CircleName, newCircle.CircleId, ownerUserId);

            return (await GetCircleDetailsAsync(newCircle.CircleId, ownerUserId))!;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create circle for user {UserId}", ownerUserId);
            throw;
        }
    }

    public async Task<List<CircleDto>> GetMyCirclesAsync(int userId)
    {
        var circles = await _context.BestieCircles
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.Members)
                .ThenInclude(m => m.Role)
            .Where(c => c.IsActive && c.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return circles.Select(c =>
        {
            var membership = c.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
            var roleName = membership?.Role.RoleName ?? "CircleMember";
            var isOwner = c.OwnerUserId == userId || roleName == "CircleOwner" || roleName == "Owner";

            return new CircleDto
            {
                CircleId = c.CircleId,
                CircleName = c.CircleName,
                Description = c.Description,
                CircleIcon = c.CircleIcon,
                CircleImage = c.CircleImage,
                OwnerUserId = c.OwnerUserId,
                OwnerUsername = c.Owner.Username,
                OwnerDisplayName = c.Owner.DisplayName,
                OwnerProfilePicture = c.Owner.ProfilePicture,
                OwnerAvatarSticker = c.Owner.AvatarSticker,
                MemberCount = c.Members.Count(m => m.IsActive),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsActive = c.IsActive,
                MyRole = roleName,
                IsOwner = isOwner,
                CanInvite = isOwner || roleName == "CircleAdmin",
                CanManageSettings = isOwner
            };
        }).ToList();
    }

    public async Task<CircleDetailDto?> GetCircleDetailsAsync(int circleId, int viewingUserId)
    {
        var circle = await _context.BestieCircles
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.Role)
            .FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);

        if (circle == null) return null;

        // Privacy rule: Only active members can view private circle details!
        var viewerMembership = circle.Members.FirstOrDefault(m => m.UserId == viewingUserId && m.IsActive);
        if (viewerMembership == null)
        {
            _logger.LogWarning("Access denied: User {UserId} is not a member of Circle {CircleId}", viewingUserId, circleId);
            return null;
        }

        var viewerRole = viewerMembership.Role.RoleName;
        var isOwner = circle.OwnerUserId == viewingUserId || viewerRole == "CircleOwner" || viewerRole == "Owner";
        var canInvite = isOwner || viewerRole == "CircleAdmin";

        // Pending invitations (visible to Owner and Admin)
        var pendingInvites = new List<CircleInvitationDto>();
        if (canInvite)
        {
            var invites = await _context.CircleInvitations
                .AsNoTracking()
                .Include(i => i.InviterUser)
                .Include(i => i.InviteeUser)
                .Where(i => i.CircleId == circleId && i.Status == "Pending")
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            pendingInvites = invites.Select(i => new CircleInvitationDto
            {
                InvitationId = i.InvitationId,
                CircleId = i.CircleId,
                CircleName = circle.CircleName,
                CircleIcon = circle.CircleIcon,
                CircleMemberCount = circle.Members.Count(m => m.IsActive),
                InviterUserId = i.InviterUserId,
                InviterUsername = i.InviterUser.Username,
                InviterDisplayName = i.InviterUser.DisplayName,
                InviterProfilePicture = i.InviterUser.ProfilePicture,
                InviterAvatarSticker = i.InviterUser.AvatarSticker,
                InviteeUserId = i.InviteeUserId,
                InviteeUsername = i.InviteeUser.Username,
                InviteeDisplayName = i.InviteeUser.DisplayName,
                InviteeProfilePicture = i.InviteeUser.ProfilePicture,
                InviteeAvatarSticker = i.InviteeUser.AvatarSticker,
                Status = i.Status,
                CreatedAt = i.CreatedAt,
                RespondedAt = i.RespondedAt
            }).ToList();
        }

        var memberDtos = circle.Members
            .Where(m => m.IsActive)
            .OrderBy(m => m.Role.RoleName == "CircleOwner" || m.Role.RoleName == "Owner" ? 0 : m.Role.RoleName == "CircleAdmin" ? 1 : 2)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new CircleMemberDto
            {
                CircleMemberId = m.CircleMemberId,
                CircleId = m.CircleId,
                UserId = m.UserId,
                Username = m.User.Username,
                DisplayName = m.User.DisplayName,
                ProfilePicture = m.User.ProfilePicture,
                AvatarSticker = m.User.AvatarSticker,
                RoleId = m.RoleId,
                RoleName = m.Role.RoleName,
                JoinedAt = m.JoinedAt,
                IsActive = m.IsActive,
                IsOwner = m.UserId == circle.OwnerUserId,
                IsOnline = m.User.IsOnline
            }).ToList();

        return new CircleDetailDto
        {
            CircleId = circle.CircleId,
            CircleName = circle.CircleName,
            Description = circle.Description,
            CircleIcon = circle.CircleIcon,
            CircleImage = circle.CircleImage,
            OwnerUserId = circle.OwnerUserId,
            OwnerUsername = circle.Owner.Username,
            OwnerDisplayName = circle.Owner.DisplayName,
            OwnerProfilePicture = circle.Owner.ProfilePicture,
            OwnerAvatarSticker = circle.Owner.AvatarSticker,
            MemberCount = memberDtos.Count,
            CreatedAt = circle.CreatedAt,
            UpdatedAt = circle.UpdatedAt,
            IsActive = circle.IsActive,
            MyRole = viewerRole,
            IsOwner = isOwner,
            CanInvite = canInvite,
            CanManageSettings = isOwner,
            Members = memberDtos,
            PendingInvitations = pendingInvites
        };
    }

    public async Task<CircleDetailDto> UpdateCircleAsync(int circleId, UpdateCircleRequestDto request, int actingUserId)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            throw new ArgumentException("Circle not found.");
        }

        // Only owner has CIRCLE_UPDATE permission
        var hasPerm = await _circleAuthService.HasCirclePermissionAsync(actingUserId, circleId, "CIRCLE_UPDATE");
        if (!hasPerm && circle.OwnerUserId != actingUserId)
        {
            throw new UnauthorizedAccessException("Only the circle owner can modify circle settings.");
        }

        if (!string.IsNullOrWhiteSpace(request.CircleName))
        {
            circle.CircleName = request.CircleName.Trim();
        }

        circle.Description = request.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(request.CircleIcon))
        {
            circle.CircleIcon = request.CircleIcon.Trim();
        }
        if (request.CircleImage != null)
        {
            circle.CircleImage = request.CircleImage.Trim();
        }

        circle.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Circle {CircleId} updated by user {UserId}", circleId, actingUserId);

        return (await GetCircleDetailsAsync(circleId, actingUserId))!;
    }

    public async Task<CircleOperationResultDto> DeactivateOrDeleteCircleAsync(int circleId, int actingUserId)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Circle not found." };
        }

        // Only owner can delete the circle
        if (circle.OwnerUserId != actingUserId)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Only the circle owner can delete the circle." };
        }

        // Soft-delete circle
        circle.IsActive = false;
        circle.UpdatedAt = DateTime.UtcNow;

        // Cancel pending invitations
        var pendingInvites = await _context.CircleInvitations
            .Where(i => i.CircleId == circleId && i.Status == "Pending")
            .ToListAsync();

        foreach (var invite in pendingInvites)
        {
            invite.Status = "Cancelled";
            invite.RespondedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Circle {CircleId} deactivated/deleted by owner {UserId}", circleId, actingUserId);

        return new CircleOperationResultDto
        {
            IsSuccess = true,
            Message = $"Circle '{circle.CircleName}' has been successfully deleted.",
            CircleId = circleId
        };
    }

    public async Task<int> GetCircleMemberCountAsync(int circleId)
    {
        return await _context.BestieCircleMembers
            .CountAsync(m => m.CircleId == circleId && m.IsActive);
    }
}
