namespace BestieSpace.Api.Infrastructure.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty; // "Bestie" or "Me"
    public string AvatarSticker { get; set; } = string.Empty; // "🌸" or "🎀"
    public string? PasswordHint { get; set; }
    public string QuickPin { get; set; } = "3000";
    public bool IsOnline { get; set; } = false;
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
