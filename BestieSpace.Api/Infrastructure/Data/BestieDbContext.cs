using BestieSpace.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Infrastructure.Data;

/// <summary>
/// EF Core DbContext following Database-First scaffolded structure.
/// Fluent API mappings match the init.sql schema.
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

    public virtual DbSet<AppStat> AppStats { get; set; } = null!;
    public virtual DbSet<Memory> Memories { get; set; } = null!;
    public virtual DbSet<SecretNote> SecretNotes { get; set; } = null!;
    public virtual DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppStat>(entity =>
        {
            entity.ToTable("AppStats");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.MissYouCount)
                .HasDefaultValue(1000);

            entity.Property(e => e.MainLetterTitle)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(e => e.MainLetterContent)
                .IsRequired();

            entity.Property(e => e.SenderNickname)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.RecipientNickname)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastUpdated)
                .IsRequired();
        });

        modelBuilder.Entity<Memory>(entity =>
        {
            entity.ToTable("Memories");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Caption)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ImageUrl)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.MemoryDate)
                .IsRequired();

            entity.Property(e => e.StickerType)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("heart");

            entity.Property(e => e.RotationAngle)
                .HasDefaultValue(0.0);

            entity.Property(e => e.DisplayOrder)
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<SecretNote>(entity =>
        {
            entity.ToTable("SecretNotes");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.PreviewHint)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(e => e.Content)
                .IsRequired();

            entity.Property(e => e.StickerIcon)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("💌");

            entity.Property(e => e.IsUnlocked)
                .HasDefaultValue(false);

            entity.Property(e => e.UnlockedAt)
                .IsRequired(false);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.SenderName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.MessageType)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Content)
                .IsRequired(false);

            entity.Property(e => e.MediaUrl)
                .IsRequired(false)
                .HasMaxLength(1000);

            entity.Property(e => e.VoiceDurationSeconds)
                .IsRequired(false);

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.IsRead)
                .HasDefaultValue(false);

            entity.Property(e => e.ReadAt)
                .IsRequired(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(e => e.Username)
                .IsUnique();

            entity.Property(e => e.Password)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.RoleKey)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.AvatarSticker)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.PasswordHint)
                .IsRequired(false)
                .HasMaxLength(255);

            entity.Property(e => e.QuickPin)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValue("3000");

            entity.Property(e => e.IsOnline)
                .HasDefaultValue(false);

            entity.Property(e => e.LastSeenAt)
                .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.LastLoginAt)
                .IsRequired(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
