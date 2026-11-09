using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing real-time multimedia chat messages (text, sticker, photo, voice).
/// Upgraded in Phase 1 to support Bestie Circles and multi-user chat while preserving 1-on-1 private clubhouse messages.
/// </summary>
public partial class ChatMessage
{
    public int Id { get; set; }

    /// <summary>
    /// Optional CircleId. NULL for legacy 1-on-1 private clubhouse chat messages; set for circle group chat.
    /// </summary>
    public int? CircleId { get; set; }

    /// <summary>
    /// Optional foreign key to Users.
    /// </summary>
    public int? SenderId { get; set; }

    public string SenderName { get; set; } = null!;

    public string MessageType { get; set; } = null!; // "text", "sticker", "photo", "voice"

    public string? Content { get; set; }

    public string? MediaUrl { get; set; }

    public double? VoiceDurationSeconds { get; set; }

    public DateTime Timestamp { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    // Navigation properties
    public virtual BestieCircle? Circle { get; set; }
    public virtual User? Sender { get; set; }
    public virtual ICollection<ChatMessageRead> ReadReceipts { get; set; } = new List<ChatMessageRead>();
}
