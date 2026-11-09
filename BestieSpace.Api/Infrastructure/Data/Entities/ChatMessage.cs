using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing real-time multimedia and circle group chat messages.
/// Upgraded in Phase 7 to support photos, thumbnails, stickers, voice messages, and media files.
/// </summary>
public partial class ChatMessage
{
    public int Id { get; set; }

    /// <summary>
    /// Alias for ChatMessageId matching specifications.
    /// </summary>
    public int ChatMessageId => Id;

    /// <summary>
    /// Optional CircleId. NULL for legacy 1-on-1 private clubhouse chat messages; set for circle group chat.
    /// </summary>
    public int? CircleId { get; set; }

    /// <summary>
    /// Optional foreign key to Users.
    /// </summary>
    public int? SenderId { get; set; }

    public string SenderName { get; set; } = null!;

    public string MessageType { get; set; } = "text"; // "text", "image", "sticker", "voice", "system"

    public string? Content { get; set; }

    public string? MediaUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public double? VoiceDurationSeconds { get; set; }

    public double? DurationSeconds
    {
        get => VoiceDurationSeconds;
        set => VoiceDurationSeconds = value;
    }

    public int? MediaId { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Alias for CreatedAt matching specifications.
    /// </summary>
    public DateTime CreatedAt
    {
        get => Timestamp;
        set => Timestamp = value;
    }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    // Navigation properties
    public virtual BestieCircle? Circle { get; set; }
    public virtual User? Sender { get; set; }
    public virtual MediaFile? MediaFile { get; set; }
    public virtual ICollection<ChatMessageRead> ReadReceipts { get; set; } = new List<ChatMessageRead>();
    public virtual ICollection<ChatMessageDelivery> DeliveryReceipts { get; set; } = new List<ChatMessageDelivery>();
}
