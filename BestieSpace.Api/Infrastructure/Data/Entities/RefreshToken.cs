using System;

namespace BestieSpace.Api.Infrastructure.Data.Entities;

/// <summary>
/// RefreshToken entity for BestieSpace Phase 2 authentication.
/// Stores only cryptographically hashed refresh tokens in SQL Server with rotation and revocation tracking.
/// </summary>
public class RefreshToken
{
    public int RefreshTokenId { get; set; }

    public int UserId { get; set; }

    /// <summary>
    /// Cryptographic SHA-256 hash of the refresh token. Never stores plaintext tokens.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Tracks refresh token rotation by storing the hash of the token that replaced this one.
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt != null;

    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
