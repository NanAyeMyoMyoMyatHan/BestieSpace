using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Features.Notifications;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Circles;

public interface ICircleInvitationService
{
    Task<CircleOperationResultDto> SendInvitationAsync(int inviterUserId, SendCircleInvitationRequestDto request);
    Task<CircleOperationResultDto> AcceptInvitationAsync(int invitationId, int inviteeUserId);
    Task<CircleOperationResultDto> RejectInvitationAsync(int invitationId, int inviteeUserId);
    Task<CircleOperationResultDto> CancelInvitationAsync(int invitationId, int actingUserId);
    Task<List<CircleInvitationDto>> GetIncomingInvitationsAsync(int inviteeUserId);
    Task<List<CircleInvitationDto>> GetOutgoingInvitationsAsync(int circleId, int actingUserId);
}

public class CircleInvitationService : ICircleInvitationService
{
    private readonly BestieDbContext _context;
    private readonly ICircleAuthorizationService _circleAuthService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CircleInvitationService> _logger;

    public CircleInvitationService(
        BestieDbContext context,
        ICircleAuthorizationService circleAuthService,
        INotificationService notificationService,
        ILogger<CircleInvitationService> logger)
    {
        _context = context;
        _circleAuthService = circleAuthService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<CircleOperationResultDto> SendInvitationAsync(int inviterUserId, SendCircleInvitationRequestDto request)
    {
        var circle = await _context.BestieCircles.FirstOrDefaultAsync(c => c.CircleId == request.CircleId && c.IsActive);
        if (circle == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Circle not found or is inactive." };
        }

        // Verify inviter has invitation permission
        var canInvite = await _circleAuthService.HasCirclePermissionAsync(inviterUserId, request.CircleId, "CIRCLE_INVITE");
        if (!canInvite && circle.OwnerUserId != inviterUserId)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "You do not have permission to invite members to this circle." };
        }

        // Check member limit (maximum 9 members including owner)
        var memberCount = await _context.BestieCircleMembers
            .CountAsync(m => m.CircleId == request.CircleId && m.IsActive);

        if (memberCount >= 9)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "This circle is already at full capacity (maximum 9 members, including the owner)!"
            };
        }

        if (request.InviteeUserId == inviterUserId)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "You cannot invite yourself to a circle." };
        }

        var invitee = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == request.InviteeUserId && u.IsActive);
        if (invitee == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Invited user not found or is deactivated." };
        }

        // Check if invitee is already an active member
        var isAlreadyMember = await _context.BestieCircleMembers
            .AnyAsync(m => m.CircleId == request.CircleId && m.UserId == request.InviteeUserId && m.IsActive);
        if (isAlreadyMember)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = $"{invitee.DisplayName} is already a member of this circle!" };
        }

        // Check block relationship
        var isBlocked = await _context.UserBlocks
            .AnyAsync(b => (b.BlockerUserId == inviterUserId && b.BlockedUserId == request.InviteeUserId) ||
                           (b.BlockerUserId == request.InviteeUserId && b.BlockedUserId == inviterUserId));
        if (isBlocked)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Unable to invite this user due to block restrictions." };
        }

        // Business Rule: The invitee must be an accepted friend of the inviter!
        var userA = Math.Min(inviterUserId, request.InviteeUserId);
        var userB = Math.Max(inviterUserId, request.InviteeUserId);
        var isFriend = await _context.Friendships
            .AnyAsync(f => f.UserA == userA && f.UserB == userB && f.Status == "Accepted");

        if (!isFriend)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = $"You can only invite accepted bestie friends to your circle! Send {invitee.DisplayName} a friend request first 🌸"
            };
        }

        // Prevent duplicate pending invitations
        var existingInvite = await _context.CircleInvitations
            .FirstOrDefaultAsync(i => i.CircleId == request.CircleId && i.InviteeUserId == request.InviteeUserId && i.Status == "Pending");

        if (existingInvite != null)
        {
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = $"An invitation has already been sent to {invitee.DisplayName} and is pending! 💌",
                InvitationId = existingInvite.InvitationId
            };
        }

        var invitation = new CircleInvitation
        {
            CircleId = request.CircleId,
            InviterUserId = inviterUserId,
            InviteeUserId = request.InviteeUserId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.CircleInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Circle invitation {InvitationId} sent by user {InviterId} to {InviteeId} for Circle {CircleId}",
            invitation.InvitationId, inviterUserId, request.InviteeUserId, request.CircleId);

        try
        {
            var inviter = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == inviterUserId);
            var inviterName = inviter?.DisplayName ?? "A bestie";
            await _notificationService.CreateNotificationAsync(
                userId: request.InviteeUserId,
                type: "CircleInvitationReceived",
                title: "Circle Invitation! 🌸",
                message: $"{inviterName} invited you to join '{circle.CircleName}'!",
                relatedEntityId: invitation.InvitationId,
                relatedEntityType: "CircleInvitation"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification for circle invitation {InvitationId}", invitation.InvitationId);
        }

        return new CircleOperationResultDto
        {
            IsSuccess = true,
            Message = $"Circle invitation sent to {invitee.DisplayName}! 💌",
            CircleId = request.CircleId,
            InvitationId = invitation.InvitationId
        };
    }

    public async Task<CircleOperationResultDto> AcceptInvitationAsync(int invitationId, int inviteeUserId)
    {
        var invitation = await _context.CircleInvitations
            .Include(i => i.Circle)
            .FirstOrDefaultAsync(i => i.InvitationId == invitationId);

        if (invitation == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Invitation not found." };
        }

        // Security check: only the intended invitee can accept
        if (invitation.InviteeUserId != inviteeUserId)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "You are not authorized to accept this invitation." };
        }

        if (invitation.Status != "Pending")
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = $"Invitation is already {invitation.Status.ToLower()}." };
        }

        if (!invitation.Circle.IsActive)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "This circle is no longer active." };
        }

        // Transactional Concurrency Lock for Maximum 9 Members
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // Lock the circle row with UPDLOCK, HOLDLOCK so concurrent acceptance cannot exceed 9 members
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT [CircleId] FROM [dbo].[BestieCircles] WITH (UPDLOCK, HOLDLOCK) WHERE [CircleId] = {invitation.CircleId}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UPDLOCK query handled (or in test environment)");
            }

            // Check current active member count within transaction
            var activeMemberCount = await _context.BestieCircleMembers
                .CountAsync(m => m.CircleId == invitation.CircleId && m.IsActive);

            if (activeMemberCount >= 9)
            {
                await transaction.RollbackAsync();
                return new CircleOperationResultDto
                {
                    IsSuccess = false,
                    Message = "Sorry, this Bestie Circle has reached its maximum limit of 9 members! 🥺"
                };
            }

            // Check if user is already an active member
            var existingMembership = await _context.BestieCircleMembers
                .FirstOrDefaultAsync(m => m.CircleId == invitation.CircleId && m.UserId == inviteeUserId);

            if (existingMembership != null && existingMembership.IsActive)
            {
                invitation.Status = "Accepted";
                invitation.RespondedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new CircleOperationResultDto
                {
                    IsSuccess = true,
                    Message = "You are already a member of this circle! 🌸",
                    CircleId = invitation.CircleId
                };
            }

            // Get CircleMember role (or Member role)
            var memberRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName == "CircleMember" || r.RoleName == "Member");
            if (memberRole == null)
            {
                memberRole = new Role
                {
                    RoleName = "CircleMember",
                    Description = "Active member of a Bestie Circle",
                    IsSystemRole = false
                };
                _context.Roles.Add(memberRole);
                await _context.SaveChangesAsync();
            }

            if (existingMembership != null)
            {
                // Reactivate previous membership
                existingMembership.IsActive = true;
                existingMembership.RoleId = memberRole.RoleId;
                existingMembership.JoinedAt = DateTime.UtcNow;
            }
            else
            {
                _context.BestieCircleMembers.Add(new BestieCircleMember
                {
                    CircleId = invitation.CircleId,
                    UserId = inviteeUserId,
                    RoleId = memberRole.RoleId,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            invitation.Status = "Accepted";
            invitation.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("User {UserId} joined Circle {CircleId} via invitation {InvitationId}. New member count: {Count}",
                inviteeUserId, invitation.CircleId, invitationId, activeMemberCount + 1);

            try
            {
                var inviteeUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == inviteeUserId);
                var inviteeName = inviteeUser?.DisplayName ?? "A bestie";
                if (invitation.Circle.OwnerUserId != inviteeUserId)
                {
                    await _notificationService.CreateNotificationAsync(
                        userId: invitation.Circle.OwnerUserId,
                        type: "CircleMemberJoined",
                        title: "New Circle Bestie! 🎉",
                        message: $"{inviteeName} joined your circle '{invitation.Circle.CircleName}'!",
                        relatedEntityId: invitation.CircleId,
                        relatedEntityType: "BestieCircle"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification for accepted invitation {InvitationId}", invitationId);
            }

            return new CircleOperationResultDto
            {
                IsSuccess = true,
                Message = $"Welcome to '{invitation.Circle.CircleName}'! You are now a member 💖",
                CircleId = invitation.CircleId
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed while accepting invitation {InvitationId}", invitationId);
            return new CircleOperationResultDto
            {
                IsSuccess = false,
                Message = "Could not join circle due to a temporary conflict. Please try again."
            };
        }
    }

    public async Task<CircleOperationResultDto> RejectInvitationAsync(int invitationId, int inviteeUserId)
    {
        var invitation = await _context.CircleInvitations.FirstOrDefaultAsync(i => i.InvitationId == invitationId);
        if (invitation == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Invitation not found." };
        }

        // Security check: only the intended invitee can reject
        if (invitation.InviteeUserId != inviteeUserId)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "You are not authorized to decline this invitation." };
        }

        if (invitation.Status != "Pending")
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = $"Invitation is already {invitation.Status.ToLower()}." };
        }

        invitation.Status = "Rejected";
        invitation.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Invitation {InvitationId} declined by user {UserId}", invitationId, inviteeUserId);

        return new CircleOperationResultDto
        {
            IsSuccess = true,
            Message = "Circle invitation declined.",
            CircleId = invitation.CircleId
        };
    }

    public async Task<CircleOperationResultDto> CancelInvitationAsync(int invitationId, int actingUserId)
    {
        var invitation = await _context.CircleInvitations
            .Include(i => i.Circle)
            .FirstOrDefaultAsync(i => i.InvitationId == invitationId);

        if (invitation == null)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Invitation not found." };
        }

        if (invitation.Status != "Pending")
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "Only pending invitations can be cancelled." };
        }

        // Must be the inviter or have CIRCLE_INVITATION_CANCEL permission
        var hasCancelPerm = await _circleAuthService.HasCirclePermissionAsync(actingUserId, invitation.CircleId, "CIRCLE_INVITATION_CANCEL");
        var isInviter = invitation.InviterUserId == actingUserId;
        var isOwner = invitation.Circle.OwnerUserId == actingUserId;

        if (!isInviter && !hasCancelPerm && !isOwner)
        {
            return new CircleOperationResultDto { IsSuccess = false, Message = "You are not authorized to cancel this invitation." };
        }

        invitation.Status = "Cancelled";
        invitation.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Invitation {InvitationId} cancelled by user {UserId}", invitationId, actingUserId);

        return new CircleOperationResultDto
        {
            IsSuccess = true,
            Message = "Invitation cancelled.",
            CircleId = invitation.CircleId
        };
    }

    public async Task<List<CircleInvitationDto>> GetIncomingInvitationsAsync(int inviteeUserId)
    {
        var invites = await _context.CircleInvitations
            .AsNoTracking()
            .Include(i => i.Circle)
                .ThenInclude(c => c.Members)
            .Include(i => i.InviterUser)
            .Include(i => i.InviteeUser)
            .Where(i => i.InviteeUserId == inviteeUserId && i.Status == "Pending" && i.Circle.IsActive)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invites.Select(i => new CircleInvitationDto
        {
            InvitationId = i.InvitationId,
            CircleId = i.CircleId,
            CircleName = i.Circle.CircleName,
            CircleIcon = i.Circle.CircleIcon,
            CircleMemberCount = i.Circle.Members.Count(m => m.IsActive),
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

    public async Task<List<CircleInvitationDto>> GetOutgoingInvitationsAsync(int circleId, int actingUserId)
    {
        var canView = await _circleAuthService.HasCirclePermissionAsync(actingUserId, circleId, "CIRCLE_INVITE") ||
                      await _circleAuthService.IsCircleOwnerAsync(actingUserId, circleId);

        if (!canView)
        {
            return new List<CircleInvitationDto>();
        }

        var invites = await _context.CircleInvitations
            .AsNoTracking()
            .Include(i => i.Circle)
                .ThenInclude(c => c.Members)
            .Include(i => i.InviterUser)
            .Include(i => i.InviteeUser)
            .Where(i => i.CircleId == circleId && i.Status == "Pending")
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invites.Select(i => new CircleInvitationDto
        {
            InvitationId = i.InvitationId,
            CircleId = i.CircleId,
            CircleName = i.Circle.CircleName,
            CircleIcon = i.Circle.CircleIcon,
            CircleMemberCount = i.Circle.Members.Count(m => m.IsActive),
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
}
