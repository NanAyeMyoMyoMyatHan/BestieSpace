namespace BestieSpace.Shared.DTOs;

public class ChatMessageDto
{
    public int Id { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text"; // "text", "sticker", "photo", "voice"
    public string? Content { get; set; }
    public string? MediaUrl { get; set; }
    public double? VoiceDurationSeconds { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class SendTextOrStickerRequest
{
    public string SenderName { get; set; } = "Me 🎀";
    public string MessageType { get; set; } = "text"; // "text" or "sticker"
    public string Content { get; set; } = string.Empty;
}

public class MarkMessagesReadRequest
{
    public List<int> MessageIds { get; set; } = new();
    public string ReaderName { get; set; } = string.Empty;
}

public class MessagesReadNotification
{
    public List<int> MessageIds { get; set; } = new();
    public string ReaderName { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
}
