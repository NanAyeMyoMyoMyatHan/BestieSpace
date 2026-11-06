using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing real-time multimedia chat messages (text, sticker, photo, voice).
/// Scaffolded to match ChatMessages SQL table.
/// </summary>
public partial class ChatMessage
{
    public int Id { get; set; }

    public string SenderName { get; set; } = null!;

    public string MessageType { get; set; } = null!; // "text", "sticker", "photo", "voice"

    public string? Content { get; set; }

    public string? MediaUrl { get; set; }

    public double? VoiceDurationSeconds { get; set; }

    public DateTime Timestamp { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
}
