using System;
using System.Collections.Generic;

namespace BestieSpace.Shared.DTOs;

/// <summary>
/// Summary representation of a Bestie Circle.
/// Maximum 9 members including owner.
/// </summary>
public class CircleDto
{
    public int CircleId { get; set; }
    public string CircleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CircleIcon { get; set; } = "🌸";
    public string? CircleImage { get; set; }
    public int OwnerUserId { get; set; }
    public string OwnerUsername { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string? OwnerProfilePicture { get; set; }
    public string OwnerAvatarSticker { get; set; } = "👑";
    public int MemberCount { get; set; }
    public int MaxMembers => 9;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string MyRole { get; set; } = "CircleMember";
    public string MyRoleName => MyRole;
    public bool IsOwner { get; set; }
    public bool CanInvite { get; set; }
    public bool CanManageSettings { get; set; }
}

/// <summary>
/// Detailed circle view with member roster and pending invitations.
/// </summary>
public class CircleDetailDto : CircleDto
{
    public List<CircleMemberDto> Members { get; set; } = new();
    public List<CircleInvitationDto> PendingInvitations { get; set; } = new();
}

/// <summary>
/// Request to create a new Bestie Circle.
/// </summary>
public class CreateCircleRequestDto
{
    public string CircleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CircleIcon { get; set; } = "🌸";
    public string? CircleImage { get; set; }
}

/// <summary>
/// Request to update circle settings.
/// </summary>
public class UpdateCircleRequestDto
{
    public string CircleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CircleIcon { get; set; } = "🌸";
    public string? CircleImage { get; set; }
}

/// <summary>
/// Represents a member within a Bestie Circle.
/// </summary>
public class CircleMemberDto
{
    public int CircleMemberId { get; set; }
    public int CircleId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserDisplayName => DisplayName;
    public string? ProfilePicture { get; set; }
    public string AvatarSticker { get; set; } = "🌸";
    public string UserAvatarSticker => AvatarSticker;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "CircleMember"; // "CircleOwner", "CircleAdmin", "CircleMember"
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOwner { get; set; }
    public bool IsOnline { get; set; }
}

/// <summary>
/// Request to change a member's role within a circle.
/// </summary>
public class ChangeMemberRoleRequestDto
{
    public int TargetUserId { get; set; }
    public string NewRoleName { get; set; } = "CircleMember"; // "CircleAdmin" or "CircleMember"
}

/// <summary>
/// Request to transfer sole circle ownership to another member.
/// </summary>
public class TransferCircleOwnershipRequestDto
{
    public int NewOwnerUserId { get; set; }
}

/// <summary>
/// Circle invitation representation.
/// </summary>
public class CircleInvitationDto
{
    public int InvitationId { get; set; }
    public int CircleId { get; set; }
    public string CircleName { get; set; } = string.Empty;
    public string? CircleDescription { get; set; }
    public string CircleIcon { get; set; } = "🌸";
    public int CircleMemberCount { get; set; }
    
    // Inviter
    public int InviterUserId { get; set; }
    public string InviterUsername { get; set; } = string.Empty;
    public string InviterDisplayName { get; set; } = string.Empty;
    public string InvitedByDisplayName => InviterDisplayName;
    public string? InviterProfilePicture { get; set; }
    public string InviterAvatarSticker { get; set; } = "🌸";

    // Invitee
    public int InviteeUserId { get; set; }
    public string InviteeUsername { get; set; } = string.Empty;
    public string InviteeDisplayName { get; set; } = string.Empty;
    public string? InviteeProfilePicture { get; set; }
    public string InviteeAvatarSticker { get; set; } = "🌸";

    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

/// <summary>
/// Request to invite a friend to a circle.
/// </summary>
public class SendCircleInvitationRequestDto
{
    public int CircleId { get; set; }
    public int InviteeUserId { get; set; }
}

/// <summary>
/// Standard operation response for circle actions.
/// </summary>
public class CircleOperationResultDto
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? CircleId { get; set; }
    public int? InvitationId { get; set; }
}
