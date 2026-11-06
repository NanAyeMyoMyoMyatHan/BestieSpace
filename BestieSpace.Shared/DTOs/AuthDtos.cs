namespace BestieSpace.Shared.DTOs;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UserSessionDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty; // "Bestie" or "Me"
    public string AvatarSticker { get; set; } = string.Empty; // "🌸" or "🎀"
    public string QuickPin { get; set; } = string.Empty; // "3000" or "4040"
}

public class PinLoginRequestDto
{
    public string PinCode { get; set; } = string.Empty;
    public string? RoleKey { get; set; }
}

public class UserPresenceDto
{
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

public class AuthResponseDto
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserSessionDto? User { get; set; }
}

public class PasswordHintDto
{
    public string Username { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
}
