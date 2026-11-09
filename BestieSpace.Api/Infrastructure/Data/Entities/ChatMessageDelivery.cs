using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Represents a delivery receipt for a chat message to a specific recipient user.
/// Distinguishes sent (stored on server) from delivered (acknowledged by client) and read.
/// </summary>
public class ChatMessageDelivery
{
    public int ChatMessageDeliveryId { get; set; }

    public int ChatMessageId { get; set; }

    public int UserId { get; set; }

    public DateTime DeliveredAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ChatMessage ChatMessage { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
