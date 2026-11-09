using System;
using System.Collections.Generic;
using BestieSpace.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Infrastructure.Data;

public partial class BestieDbContext : DbContext
{
    public BestieDbContext(DbContextOptions<BestieDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppStat> AppStats { get; set; }

    public virtual DbSet<BestieCircle> BestieCircles { get; set; }

    public virtual DbSet<BestieCircleMember> BestieCircleMembers { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<ChatMessageDelivery> ChatMessageDeliveries { get; set; }

    public virtual DbSet<ChatMessageRead> ChatMessageReads { get; set; }

    public virtual DbSet<CircleInvitation> CircleInvitations { get; set; }

    public virtual DbSet<Friendship> Friendships { get; set; }

    public virtual DbSet<MediaFile> MediaFiles { get; set; }

    public virtual DbSet<Memory> Memories { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<SecretNote> SecretNotes { get; set; }

    public virtual DbSet<Sticker> Stickers { get; set; }

    public virtual DbSet<StickerCategory> StickerCategories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBlock> UserBlocks { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppStat>(entity =>
        {
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.MainLetterTitle).HasMaxLength(250);
            entity.Property(e => e.MissYouCount).HasDefaultValue(1000, "DF_AppStats_MissYouCount");
            entity.Property(e => e.RecipientNickname).HasMaxLength(100);
            entity.Property(e => e.SenderNickname).HasMaxLength(100);
        });

        modelBuilder.Entity<BestieCircle>(entity =>
        {
            entity.HasKey(e => e.CircleId);

            entity.HasIndex(e => e.IsActive, "IX_BestieCircles_IsActive");

            entity.HasIndex(e => e.OwnerUserId, "IX_BestieCircles_OwnerUserId");

            entity.Property(e => e.CircleIcon)
                .HasMaxLength(50)
                .HasDefaultValue("ðŸŒ¸", "DF_BestieCircles_CircleIcon");
            entity.Property(e => e.CircleImage).HasMaxLength(1000);
            entity.Property(e => e.CircleName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_BestieCircles_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_BestieCircles_IsActive");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.BestieCircles)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BestieCircles_Owner");
        });

        modelBuilder.Entity<BestieCircleMember>(entity =>
        {
            entity.HasKey(e => e.CircleMemberId);

            entity.HasIndex(e => new { e.CircleId, e.IsActive }, "IX_BestieCircleMembers_CircleId_IsActive");

            entity.HasIndex(e => new { e.CircleId, e.UserId }, "IX_BestieCircleMembers_CircleId_UserId").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.IsActive }, "IX_BestieCircleMembers_UserId_IsActive");

            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_BestieCircleMembers_IsActive");
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_BestieCircleMembers_JoinedAt");

            entity.HasOne(d => d.Circle).WithMany(p => p.BestieCircleMembers)
                .HasForeignKey(d => d.CircleId)
                .HasConstraintName("FK_BestieCircleMembers_Circle");

            entity.HasOne(d => d.Role).WithMany(p => p.BestieCircleMembers)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BestieCircleMembers_Role");

            entity.HasOne(d => d.User).WithMany(p => p.BestieCircleMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BestieCircleMembers_User");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(e => new { e.CircleId, e.Timestamp }, "IX_ChatMessages_Circle_Timestamp");

            entity.HasIndex(e => new { e.SenderId, e.Timestamp }, "IX_ChatMessages_Sender_Timestamp");

            entity.HasIndex(e => e.Timestamp, "IX_ChatMessages_Timestamp");

            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.FileName).HasMaxLength(260);
            entity.Property(e => e.MediaUrl).HasMaxLength(1000);
            entity.Property(e => e.MessageType)
                .HasMaxLength(20)
                .HasDefaultValue("text", "DF_ChatMessages_MessageType");
            entity.Property(e => e.SenderName).HasMaxLength(100);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(1000);

            entity.HasOne(d => d.Circle).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.CircleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ChatMessages_Circle");

            entity.HasOne(d => d.Sender).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ChatMessages_Sender");
        });

        modelBuilder.Entity<ChatMessageDelivery>(entity =>
        {
            entity.HasIndex(e => new { e.ChatMessageId, e.UserId }, "IX_ChatMessageDeliveries_Message_User").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.DeliveredAt }, "IX_ChatMessageDeliveries_User_DeliveredAt");

            entity.Property(e => e.DeliveredAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChatMessageDeliveries_DeliveredAt");

            entity.HasOne(d => d.ChatMessage).WithMany(p => p.ChatMessageDeliveries)
                .HasForeignKey(d => d.ChatMessageId)
                .HasConstraintName("FK_ChatMessageDeliveries_Message");

            entity.HasOne(d => d.User).WithMany(p => p.ChatMessageDeliveries)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatMessageDeliveries_User");
        });

        modelBuilder.Entity<ChatMessageRead>(entity =>
        {
            entity.HasIndex(e => new { e.ChatMessageId, e.UserId }, "IX_ChatMessageReads_Message_User").IsUnique();

            entity.Property(e => e.ReadAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChatMessageReads_ReadAt");

            entity.HasOne(d => d.ChatMessage).WithMany(p => p.ChatMessageReads)
                .HasForeignKey(d => d.ChatMessageId)
                .HasConstraintName("FK_ChatMessageReads_Message");

            entity.HasOne(d => d.User).WithMany(p => p.ChatMessageReads)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatMessageReads_User");
        });

        modelBuilder.Entity<CircleInvitation>(entity =>
        {
            entity.HasKey(e => e.InvitationId);

            entity.HasIndex(e => new { e.CircleId, e.InviteeUserId, e.Status }, "IX_CircleInvitations_Circle_Invitee_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_CircleInvitations_CreatedAt");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending", "DF_CircleInvitations_Status");

            entity.HasOne(d => d.Circle).WithMany(p => p.CircleInvitations)
                .HasForeignKey(d => d.CircleId)
                .HasConstraintName("FK_CircleInvitations_Circle");

            entity.HasOne(d => d.InviteeUser).WithMany(p => p.CircleInvitationInviteeUsers)
                .HasForeignKey(d => d.InviteeUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CircleInvitations_Invitee");

            entity.HasOne(d => d.InviterUser).WithMany(p => p.CircleInvitationInviterUsers)
                .HasForeignKey(d => d.InviterUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CircleInvitations_Inviter");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasIndex(e => new { e.UserA, e.UserB }, "IX_Friendships_CanonicalPair").IsUnique();

            entity.HasIndex(e => new { e.FriendUserId, e.Status }, "IX_Friendships_FriendUserId_Status");

            entity.HasIndex(e => new { e.UserId, e.Status }, "IX_Friendships_UserId_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Friendships_CreatedAt");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending", "DF_Friendships_Status");
            entity.Property(e => e.UserA).HasComputedColumnSql("(case when [UserId]<[FriendUserId] then [UserId] else [FriendUserId] end)", true);
            entity.Property(e => e.UserB).HasComputedColumnSql("(case when [UserId]<[FriendUserId] then [FriendUserId] else [UserId] end)", true);

            entity.HasOne(d => d.FriendUser).WithMany(p => p.FriendshipFriendUsers)
                .HasForeignKey(d => d.FriendUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Friendships_FriendUsers");

            entity.HasOne(d => d.User).WithMany(p => p.FriendshipUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Friendships_Users");
        });

        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.MediaId);

            entity.HasIndex(e => new { e.CircleId, e.CreatedAt }, "IX_MediaFiles_Circle_CreatedAt");

            entity.HasIndex(e => e.UploadedByUserId, "IX_MediaFiles_UploadedBy");

            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_MediaFiles_CreatedAt");
            entity.Property(e => e.MediaType).HasMaxLength(20);
            entity.Property(e => e.OriginalFileName).HasMaxLength(260);
            entity.Property(e => e.StorageKey).HasMaxLength(260);
            entity.Property(e => e.ThumbnailStorageKey).HasMaxLength(260);

            entity.HasOne(d => d.Circle).WithMany(p => p.MediaFiles)
                .HasForeignKey(d => d.CircleId)
                .HasConstraintName("FK_MediaFiles_Circle");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.MediaFiles)
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MediaFiles_User");
        });

        modelBuilder.Entity<Memory>(entity =>
        {
            entity.Property(e => e.Caption).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ImageUrl).HasMaxLength(1000);
            entity.Property(e => e.StickerType)
                .HasMaxLength(50)
                .HasDefaultValue("heart", "DF_Memories_StickerType");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_Notifications_User_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "IX_Notifications_User_IsRead_CreatedAt").IsDescending(false, false, true);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Notifications_CreatedAt");
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.NotificationType).HasMaxLength(100);
            entity.Property(e => e.RelatedEntityType).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(250);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notifications_User");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(e => e.PermissionCode, "IX_Permissions_PermissionCode").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.PermissionCode).HasMaxLength(100);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.ExpiresAt, "IX_RefreshTokens_ExpiresAt");

            entity.HasIndex(e => e.TokenHash, "IX_RefreshTokens_TokenHash");

            entity.HasIndex(e => e.UserId, "IX_RefreshTokens_UserId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_RefreshTokens_CreatedAt");
            entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(256);
            entity.Property(e => e.TokenHash).HasMaxLength(256);

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_RefreshTokens_Users");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.RoleName, "IX_Roles_RoleName").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasIndex(e => new { e.RoleId, e.PermissionId }, "IX_RolePermissions_RoleId_PermissionId").IsUnique();

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("FK_RolePermissions_Permissions");

            entity.HasOne(d => d.Role).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_RolePermissions_Roles");
        });

        modelBuilder.Entity<SecretNote>(entity =>
        {
            entity.Property(e => e.PreviewHint).HasMaxLength(300);
            entity.Property(e => e.StickerIcon)
                .HasMaxLength(50)
                .HasDefaultValue("ðŸ’Œ", "DF_SecretNotes_StickerIcon");
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<Sticker>(entity =>
        {
            entity.HasIndex(e => new { e.CategoryId, e.DisplayOrder }, "IX_Stickers_Category_Order");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Stickers_CreatedAt");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Stickers_IsActive");
            entity.Property(e => e.StickerName).HasMaxLength(100);

            entity.HasOne(d => d.Category).WithMany(p => p.Stickers)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_Stickers_Category");
        });

        modelBuilder.Entity<StickerCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId);

            entity.HasIndex(e => e.CategoryName, "UQ_StickerCategories_Name").IsUnique();

            entity.Property(e => e.CategoryName).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_StickerCategories_CreatedAt");
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_StickerCategories_IsActive");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.NormalizedUsername, "IX_Users_NormalizedUsername").IsUnique();

            entity.HasIndex(e => e.Username, "IX_Users_Username").IsUnique();

            entity.Property(e => e.AvatarSticker)
                .HasMaxLength(50)
                .HasDefaultValue("ðŸŒ¸", "DF_Users_AvatarSticker");
            entity.Property(e => e.Bio).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Users_CreatedAt");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(100)
                .HasDefaultValue("", "DF_Users_DisplayName");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Users_IsActive");
            entity.Property(e => e.NormalizedEmail).HasMaxLength(255);
            entity.Property(e => e.NormalizedUsername).HasMaxLength(50);
            entity.Property(e => e.Password)
                .HasMaxLength(100)
                .HasDefaultValue("", "DF_Users_Password");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PasswordHint).HasMaxLength(255);
            entity.Property(e => e.ProfilePicture).HasMaxLength(1000);
            entity.Property(e => e.QuickPin)
                .HasMaxLength(10)
                .HasDefaultValue("3000", "DF_Users_QuickPin");
            entity.Property(e => e.RoleKey)
                .HasMaxLength(50)
                .HasDefaultValue("User", "DF_Users_RoleKey");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.HasIndex(e => new { e.BlockerUserId, e.BlockedUserId }, "IX_UserBlocks_Blocker_Blocked").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_UserBlocks_CreatedAt");

            entity.HasOne(d => d.BlockedUser).WithMany(p => p.UserBlockBlockedUsers)
                .HasForeignKey(d => d.BlockedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserBlocks_Blocked");

            entity.HasOne(d => d.BlockerUser).WithMany(p => p.UserBlockBlockerUsers)
                .HasForeignKey(d => d.BlockerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserBlocks_Blocker");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.RoleId }, "IX_UserRoles_UserId_RoleId").IsUnique();

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_UserRoles_Roles");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserRoles_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
