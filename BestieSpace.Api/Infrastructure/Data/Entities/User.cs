using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// User entity for BestieSpace platform.
/// Upgraded in Phase 1 for multi-user social friendship and circle foundation.
/// </summary>
public class User
{
    // Primary Key (UserId)
    public int UserId { get; set; }

    /// <summary>
    /// Backward-compatibility alias for UserId so existing controllers, services, and DTO mappings compile seamlessly.
    /// Ignored in EF Core mapping.
    /// </summary>
    public int Id
    {
        get => UserId;
        set => UserId = value;
    }

    public string Username { get; set; } = string.Empty;

    public string NormalizedUsername { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Preserved fields from initial Kawaii clubhouse implementation
    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string RoleKey { get; set; } = string.Empty; // "Bestie", "Me", "Admin", etc.

    public string AvatarSticker { get; set; } = "🌸";

    public string? PasswordHint { get; set; }

    public string QuickPin { get; set; } = "3000";

    public bool IsOnline { get; set; } = false;

    public DateTime? LastSeenAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // Navigation properties for relationships
    public virtual ICollection<Friendship> InitiatedFriendships { get; set; } = new List<Friendship>();
    public virtual ICollection<Friendship> ReceivedFriendships { get; set; } = new List<Friendship>();

    public virtual ICollection<UserBlock> BlockedUsers { get; set; } = new List<UserBlock>();
    public virtual ICollection<UserBlock> BlockedByUsers { get; set; } = new List<UserBlock>();

    public virtual ICollection<BestieCircle> OwnedCircles { get; set; } = new List<BestieCircle>();
    public virtual ICollection<BestieCircleMember> CircleMemberships { get; set; } = new List<BestieCircleMember>();

    public virtual ICollection<CircleInvitation> SentInvitations { get; set; } = new List<CircleInvitation>();
    public virtual ICollection<CircleInvitation> ReceivedInvitations { get; set; } = new List<CircleInvitation>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<ChatMessageRead> MessageReads { get; set; } = new List<ChatMessageRead>();
    public virtual ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
