using System;
using System.Collections.Generic;

namespace BestieSpace.Shared.DTOs;

public class RegisterRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ConfirmPassword { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarSticker { get; set; } = "🌸";
    public string QuickPin { get; set; } = "3000";
    public string? Pin
    {
        get => QuickPin;
        set => QuickPin = value ?? "3000";
    }
}

public class LoginRequestDto
{
    /// <summary>
    /// Accepts either the user's username or email address.
    /// </summary>
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RevokeTokenRequestDto
{
    public string? RefreshToken { get; set; }
}

public class UserSessionDto
{
    public int UserId { get; set; }

    /// <summary>
    /// Backward-compatibility alias for UserId.
    /// </summary>
    public int Id
    {
        get => UserId;
        set => UserId = value;
    }

    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = "User"; // "Bestie", "Me", "Admin", etc.
    public string AvatarSticker { get; set; } = "🌸";
    public string QuickPin { get; set; } = "3000";

    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();

    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
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
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserSessionDto? User { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public class PasswordHintDto
{
    public string Username { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
}
