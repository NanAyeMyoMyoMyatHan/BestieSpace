using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string NormalizedUsername { get; set; } = null!;

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    public string Password { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? ProfilePicture { get; set; }

    public string? Bio { get; set; }

    public string RoleKey { get; set; } = null!;

    public string AvatarSticker { get; set; } = null!;

    public string? PasswordHint { get; set; }

    public string QuickPin { get; set; } = null!;

    public bool IsOnline { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public virtual ICollection<BestieCircleMember> BestieCircleMembers { get; set; } = new List<BestieCircleMember>();

    public virtual ICollection<BestieCircle> BestieCircles { get; set; } = new List<BestieCircle>();

    public virtual ICollection<ChatMessageDelivery> ChatMessageDeliveries { get; set; } = new List<ChatMessageDelivery>();

    public virtual ICollection<ChatMessageRead> ChatMessageReads { get; set; } = new List<ChatMessageRead>();

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<CircleInvitation> CircleInvitationInviteeUsers { get; set; } = new List<CircleInvitation>();

    public virtual ICollection<CircleInvitation> CircleInvitationInviterUsers { get; set; } = new List<CircleInvitation>();

    public virtual ICollection<Friendship> FriendshipFriendUsers { get; set; } = new List<Friendship>();

    public virtual ICollection<Friendship> FriendshipUsers { get; set; } = new List<Friendship>();

    public virtual ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<UserFcmToken> UserFcmTokens { get; set; } = new List<UserFcmToken>();

    public virtual ICollection<UserBlock> UserBlockBlockedUsers { get; set; } = new List<UserBlock>();

    public virtual ICollection<UserBlock> UserBlockBlockerUsers { get; set; } = new List<UserBlock>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
