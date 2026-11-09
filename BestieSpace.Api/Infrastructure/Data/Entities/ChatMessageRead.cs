using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents a read receipt for a chat message by a specific user.
/// </summary>
public class ChatMessageRead
{
    public int ChatMessageReadId { get; set; }

    public int ChatMessageId { get; set; }

    public int UserId { get; set; }

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ChatMessage ChatMessage { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
