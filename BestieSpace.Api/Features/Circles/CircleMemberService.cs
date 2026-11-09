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

public interface ICircleMemberService
{
    Task<List<CircleMemberDto>> GetCircleMembersAsync(int circleId, int viewingUserId);
    Task<CircleOperationResultDto> RemoveMemberAsync(int circleId, int memberUserIdToRemove, int actingUserId);
    Task<CircleOperationResultDto> LeaveCircleAsync(int circleId, int actingUserId);
    Task<CircleOperationResultDto> ChangeMemberRoleAsync(int circleId, ChangeMemberRoleRequestDto request, int actingUserId);
    Task<CircleOperationResultDto> TransferOwnershipAsync(int circleId, TransferCircleOwnershipRequestDto request, int actingUserId);
}

public class CircleMemberService : ICircleMemberService
{
    private readonly BestieDbContext _context;
    private readonly ICircleAuthorizationService _circleAuthService;
    private readonly ILogger<CircleMemberService> _logger;

    public CircleMemberService(
        BestieDbContext context,
        ICircleAuthorizationService circleAuthService,
        ILogger<CircleMemberService> logger)
    {
        _context = context;
        _circleAuthService = circleAuthService;
        _logger = logger;
    }

    public async Task<List<CircleMemberDto>> GetCircleMembersAsync(int circleId, int viewingUserId)
    {
        // Privacy check: viewer must be an active member of the circle
        var isMember = await _circleAuthService.IsCircleMemberAsync(viewingUserId, circleId);
        if (!isMember)
        {
            return new List<CircleMemberDto>();
        }

        var members = await _context.BestieCircleMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Role)
            .Include(m => m.Circle)
            .Where(m => m.CircleId == circleId && m.IsActive)
            .OrderBy(m => m.UserId == m.Circle.OwnerUserId ? 0 : m.Role.RoleName == "CircleAdmin" ? 1 : 2)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync();

        return members.Select(m => new CircleMemberDto
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
            IsOwner = m.UserId == m.Circle.OwnerUserId,
            IsOnline = m.User.IsOnline
        }).ToList();
    }

    public async Task<CircleOperationResultDto> RemoveMemberAsync(int circleId, int memberUserIdToRemove, int actingUserId)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Circle not found." };
        }

        // Critical safety rule: The owner CANNOT be removed!
        if (memberUserIdToRemove == circle.OwnerUserId)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "The circle owner cannot be removed from the circle."
            };
        }

        var canRemove = await _circleAuthService.HasCirclePermissionAsync(actingUserId, circleId, "CIRCLE_MEMBER_REMOVE");
        var isOwner = circle.OwnerUserId == actingUserId;

        if (!canRemove && !isOwner)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "You do not have permission to remove members from this circle."
            };
        }

        var memberToRemove = await _context.BestieCircleMembers
            .Include(m => m.Role)
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == memberUserIdToRemove && m.IsActive);

        if (memberToRemove == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Member not found in this circle." };
        }

        // Safety rule: CircleAdmin cannot remove another CircleAdmin or the Owner (only the Owner can do that)
        if (!isOwner && (memberToRemove.Role.RoleName == "CircleAdmin" || memberToRemove.Role.RoleName == "CircleOwner"))
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "Circle administrators cannot remove other administrators. Only the owner can remove admins."
            };
        }

        memberToRemove.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Member {MemberId} removed from Circle {CircleId} by actor {ActorId}",
            memberUserIdToRemove, circleId, actingUserId);

        return new CircleOperationResultDto
        {
            IsSuccess = true,
            Message = $"Removed {memberToRemove.User.DisplayName} from the circle.",
            CircleId = circleId
        };
    }

    public async Task<CircleOperationResultDto> LeaveCircleAsync(int circleId, int actingUserId)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Circle not found." };
        }

        // Critical safety rule: The owner cannot leave without transferring ownership or deleting the circle
        if (circle.OwnerUserId == actingUserId)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "The circle owner cannot leave the circle! Please transfer ownership to another member or delete the circle."
            };
        }

        var membership = await _context.BestieCircleMembers
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == actingUserId && m.IsActive);

        if (membership == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "You are not an active member of this circle." };
        }

        membership.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} left Circle {CircleId}", actingUserId, circleId);

        return new CircleOperationResultDto
        {
            IsSuccess = true,
            Message = $"You have left '{circle.CircleName}'.",
            CircleId = circleId
        };
    }

    public async Task<CircleOperationResultDto> ChangeMemberRoleAsync(int circleId, ChangeMemberRoleRequestDto request, int actingUserId)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Circle not found." };
        }

        // Only owner can change member roles
        var canUpdateRole = await _circleAuthService.HasCirclePermissionAsync(actingUserId, circleId, "CIRCLE_MEMBER_ROLE_UPDATE");
        if (!canUpdateRole && circle.OwnerUserId != actingUserId)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "Only the circle owner can promote or demote member roles."
            };
        }

        if (request.TargetUserId == circle.OwnerUserId)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "Cannot change role of the circle owner directly. Use 'Transfer Ownership' instead."
            };
        }

        var member = await _context.BestieCircleMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == request.TargetUserId && m.IsActive);

        if (member == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Member not found in this circle." };
        }

        var targetRoleName = request.NewRoleName.Trim();
        if (targetRoleName != "CircleAdmin" && targetRoleName != "CircleMember")
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "Invalid circle role specified. Allowed values are 'CircleAdmin' or 'CircleMember'."
            };
        }

        var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == targetRoleName);
        if (newRole == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = $"Role '{targetRoleName}' does not exist." };
        }

        member.RoleId = newRole.RoleId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role of member {TargetId} in Circle {CircleId} changed to {RoleName} by owner {OwnerId}",
            request.TargetUserId, circleId, targetRoleName, actingUserId);

        return new CircleOperationResultDto
        {
            IsSuccess = true,
            Message = $"Role for {member.User.DisplayName} updated to {targetRoleName} ✨",
            CircleId = circleId
        };
    }

    public async Task<CircleOperationResultDto> TransferOwnershipAsync(int circleId, TransferCircleOwnershipRequestDto request, int actingUserId)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == circleId && c.IsActive);
        if (circle == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Circle not found." };
        }

        // Only the current owner can transfer ownership
        if (circle.OwnerUserId != actingUserId)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "Only the current circle owner can transfer ownership."
            };
        }

        if (request.NewOwnerUserId == actingUserId)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "You already own this circle."
            };
        }

        var newOwnerMember = await _context.BestieCircleMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == request.NewOwnerUserId && m.IsActive);

        if (newOwnerMember == null)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "The designated new owner must be an active member of this circle."
            };
        }

        var currentOwnerMember = await _context.BestieCircleMembers
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == actingUserId && m.IsActive);

        var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "CircleOwner" || r.RoleName == "Owner");
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "CircleAdmin");

        if (ownerRole == null || adminRole == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Required circle roles are not configured." };
        }

        // Atomic ownership transfer in transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Update circle's OwnerUserId
            circle.OwnerUserId = request.NewOwnerUserId;
            circle.UpdatedAt = DateTime.UtcNow;

            // 2. Promote new owner member to CircleOwner role
            newOwnerMember.RoleId = ownerRole.RoleId;

            // 3. Demote previous owner to CircleAdmin role so they remain an administrator
            if (currentOwnerMember != null)
            {
                currentOwnerMember.RoleId = adminRole.RoleId;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Ownership of Circle {CircleId} transferred from user {OldOwnerId} to {NewOwnerId}",
                circleId, actingUserId, request.NewOwnerUserId);

            return new CircleOperationResultDto
            {
                IsSuccess = true,
                Message = $"Circle ownership successfully transferred to {newOwnerMember.User.DisplayName}! 👑",
                CircleId = circleId
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to transfer ownership of Circle {CircleId}", circleId);
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "Error transferring ownership. Please try again."
            };
        }
    }
}
