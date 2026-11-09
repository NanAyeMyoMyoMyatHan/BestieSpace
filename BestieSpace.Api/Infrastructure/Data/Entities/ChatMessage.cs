using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class ChatMessage
{
    public int Id { get; set; }

    public int? CircleId { get; set; }

    public int? SenderId { get; set; }

    public string SenderName { get; set; } = null!;

    public string MessageType { get; set; } = null!;

    public string? Content { get; set; }

    public string? MediaUrl { get; set; }

    public double? VoiceDurationSeconds { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public int? MediaId { get; set; }

    public DateTime Timestamp { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual ICollection<ChatMessageDelivery> ChatMessageDeliveries { get; set; } = new List<ChatMessageDelivery>();

    public virtual ICollection<ChatMessageRead> ChatMessageReads { get; set; } = new List<ChatMessageRead>();

    public virtual BestieCircle? Circle { get; set; }

    public virtual User? Sender { get; set; }
}
