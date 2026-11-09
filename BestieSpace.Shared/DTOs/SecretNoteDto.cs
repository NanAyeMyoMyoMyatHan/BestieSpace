namespace BestieSpace.Shared.DTOs;

public class SecretNoteDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PreviewHint { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string StickerIcon { get; set; } = "💌";
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
}

public class CreateSecretNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string PreviewHint { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string StickerIcon { get; set; } = "💌";
}

public class UpdateSecretNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string PreviewHint { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string StickerIcon { get; set; } = "💌";
    public bool? IsUnlocked { get; set; }
}
