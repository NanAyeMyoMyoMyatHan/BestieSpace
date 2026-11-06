using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing an interactive mystery envelope note.
/// Scaffolded to match SecretNotes SQL table.
/// </summary>
public partial class SecretNote
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string PreviewHint { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string StickerIcon { get; set; } = null!;

    public bool IsUnlocked { get; set; }

    public DateTime? UnlockedAt { get; set; }
}
