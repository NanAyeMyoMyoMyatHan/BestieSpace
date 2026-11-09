using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BestieSpace.Api.Infrastructure.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BestieSpace.Api.Features.Auth;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
    (string rawToken, string tokenHash, DateTime expiresAt) GenerateRefreshToken();
    string HashToken(string token);
    ClaimsPrincipal? ValidateAccessToken(string token);
    DateTime GetAccessTokenExpiration();
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;
    private readonly int _refreshTokenDays;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
        _secretKey = _config["Jwt:Key"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? "BestieSpaceSuperSecretKawaiiKeyForJwtTokenAuth2026_MustBeAtLeast32BytesLong!";
        _issuer = _config["Jwt:Issuer"] ?? "BestieSpace";
        _audience = _config["Jwt:Audience"] ?? "BestieSpaceClient";

        if (!int.TryParse(_config["Jwt:AccessTokenExpirationMinutes"], out _accessTokenMinutes) || _accessTokenMinutes <= 0)
        {
            _accessTokenMinutes = 60; // 1 hour access token
        }

        if (!int.TryParse(_config["Jwt:RefreshTokenExpirationDays"], out _refreshTokenDays) || _refreshTokenDays <= 0)
        {
            _refreshTokenDays = 7; // 7 days refresh token
        }
    }

    public DateTime GetAccessTokenExpiration() => DateTime.UtcNow.AddMinutes(_accessTokenMinutes);

    public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("displayName", user.DisplayName),
            new("avatarSticker", user.AvatarSticker),
            new("roleKey", user.RoleKey),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        foreach (var perm in permissions)
        {
            if (!string.IsNullOrWhiteSpace(perm))
            {
                claims.Add(new Claim("permission", perm));
            }
        }

        var expires = GetAccessTokenExpiration();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public (string rawToken, string tokenHash, DateTime expiresAt) GenerateRefreshToken()
    {
        // 64 cryptographically secure random bytes
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var tokenHash = HashToken(rawToken);
        var expiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays);

        return (rawToken, tokenHash, expiresAt);
    }

    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentNullException(nameof(token));

        byte[] inputBytes = Encoding.UTF8.GetBytes(token.Trim());
        byte[] hashBytes = SHA256.HashData(inputBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            if (validatedToken is JwtSecurityToken jwtToken &&
                jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return principal;
            }
        }
        catch
        {
            // Invalid token
        }

        return null;
    }
}
