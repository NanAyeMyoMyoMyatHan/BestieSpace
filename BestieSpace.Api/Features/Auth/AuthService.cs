using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task<AuthResponseDto> RevokeTokenAsync(RevokeTokenRequestDto? request, int? userId);
    Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request);
    Task<PasswordHintDto> GetHintAsync(string? username);
    Task<UserSessionDto?> GetCurrentUserSessionAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly BestieDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        BestieDbContext context,
        IPasswordHasherService passwordHasher,
        IJwtTokenService jwtTokenService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return Fail("Please choose a cute nickname / username 🌸");
        }

        var cleanUsername = request.Username.Trim();
        if (cleanUsername.Length < 3 || cleanUsername.Length > 30)
        {
            return Fail("Username must be between 3 and 30 characters long 🎀");
        }

        if (!Regex.IsMatch(cleanUsername, @"^[a-zA-Z0-9_\-\.]+$"))
        {
            return Fail("Username can only contain letters, numbers, underscores, and hyphens ✨");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Fail("Please enter a valid email address 💌");
        }

        var cleanEmail = request.Email.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(cleanEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return Fail("That email format doesn't look quite right 🥺 Please check and try again!");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Fail("Please enter a password 🔒");
        }

        if (request.Password.Length < 6)
        {
            return Fail("Password must be at least 6 characters long for security 🛡️");
        }

        if (!string.IsNullOrEmpty(request.ConfirmPassword) && request.Password != request.ConfirmPassword)
        {
            return Fail("Passwords do not match! Please check both fields 🥺");
        }

        var normalizedUsername = cleanUsername.ToUpperInvariant();
        var normalizedEmail = cleanEmail.ToUpperInvariant();

        // 2. Check for unique username
        var existingUser = await _context.Users
            .AnyAsync(u => u.NormalizedUsername == normalizedUsername);
        if (existingUser)
        {
            return Fail("Oh no! That username is already taken by another bestie 🌸 Please choose another!");
        }

        // 3. Check for unique email
        var existingEmail = await _context.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail);
        if (existingEmail)
        {
            return Fail("An account with that email already exists 💌 Try logging in instead!");
        }

        // 4. Secure Password Hashing (Never store plaintext)
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? cleanUsername
            : request.DisplayName.Trim();

        var avatarSticker = string.IsNullOrWhiteSpace(request.AvatarSticker)
            ? "🌸"
            : request.AvatarSticker.Trim();

        var newUser = new User
        {
            Username = cleanUsername,
            NormalizedUsername = normalizedUsername,
            Email = cleanEmail,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            Password = string.Empty, // Plaintext is never stored
            DisplayName = displayName,
            RoleKey = "User",
            AvatarSticker = avatarSticker,
            PasswordHint = "Your secret personal password 🌸",
            QuickPin = "3000",
            IsOnline = true,
            LastSeenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // 5. Assign default 'User' role in UserRoles table
        var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
        if (defaultRole == null)
        {
            defaultRole = new Role { RoleName = "User", Description = "Default registered platform user" };
            _context.Roles.Add(defaultRole);
            await _context.SaveChangesAsync();
        }

        _context.UserRoles.Add(new UserRole
        {
            UserId = newUser.UserId,
            RoleId = defaultRole.RoleId
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("New user registered successfully: '{Username}' (UserId: {UserId})", newUser.Username, newUser.UserId);

        // 6. Generate JWT and Refresh Token
        var roles = new List<string> { "User" };
        var permissions = await LoadPermissionsForRolesAsync(roles);

        var accessToken = _jwtTokenService.GenerateAccessToken(newUser, roles, permissions);
        var (rawRefreshToken, tokenHash, refreshExpires) = _jwtTokenService.GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = newUser.UserId,
            TokenHash = tokenHash,
            ExpiresAt = refreshExpires,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var userSession = CreateUserSession(newUser, roles, permissions, accessToken, rawRefreshToken);

        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = $"Welcome to BestieSpace, {newUser.DisplayName}! 🌸✨ Your account was created successfully!",
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = _jwtTokenService.GetAccessTokenExpiration(),
            User = userSession,
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Fail("Oopsie! Please enter your nickname/email and password 🥺");
        }

        var cleanInput = request.Username.Trim();
        var cleanUpper = cleanInput.ToUpperInvariant();

        // Support login by either Username OR Email
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedUsername == cleanUpper || u.NormalizedEmail == cleanUpper);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Failed login attempt for identifier: '{Identifier}'", cleanInput);
            return Fail("Oopsie! Invalid username/email or password 🥺 Try again!");
        }

        // Validate password using secure hasher
        bool isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);

        // Fallback backward-compatibility for legacy unhashed passwords
        if (!isPasswordValid && !string.IsNullOrEmpty(user.Password) && user.Password == request.Password.Trim())
        {
            isPasswordValid = true;
        }

        if (!isPasswordValid)
        {
            _logger.LogWarning("Invalid password for user: '{Username}'", user.Username);
            return Fail("Oopsie! Wrong password 🥺 Try again!");
        }

        // Auto-upgrade password hash if needed and wipe plaintext password
        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHasher.HashPassword(request.Password.Trim());
            user.Password = string.Empty; // Zero plaintext password
            user.UpdatedAt = DateTime.UtcNow;
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.IsOnline = true;
        user.LastSeenAt = DateTime.UtcNow;

        // Ensure user has at least 'User' role assigned in UserRoles
        if (!user.UserRoles.Any())
        {
            var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            if (defaultRole != null)
            {
                var newUr = new UserRole { UserId = user.UserId, RoleId = defaultRole.RoleId, Role = defaultRole };
                user.UserRoles.Add(newUr);
                _context.UserRoles.Add(newUr);
            }
        }

        await _context.SaveChangesAsync();

        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).Distinct().ToList();
        if (!roles.Contains("User")) roles.Add("User");
        if (user.RoleKey == "Bestie" && !roles.Contains("Bestie")) roles.Add("Bestie");
        if (user.RoleKey == "Me" && !roles.Contains("Owner")) roles.Add("Owner");

        var permissions = await LoadPermissionsForRolesAsync(roles);

        // Generate JWT and Refresh Token
        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles, permissions);
        var (rawRefreshToken, tokenHash, refreshExpires) = _jwtTokenService.GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = refreshExpires,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successful login for user '{Username}' ({DisplayName})", user.Username, user.DisplayName);

        var userSession = CreateUserSession(user, roles, permissions, accessToken, rawRefreshToken);

        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = $"Welcome back, {user.DisplayName}! 💖",
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = _jwtTokenService.GetAccessTokenExpiration(),
            User = userSession,
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Fail("Refresh token is required.");
        }

        var incomingHash = _jwtTokenService.HashToken(request.RefreshToken);

        var tokenRecord = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash);

        if (tokenRecord == null)
        {
            _logger.LogWarning("Refresh token hash not found.");
            return Fail("Invalid refresh token.");
        }

        // Token reuse detection: if revoked token is used again, revoke all tokens for this user!
        if (tokenRecord.IsRevoked)
        {
            _logger.LogWarning("Revoked refresh token reuse detected for UserId: {UserId}! Revoking all active tokens.", tokenRecord.UserId);
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == tokenRecord.UserId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            return Fail("Invalid refresh token. Please log in again.");
        }

        if (tokenRecord.IsExpired)
        {
            return Fail("Refresh token has expired. Please log in again.");
        }

        var user = tokenRecord.User;
        if (!user.IsActive)
        {
            return Fail("User account is inactive.");
        }

        // Implement Refresh Token Rotation
        var (newRawRefreshToken, newTokenHash, newRefreshExpires) = _jwtTokenService.GenerateRefreshToken();

        // Mark old token revoked and link to the replacement
        tokenRecord.RevokedAt = DateTime.UtcNow;
        tokenRecord.ReplacedByTokenHash = newTokenHash;

        // Save new active refresh token
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = newTokenHash,
            ExpiresAt = newRefreshExpires,
            CreatedAt = DateTime.UtcNow
        });

        // Load roles and permissions
        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).Distinct().ToList();
        if (!roles.Contains("User")) roles.Add("User");
        var permissions = await LoadPermissionsForRolesAsync(roles);

        // Generate new access token
        var newAccessToken = _jwtTokenService.GenerateAccessToken(user, roles, permissions);

        await _context.SaveChangesAsync();

        var userSession = CreateUserSession(user, roles, permissions, newAccessToken, newRawRefreshToken);

        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = "Token refreshed successfully 🌸",
            AccessToken = newAccessToken,
            RefreshToken = newRawRefreshToken,
            ExpiresAt = _jwtTokenService.GetAccessTokenExpiration(),
            User = userSession,
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<AuthResponseDto> RevokeTokenAsync(RevokeTokenRequestDto? request, int? userId)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            var hash = _jwtTokenService.HashToken(request.RefreshToken);
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token != null && token.RevokedAt == null)
            {
                token.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        else if (userId.HasValue)
        {
            // Revoke all active tokens for user (e.g. full logout)
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId.Value && t.RevokedAt == null)
                .ToListAsync();

            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }

        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = "Token successfully revoked."
        };
    }

    public async Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.PinCode))
        {
            return Fail("Please enter your 4-digit strawberry PIN 🍓");
        }

        var cleanPin = request.PinCode.Trim();
        User? user = null;

        if (!string.IsNullOrWhiteSpace(request.RoleKey))
        {
            var cleanRole = request.RoleKey.Trim();
            user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.RoleKey.ToLower() == cleanRole.ToLower() && u.QuickPin == cleanPin);
        }

        user ??= await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.QuickPin == cleanPin);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Failed PIN login attempt with PIN '{Pin}'", cleanPin);
            return Fail("Oopsie! Wrong strawberry PIN 🍓🥺 Try again!");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.IsOnline = true;
        user.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).Distinct().ToList();
        if (!roles.Contains("User")) roles.Add("User");
        if (user.RoleKey == "Bestie" && !roles.Contains("Bestie")) roles.Add("Bestie");
        if (user.RoleKey == "Me" && !roles.Contains("Owner")) roles.Add("Owner");

        var permissions = await LoadPermissionsForRolesAsync(roles);

        // Generate JWT and Refresh Token
        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles, permissions);
        var (rawRefreshToken, tokenHash, refreshExpires) = _jwtTokenService.GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = refreshExpires,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Successful PIN login for user '{Username}' ({RoleKey})", user.Username, user.RoleKey);

        var userSession = CreateUserSession(user, roles, permissions, accessToken, rawRefreshToken);

        return new AuthResponseDto
        {
            IsSuccess = true,
            Message = $"Strawberry PIN accepted! Welcome, {user.DisplayName}! 🍓💖",
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = _jwtTokenService.GetAccessTokenExpiration(),
            User = userSession,
            Roles = roles,
            Permissions = permissions
        };
    }

    public async Task<PasswordHintDto> GetHintAsync(string? username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var clean = username.Trim().ToLowerInvariant();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == clean || (u.Email != null && u.Email.ToLower() == clean));

            if (user != null)
            {
                return new PasswordHintDto
                {
                    Username = user.Username,
                    Hint = user.PasswordHint ?? "Try your secret personal passphrase! 🌸"
                };
            }
        }

        // Default hint
        return new PasswordHintDto
        {
            Username = "strawberry_bestie",
            Hint = "Try: missyou3000! 🌸"
        };
    }

    public async Task<UserSessionDto?> GetCurrentUserSessionAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (user == null) return null;

        var roles = user.UserRoles.Select(ur => ur.Role.RoleName).Distinct().ToList();
        if (!roles.Contains("User")) roles.Add("User");
        var permissions = await LoadPermissionsForRolesAsync(roles);

        return CreateUserSession(user, roles, permissions, null, null);
    }

    private async Task<List<string>> LoadPermissionsForRolesAsync(IEnumerable<string> roles)
    {
        var roleNames = roles.ToList();
        var permissions = await _context.RolePermissions
            .Where(rp => roleNames.Contains(rp.Role.RoleName))
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToListAsync();

        return permissions;
    }

    private static UserSessionDto CreateUserSession(User user, List<string> roles, List<string> permissions, string? accessToken, string? refreshToken)
    {
        return new UserSessionDto
        {
            UserId = user.UserId,
            Id = user.UserId,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            RoleKey = user.RoleKey,
            AvatarSticker = user.AvatarSticker,
            QuickPin = user.QuickPin,
            Roles = roles,
            Permissions = permissions,
            Token = accessToken,
            RefreshToken = refreshToken
        };
    }

    private static AuthResponseDto Fail(string message) => new()
    {
        IsSuccess = false,
        Message = message
    };
}
