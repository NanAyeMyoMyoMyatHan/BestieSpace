using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents a friendship connection or request between two users.
/// </summary>
public class Friendship
{
    public int FriendshipId { get; set; }

    public int UserId { get; set; }

    public int FriendUserId { get; set; }

    /// <summary>
    /// Friendship status: "Pending", "Accepted", "Rejected"
    /// </summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Computed canonical user pairing columns for database-level uniqueness regardless of direction
    public int UserA { get; private set; }
    public int UserB { get; private set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual User FriendUser { get; set; } = null!;
}
