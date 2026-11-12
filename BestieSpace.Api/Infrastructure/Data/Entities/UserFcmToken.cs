using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

public partial class UserFcmToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public string DeviceType { get; set; } = "Web";

    public string? DeviceModel { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
