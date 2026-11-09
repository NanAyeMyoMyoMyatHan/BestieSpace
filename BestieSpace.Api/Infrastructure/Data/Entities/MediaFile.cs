using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// Entity representing uploaded media files (photos, voice notes) in private circles.
/// Stores storage references and metadata outside wwwroot.
/// </summary>
public class MediaFile
{
    public int MediaId { get; set; }
    public int CircleId { get; set; }
    public int UploadedByUserId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? ThumbnailStorageKey { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image"; // "image", "voice"
    public long FileSize { get; set; }
    public double? DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public virtual BestieCircle Circle { get; set; } = null!;
    public virtual User UploadedByUser { get; set; } = null!;
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
