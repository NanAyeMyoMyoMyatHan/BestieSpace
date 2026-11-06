using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request);
    Task<PasswordHintDto> GetHintAsync(string? username);
}

public class AuthService : IAuthService
{
    private readonly BestieDbContext _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(BestieDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Oopsie! Please enter both your nickname and secret code 🥺"
            };
        }

        var cleanUsername = request.Username.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername);

        if (user == null || user.Password != request.Password.Trim())
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            return new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Oopsie! Wrong secret code 🥺 Try again!"
            };
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successful login for user '{Username}' ({DisplayName})", user.Username, user.DisplayName);

        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = $"Welcome back, {user.DisplayName}! 💖",
            User = new UserSessionDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                RoleKey = user.RoleKey,
                AvatarSticker = user.AvatarSticker,
                QuickPin = user.QuickPin
            }
        };
    }

    public async Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.PinCode))
        {
            return new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Please enter your 4-digit strawberry PIN 🍓"
            };
        }

        var cleanPin = request.PinCode.Trim();
        User? user = null;

        if (!string.IsNullOrWhiteSpace(request.RoleKey))
        {
            var cleanRole = request.RoleKey.Trim();
            user = await _context.Users
                .FirstOrDefaultAsync(u => u.RoleKey.ToLower() == cleanRole.ToLower() && u.QuickPin == cleanPin);
        }

        user ??= await _context.Users
            .FirstOrDefaultAsync(u => u.QuickPin == cleanPin);

        if (user == null)
        {
            _logger.LogWarning("Failed PIN login attempt with PIN '{Pin}'", cleanPin);
            return new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Oopsie! Wrong strawberry PIN 🍓🥺 Try again!"
            };
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.IsOnline = true;
        user.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successful PIN login for user '{Username}' ({RoleKey})", user.Username, user.RoleKey);

        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = $"Strawberry PIN accepted! Welcome, {user.DisplayName}! 🍓💖",
            User = new UserSessionDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                RoleKey = user.RoleKey,
                AvatarSticker = user.AvatarSticker,
                QuickPin = user.QuickPin
            }
        };
    }

    public async Task<PasswordHintDto> GetHintAsync(string? username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var clean = username.Trim().ToLowerInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == clean);

            if (user != null)
            {
                return new PasswordHintDto
                {
                    Username = user.Username,
                    Hint = user.PasswordHint ?? "Try our secret phrase! 🌸"
                };
            }
        }

        // Default hint for Bestie
        var bestie = await _context.Users.FirstOrDefaultAsync(u => u.RoleKey == "Bestie");
        return new PasswordHintDto
        {
            Username = bestie?.Username ?? "strawberry_bestie",
            Hint = bestie?.PasswordHint ?? "How much I miss you (missyou3000!)"
        };
    }
}
