using BestieSpace.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for BestieSpace platform using Microsoft SQL Server.
/// Configures complete multi-user social friendships, circles, RBAC, and chat foundations,
/// while preserving existing Kawaii memory box, secret notes, app stats, and chat history.
/// </summary>
public partial class BestieDbContext : DbContext
{
    public BestieDbContext()
    {
    }

    public BestieDbContext(DbContextOptions<BestieDbContext> options)
        : base(options)
    {
    }

    // Existing entities (preserved)
    public virtual DbSet<AppStat> AppStats { get; set; } = null!;
    public virtual DbSet<Memory> Memories { get; set; } = null!;
    public virtual DbSet<SecretNote> SecretNotes { get; set; } = null!;
    public virtual DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;

    // New social, circle, and RBAC entities (Phase 1)
    public virtual DbSet<Friendship> Friendships { get; set; } = null!;
    public virtual DbSet<UserBlock> UserBlocks { get; set; } = null!;
    public virtual DbSet<BestieCircle> BestieCircles { get; set; } = null!;
    public virtual DbSet<BestieCircleMember> BestieCircleMembers { get; set; } = null!;
    public virtual DbSet<CircleInvitation> CircleInvitations { get; set; } = null!;
    public virtual DbSet<Role> Roles { get; set; } = null!;
    public virtual DbSet<Permission> Permissions { get; set; } = null!;
    public virtual DbSet<UserRole> UserRoles { get; set; } = null!;
    public virtual DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public virtual DbSet<ChatMessageRead> ChatMessageReads { get; set; } = null!;
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. AppStats (Preserved)
        modelBuilder.Entity<AppStat>(entity =>
        {
            entity.ToTable("AppStats");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.MissYouCount).HasDefaultValue(1000);
            entity.Property(e => e.MainLetterTitle).IsRequired().HasMaxLength(250);
            entity.Property(e => e.MainLetterContent).IsRequired();
            entity.Property(e => e.SenderNickname).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RecipientNickname).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastUpdated).IsRequired();
        });

        // 2. Memories (Preserved)
        modelBuilder.Entity<Memory>(entity =>
        {
            entity.ToTable("Memories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Caption).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.MemoryDate).IsRequired();
            entity.Property(e => e.StickerType).IsRequired().HasMaxLength(50).HasDefaultValue("heart");
            entity.Property(e => e.RotationAngle).HasDefaultValue(0.0);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        // 3. SecretNotes (Preserved)
        modelBuilder.Entity<SecretNote>(entity =>
        {
            entity.ToTable("SecretNotes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PreviewHint).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.StickerIcon).IsRequired().HasMaxLength(50).HasDefaultValue("💌");
            entity.Property(e => e.IsUnlocked).HasDefaultValue(false);
            entity.Property(e => e.UnlockedAt).IsRequired(false);
        });

        // 4. Users (Upgraded with UserId PK while preserving existing auth fields)
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedOnAdd();

            // Ignore the backward-compatibility 'Id' alias in EF Core mapping
            entity.Ignore(e => e.Id);

            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.NormalizedUsername).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.NormalizedUsername).IsUnique();

            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(255);
            entity.HasIndex(e => e.NormalizedEmail);

            entity.Property(e => e.PasswordHash).HasMaxLength(255);

            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Existing Kawaii auth fields (preserved)
            entity.Property(e => e.Password).IsRequired().HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.RoleKey).IsRequired().HasMaxLength(50).HasDefaultValue("User");
            entity.Property(e => e.AvatarSticker).IsRequired().HasMaxLength(50).HasDefaultValue("🌸");
            entity.Property(e => e.PasswordHint).HasMaxLength(255);
            entity.Property(e => e.QuickPin).IsRequired().HasMaxLength(10).HasDefaultValue("3000");
            entity.Property(e => e.IsOnline).HasDefaultValue(false);
            entity.Property(e => e.LastSeenAt);
            entity.Property(e => e.LastLoginAt);
        });

        // 5. Friendships
        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.ToTable("Friendships", t =>
            {
                t.HasCheckConstraint("CK_Friendships_NoSelfFriendship", "[UserId] <> [FriendUserId]");
                t.HasCheckConstraint("CK_Friendships_ValidStatus", "[Status] IN ('Pending', 'Accepted', 'Rejected')");
            });

            entity.HasKey(e => e.FriendshipId);
            entity.Property(e => e.FriendshipId).ValueGeneratedOnAdd();

            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);

            // Computed canonical pairing to guarantee friendship pair is stored consistently and prevent duplicates in SQL Server
            entity.Property(e => e.UserA)
                .HasComputedColumnSql("CASE WHEN [UserId] < [FriendUserId] THEN [UserId] ELSE [FriendUserId] END", stored: true);
            entity.Property(e => e.UserB)
                .HasComputedColumnSql("CASE WHEN [UserId] < [FriendUserId] THEN [FriendUserId] ELSE [UserId] END", stored: true);

            entity.HasIndex(e => new { e.UserA, e.UserB }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.FriendUserId, e.Status });

            entity.HasOne(e => e.User)
                .WithMany(u => u.InitiatedFriendships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FriendUser)
                .WithMany(u => u.ReceivedFriendships)
                .HasForeignKey(e => e.FriendUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. UserBlocks
        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.ToTable("UserBlocks", t =>
            {
                t.HasCheckConstraint("CK_UserBlocks_NoSelfBlock", "[BlockerUserId] <> [BlockedUserId]");
            });

            entity.HasKey(e => e.UserBlockId);
            entity.Property(e => e.UserBlockId).ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => new { e.BlockerUserId, e.BlockedUserId }).IsUnique();

            entity.HasOne(e => e.BlockerUser)
                .WithMany(u => u.BlockedUsers)
                .HasForeignKey(e => e.BlockerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.BlockedUser)
                .WithMany(u => u.BlockedByUsers)
                .HasForeignKey(e => e.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. BestieCircles (Maximum 9 members including owner, prepared for transactional service-layer enforcement)
        modelBuilder.Entity<BestieCircle>(entity =>
        {
            entity.ToTable("BestieCircles");

            entity.HasKey(e => e.CircleId);
            entity.Property(e => e.CircleId).ValueGeneratedOnAdd();

            entity.Property(e => e.CircleName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => e.OwnerUserId);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Owner)
                .WithMany(u => u.OwnedCircles)
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. BestieCircleMembers
        modelBuilder.Entity<BestieCircleMember>(entity =>
        {
            entity.ToTable("BestieCircleMembers");

            entity.HasKey(e => e.CircleMemberId);
            entity.Property(e => e.CircleMemberId).ValueGeneratedOnAdd();
            entity.Property(e => e.JoinedAt).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // A user cannot join the same circle twice
            entity.HasIndex(e => new { e.CircleId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.CircleId, e.IsActive });
            entity.HasIndex(e => new { e.UserId, e.IsActive });

            entity.HasOne(e => e.Circle)
                .WithMany(c => c.Members)
                .HasForeignKey(e => e.CircleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.CircleMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.CircleMembers)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. CircleInvitations
        modelBuilder.Entity<CircleInvitation>(entity =>
        {
            entity.ToTable("CircleInvitations", t =>
            {
                t.HasCheckConstraint("CK_CircleInvitations_NoSelfInvite", "[InviterUserId] <> [InviteeUserId]");
                t.HasCheckConstraint("CK_CircleInvitations_ValidStatus", "[Status] IN ('Pending', 'Accepted', 'Declined', 'Cancelled')");
            });

            entity.HasKey(e => e.InvitationId);
            entity.Property(e => e.InvitationId).ValueGeneratedOnAdd();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.RespondedAt);

            entity.HasIndex(e => new { e.CircleId, e.InviteeUserId, e.Status });

            entity.HasOne(e => e.Circle)
                .WithMany(c => c.Invitations)
                .HasForeignKey(e => e.CircleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.InviterUser)
                .WithMany(u => u.SentInvitations)
                .HasForeignKey(e => e.InviterUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.InviteeUser)
                .WithMany(u => u.ReceivedInvitations)
                .HasForeignKey(e => e.InviteeUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 10. Roles
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");

            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleId).ValueGeneratedOnAdd();
            entity.Property(e => e.RoleName).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.RoleName).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.IsSystemRole).HasDefaultValue(false);
        });

        // 11. Permissions
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");

            entity.HasKey(e => e.PermissionId);
            entity.Property(e => e.PermissionId).ValueGeneratedOnAdd();
            entity.Property(e => e.PermissionCode).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.PermissionCode).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(255);
        });

        // 12. UserRoles
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");

            entity.HasKey(e => e.UserRoleId);
            entity.Property(e => e.UserRoleId).ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. RolePermissions
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");

            entity.HasKey(e => e.RolePermissionId);
            entity.Property(e => e.RolePermissionId).ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();

            entity.HasOne(e => e.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 14. ChatMessages (Upgraded with optional CircleId and SenderId, preserving existing records)
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.CircleId).IsRequired(false);
            entity.Property(e => e.SenderId).IsRequired(false);

            entity.Property(e => e.SenderName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).IsRequired(false);
            entity.Property(e => e.MediaUrl).IsRequired(false).HasMaxLength(1000);
            entity.Property(e => e.VoiceDurationSeconds).IsRequired(false);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.ReadAt).IsRequired(false);

            entity.HasIndex(e => new { e.CircleId, e.Timestamp });
            entity.HasIndex(e => new { e.SenderId, e.Timestamp });
            entity.HasIndex(e => e.Timestamp);

            entity.HasOne(e => e.Circle)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.CircleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 15. ChatMessageReads (Receipt unique per message and user)
        modelBuilder.Entity<ChatMessageRead>(entity =>
        {
            entity.ToTable("ChatMessageReads");

            entity.HasKey(e => e.ChatMessageReadId);
            entity.Property(e => e.ChatMessageReadId).ValueGeneratedOnAdd();
            entity.Property(e => e.ReadAt).IsRequired();

            entity.HasIndex(e => new { e.ChatMessageId, e.UserId }).IsUnique();

            entity.HasOne(e => e.ChatMessage)
                .WithMany(m => m.ReadReceipts)
                .HasForeignKey(e => e.ChatMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.MessageReads)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 16. RefreshTokens (Phase 2 JWT token rotation & revocation)
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");

            entity.HasKey(e => e.RefreshTokenId);
            entity.Property(e => e.RefreshTokenId).ValueGeneratedOnAdd();

            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.RevokedAt);
            entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(256);

            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
