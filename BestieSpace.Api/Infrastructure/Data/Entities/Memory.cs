using System;
using System.Collections.Generic;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class Memory
{
    public int Id { get; set; }

    public string Caption { get; set; } = null!;

    public string ImageUrl { get; set; } = null!;

    public DateTime MemoryDate { get; set; }

    public string StickerType { get; set; } = null!;

    public double RotationAngle { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}
