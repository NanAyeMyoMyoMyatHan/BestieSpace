using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class MediaFile
{
    public int MediaId { get; set; }

    public int CircleId { get; set; }

    public int UploadedByUserId { get; set; }

    public string StorageKey { get; set; } = null!;

    public string? ThumbnailStorageKey { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public string MediaType { get; set; } = null!;

    public long FileSize { get; set; }

    public double? DurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual BestieCircle Circle { get; set; } = null!;

    public virtual User UploadedByUser { get; set; } = null!;
}
