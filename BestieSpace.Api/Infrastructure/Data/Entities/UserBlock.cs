using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents a user blocking another user from interaction.
/// </summary>
public class UserBlock
{
    public int UserBlockId { get; set; }

    public int BlockerUserId { get; set; }

    public int BlockedUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User BlockerUser { get; set; } = null!;
    public virtual User BlockedUser { get; set; } = null!;
}
