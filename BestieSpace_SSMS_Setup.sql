-- ==============================================================================
-- BestieSpace SQL Server Management Studio (SSMS) Setup Script
-- Database: BestieSpaceDb
-- Server: localhost
-- User: sa / sasa@123
-- ==============================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'BestieSpaceDb')
BEGIN
    PRINT 'Creating database [BestieSpaceDb]...';
    CREATE DATABASE [BestieSpaceDb];
END
ELSE
BEGIN
    PRINT 'Database [BestieSpaceDb] already exists.';
END
GO

USE [BestieSpaceDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ==============================================================================
-- BestieSpace Phase 1: Microsoft SQL Server Initial Schema & Seed Script
-- Platform: Microsoft SQL Server 2019 / 2022 / LocalDB / Azure SQL Database
-- Requirements: Idempotent, safe to run multiple times, preserves existing records.
-- ==============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. AppStats (Global miss-you meter & heartfelt letter)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppStats')
BEGIN
    CREATE TABLE [dbo].[AppStats] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [MissYouCount] INT NOT NULL CONSTRAINT [DF_AppStats_MissYouCount] DEFAULT 1000,
        [MainLetterTitle] NVARCHAR(250) NOT NULL,
        [MainLetterContent] NVARCHAR(MAX) NOT NULL,
        [SenderNickname] NVARCHAR(100) NOT NULL,
        [RecipientNickname] NVARCHAR(100) NOT NULL,
        [LastUpdated] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_AppStats] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 2. Memories (Polaroid gallery memories)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Memories')
BEGIN
    CREATE TABLE [dbo].[Memories] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Caption] NVARCHAR(500) NOT NULL,
        [ImageUrl] NVARCHAR(1000) NOT NULL,
        [MemoryDate] DATETIME2 NOT NULL,
        [StickerType] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Memories_StickerType] DEFAULT 'heart',
        [RotationAngle] FLOAT NOT NULL CONSTRAINT [DF_Memories_RotationAngle] DEFAULT 0.0,
        [DisplayOrder] INT NOT NULL CONSTRAINT [DF_Memories_DisplayOrder] DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Memories] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 3. SecretNotes (Mystery envelope notes)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SecretNotes')
BEGIN
    CREATE TABLE [dbo].[SecretNotes] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [PreviewHint] NVARCHAR(300) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [StickerIcon] NVARCHAR(50) NOT NULL CONSTRAINT [DF_SecretNotes_StickerIcon] DEFAULT '💌',
        [IsUnlocked] BIT NOT NULL CONSTRAINT [DF_SecretNotes_IsUnlocked] DEFAULT 0,
        [UnlockedAt] DATETIME2 NULL,
        CONSTRAINT [PK_SecretNotes] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- 4. Users (Upgraded with UserId PK, normalized fields, while preserving existing login/auth credentials)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [UserId] INT IDENTITY(1,1) NOT NULL,
        [Username] NVARCHAR(50) NOT NULL,
        [NormalizedUsername] NVARCHAR(50) NOT NULL,
        [Email] NVARCHAR(255) NULL,
        [NormalizedEmail] NVARCHAR(255) NULL,
        [PasswordHash] NVARCHAR(255) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT 1,
        
        -- Preserved fields from initial Kawaii clubhouse implementation
        [Password] NVARCHAR(100) NOT NULL CONSTRAINT [DF_Users_Password] DEFAULT '',
        [DisplayName] NVARCHAR(100) NOT NULL CONSTRAINT [DF_Users_DisplayName] DEFAULT '',
        [ProfilePicture] NVARCHAR(1000) NULL,
        [Bio] NVARCHAR(1000) NULL,
        [RoleKey] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Users_RoleKey] DEFAULT 'User',
        [AvatarSticker] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Users_AvatarSticker] DEFAULT '🌸',
        [PasswordHint] NVARCHAR(255) NULL,
        [QuickPin] NVARCHAR(10) NOT NULL CONSTRAINT [DF_Users_QuickPin] DEFAULT '3000',
        [IsOnline] BIT NOT NULL CONSTRAINT [DF_Users_IsOnline] DEFAULT 0,
        [LastSeenAt] DATETIME2 NULL,
        [LastLoginAt] DATETIME2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_Username] ON [dbo].[Users]([Username] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_NormalizedUsername] ON [dbo].[Users]([NormalizedUsername] ASC);
    CREATE NONCLUSTERED INDEX [IX_Users_NormalizedEmail] ON [dbo].[Users]([NormalizedEmail] ASC) WHERE [NormalizedEmail] IS NOT NULL;
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'ProfilePicture')
        ALTER TABLE [dbo].[Users] ADD [ProfilePicture] NVARCHAR(1000) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'Bio')
        ALTER TABLE [dbo].[Users] ADD [Bio] NVARCHAR(1000) NULL;
END
GO

-- 5. Roles (System and Circle RBAC roles)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles')
BEGIN
    CREATE TABLE [dbo].[Roles] (
        [RoleId] INT IDENTITY(1,1) NOT NULL,
        [RoleName] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(255) NULL,
        [IsSystemRole] BIT NOT NULL CONSTRAINT [DF_Roles_IsSystemRole] DEFAULT 0,
        CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Roles_RoleName] ON [dbo].[Roles]([RoleName] ASC);
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Roles]') AND name = 'IsSystemRole')
    BEGIN
        ALTER TABLE [dbo].[Roles] ADD [IsSystemRole] BIT NOT NULL CONSTRAINT [DF_Roles_IsSystemRole] DEFAULT 0;
    END
END
GO

-- 6. Permissions (Granular capability codes)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Permissions')
BEGIN
    CREATE TABLE [dbo].[Permissions] (
        [PermissionId] INT IDENTITY(1,1) NOT NULL,
        [PermissionCode] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(255) NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED ([PermissionId] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Permissions_PermissionCode] ON [dbo].[Permissions]([PermissionCode] ASC);
END
GO

-- 7. UserRoles (Associative entity for users and roles)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserRoles')
BEGIN
    CREATE TABLE [dbo].[UserRoles] (
        [UserRoleId] INT IDENTITY(1,1) NOT NULL,
        [UserId] INT NOT NULL,
        [RoleId] INT NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserRoleId] ASC),
        CONSTRAINT [FK_UserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([RoleId]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_UserRoles_UserId_RoleId] ON [dbo].[UserRoles]([UserId] ASC, [RoleId] ASC);
END
GO

-- 8. RolePermissions (Associative entity for roles and permissions)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RolePermissions')
BEGIN
    CREATE TABLE [dbo].[RolePermissions] (
        [RolePermissionId] INT IDENTITY(1,1) NOT NULL,
        [RoleId] INT NOT NULL,
        [PermissionId] INT NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([RolePermissionId] ASC),
        CONSTRAINT [FK_RolePermissions_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([RoleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Permissions] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions]([PermissionId]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_RolePermissions_RoleId_PermissionId] ON [dbo].[RolePermissions]([RoleId] ASC, [PermissionId] ASC);
END
GO

-- 9. Friendships (Consistent canonical pair storage preventing duplicates & self-friendship)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Friendships')
BEGIN
    CREATE TABLE [dbo].[Friendships] (
        [FriendshipId] INT IDENTITY(1,1) NOT NULL,
        [UserId] INT NOT NULL,
        [FriendUserId] INT NOT NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Friendships_Status] DEFAULT 'Pending',
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Friendships_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,

        -- Canonical user pairing computed columns: guaranteed duplicate prevention in any order
        [UserA] AS (CASE WHEN [UserId] < [FriendUserId] THEN [UserId] ELSE [FriendUserId] END) PERSISTED,
        [UserB] AS (CASE WHEN [UserId] < [FriendUserId] THEN [FriendUserId] ELSE [UserId] END) PERSISTED,

        CONSTRAINT [PK_Friendships] PRIMARY KEY CLUSTERED ([FriendshipId] ASC),
        CONSTRAINT [CK_Friendships_NoSelfFriendship] CHECK ([UserId] <> [FriendUserId]),
        CONSTRAINT [CK_Friendships_ValidStatus] CHECK ([Status] IN ('Pending', 'Accepted', 'Rejected')),
        CONSTRAINT [FK_Friendships_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]),
        CONSTRAINT [FK_Friendships_FriendUsers] FOREIGN KEY ([FriendUserId]) REFERENCES [dbo].[Users]([UserId])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Friendships_CanonicalPair] ON [dbo].[Friendships]([UserA] ASC, [UserB] ASC);
    CREATE NONCLUSTERED INDEX [IX_Friendships_UserId_Status] ON [dbo].[Friendships]([UserId] ASC, [Status] ASC);
    CREATE NONCLUSTERED INDEX [IX_Friendships_FriendUserId_Status] ON [dbo].[Friendships]([FriendUserId] ASC, [Status] ASC);
END
GO

-- 10. UserBlocks (User blocking mechanism)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserBlocks')
BEGIN
    CREATE TABLE [dbo].[UserBlocks] (
        [UserBlockId] INT IDENTITY(1,1) NOT NULL,
        [BlockerUserId] INT NOT NULL,
        [BlockedUserId] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserBlocks_CreatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_UserBlocks] PRIMARY KEY CLUSTERED ([UserBlockId] ASC),
        CONSTRAINT [CK_UserBlocks_NoSelfBlock] CHECK ([BlockerUserId] <> [BlockedUserId]),
        CONSTRAINT [FK_UserBlocks_Blocker] FOREIGN KEY ([BlockerUserId]) REFERENCES [dbo].[Users]([UserId]),
        CONSTRAINT [FK_UserBlocks_Blocked] FOREIGN KEY ([BlockedUserId]) REFERENCES [dbo].[Users]([UserId])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_UserBlocks_Blocker_Blocked] ON [dbo].[UserBlocks]([BlockerUserId] ASC, [BlockedUserId] ASC);
END
GO

-- 11. BestieCircles (Private circles, max 9 members including owner enforced transactionally in service layer)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BestieCircles')
BEGIN
    CREATE TABLE [dbo].[BestieCircles] (
        [CircleId] INT IDENTITY(1,1) NOT NULL,
        [CircleName] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [CircleIcon] NVARCHAR(50) NOT NULL CONSTRAINT [DF_BestieCircles_CircleIcon] DEFAULT '🌸',
        [CircleImage] NVARCHAR(1000) NULL,
        [OwnerUserId] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_BestieCircles_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_BestieCircles_IsActive] DEFAULT 1,
        CONSTRAINT [PK_BestieCircles] PRIMARY KEY CLUSTERED ([CircleId] ASC),
        CONSTRAINT [FK_BestieCircles_Owner] FOREIGN KEY ([OwnerUserId]) REFERENCES [dbo].[Users]([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_BestieCircles_OwnerUserId] ON [dbo].[BestieCircles]([OwnerUserId] ASC);
    CREATE NONCLUSTERED INDEX [IX_BestieCircles_IsActive] ON [dbo].[BestieCircles]([IsActive] ASC);
END
GO

-- 12. BestieCircleMembers (Circle membership & roles)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BestieCircleMembers')
BEGIN
    CREATE TABLE [dbo].[BestieCircleMembers] (
        [CircleMemberId] INT IDENTITY(1,1) NOT NULL,
        [CircleId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [RoleId] INT NOT NULL,
        [JoinedAt] DATETIME2 NOT NULL CONSTRAINT [DF_BestieCircleMembers_JoinedAt] DEFAULT SYSUTCDATETIME(),
        [IsActive] BIT NOT NULL CONSTRAINT [DF_BestieCircleMembers_IsActive] DEFAULT 1,
        CONSTRAINT [PK_BestieCircleMembers] PRIMARY KEY CLUSTERED ([CircleMemberId] ASC),
        CONSTRAINT [FK_BestieCircleMembers_Circle] FOREIGN KEY ([CircleId]) REFERENCES [dbo].[BestieCircles]([CircleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_BestieCircleMembers_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]),
        CONSTRAINT [FK_BestieCircleMembers_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([RoleId])
    );

    -- Prevents user from joining the same circle twice
    CREATE UNIQUE NONCLUSTERED INDEX [IX_BestieCircleMembers_CircleId_UserId] ON [dbo].[BestieCircleMembers]([CircleId] ASC, [UserId] ASC);
    CREATE NONCLUSTERED INDEX [IX_BestieCircleMembers_CircleId_IsActive] ON [dbo].[BestieCircleMembers]([CircleId] ASC, [IsActive] ASC);
    CREATE NONCLUSTERED INDEX [IX_BestieCircleMembers_UserId_IsActive] ON [dbo].[BestieCircleMembers]([UserId] ASC, [IsActive] ASC);
END
GO

-- 13. CircleInvitations (Invitations to join circles)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CircleInvitations')
BEGIN
    CREATE TABLE [dbo].[CircleInvitations] (
        [InvitationId] INT IDENTITY(1,1) NOT NULL,
        [CircleId] INT NOT NULL,
        [InviterUserId] INT NOT NULL,
        [InviteeUserId] INT NOT NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_CircleInvitations_Status] DEFAULT 'Pending',
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_CircleInvitations_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [RespondedAt] DATETIME2 NULL,
        CONSTRAINT [PK_CircleInvitations] PRIMARY KEY CLUSTERED ([InvitationId] ASC),
        CONSTRAINT [CK_CircleInvitations_NoSelfInvite] CHECK ([InviterUserId] <> [InviteeUserId]),
        CONSTRAINT [CK_CircleInvitations_ValidStatus] CHECK ([Status] IN ('Pending', 'Accepted', 'Declined', 'Rejected', 'Cancelled')),
        CONSTRAINT [FK_CircleInvitations_Circle] FOREIGN KEY ([CircleId]) REFERENCES [dbo].[BestieCircles]([CircleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_CircleInvitations_Inviter] FOREIGN KEY ([InviterUserId]) REFERENCES [dbo].[Users]([UserId]),
        CONSTRAINT [FK_CircleInvitations_Invitee] FOREIGN KEY ([InviteeUserId]) REFERENCES [dbo].[Users]([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_CircleInvitations_Circle_Invitee_Status] ON [dbo].[CircleInvitations]([CircleId] ASC, [InviteeUserId] ASC, [Status] ASC);
END
GO

-- 14. ChatMessages (Upgraded with optional CircleId and SenderId, preserving existing 1-on-1 private clubhouse messages)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessages')
BEGIN
    CREATE TABLE [dbo].[ChatMessages] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [CircleId] INT NULL,
        [SenderId] INT NULL,
        [SenderName] NVARCHAR(100) NOT NULL,
        [MessageType] NVARCHAR(20) NOT NULL CONSTRAINT [DF_ChatMessages_MessageType] DEFAULT 'text',
        [Content] NVARCHAR(MAX) NULL,
        [MediaUrl] NVARCHAR(1000) NULL,
        [VoiceDurationSeconds] FLOAT NULL,
        [ThumbnailUrl] NVARCHAR(1000) NULL,
        [FileName] NVARCHAR(260) NULL,
        [ContentType] NVARCHAR(100) NULL,
        [FileSize] BIGINT NULL,
        [MediaId] INT NULL,
        [Timestamp] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsDeleted] DEFAULT 0,
        [DeletedAt] DATETIME2 NULL,
        [IsRead] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsRead] DEFAULT 0,
        [ReadAt] DATETIME2 NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ChatMessages_Circle] FOREIGN KEY ([CircleId]) REFERENCES [dbo].[BestieCircles]([CircleId]) ON DELETE SET NULL,
        CONSTRAINT [FK_ChatMessages_Sender] FOREIGN KEY ([SenderId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE SET NULL
    );

    CREATE NONCLUSTERED INDEX [IX_ChatMessages_Circle_Timestamp] ON [dbo].[ChatMessages]([CircleId] ASC, [Timestamp] ASC);
    CREATE NONCLUSTERED INDEX [IX_ChatMessages_Sender_Timestamp] ON [dbo].[ChatMessages]([SenderId] ASC, [Timestamp] ASC);
    CREATE NONCLUSTERED INDEX [IX_ChatMessages_Timestamp] ON [dbo].[ChatMessages]([Timestamp] ASC);
END
ELSE
BEGIN
    -- Safe schema migration if ChatMessages already exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'CircleId')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [CircleId] INT NULL;
        ALTER TABLE [dbo].[ChatMessages] ADD CONSTRAINT [FK_ChatMessages_Circle] FOREIGN KEY ([CircleId]) REFERENCES [dbo].[BestieCircles]([CircleId]) ON DELETE SET NULL;
        CREATE NONCLUSTERED INDEX [IX_ChatMessages_Circle_Timestamp] ON [dbo].[ChatMessages]([CircleId] ASC, [Timestamp] ASC);
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'SenderId')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [SenderId] INT NULL;
        ALTER TABLE [dbo].[ChatMessages] ADD CONSTRAINT [FK_ChatMessages_Sender] FOREIGN KEY ([SenderId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE SET NULL;
        CREATE NONCLUSTERED INDEX [IX_ChatMessages_Sender_Timestamp] ON [dbo].[ChatMessages]([SenderId] ASC, [Timestamp] ASC);
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'UpdatedAt')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [UpdatedAt] DATETIME2 NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'IsDeleted')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsDeleted] DEFAULT 0;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'DeletedAt')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [DeletedAt] DATETIME2 NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'ThumbnailUrl')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [ThumbnailUrl] NVARCHAR(1000) NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'FileName')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [FileName] NVARCHAR(260) NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'ContentType')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [ContentType] NVARCHAR(100) NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'FileSize')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [FileSize] BIGINT NULL;
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[ChatMessages]') AND name = 'MediaId')
    BEGIN
        ALTER TABLE [dbo].[ChatMessages] ADD [MediaId] INT NULL;
    END
END
GO

-- 15. ChatMessageReads (Read receipts unique per message and user)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessageReads')
BEGIN
    CREATE TABLE [dbo].[ChatMessageReads] (
        [ChatMessageReadId] INT IDENTITY(1,1) NOT NULL,
        [ChatMessageId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [ReadAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatMessageReads_ReadAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_ChatMessageReads] PRIMARY KEY CLUSTERED ([ChatMessageReadId] ASC),
        CONSTRAINT [FK_ChatMessageReads_Message] FOREIGN KEY ([ChatMessageId]) REFERENCES [dbo].[ChatMessages]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChatMessageReads_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_ChatMessageReads_Message_User] ON [dbo].[ChatMessageReads]([ChatMessageId] ASC, [UserId] ASC);
END
GO

-- 15B. MediaFiles (Phase 7 Private Circle Multimedia Storage Metadata)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MediaFiles')
BEGIN
    CREATE TABLE [dbo].[MediaFiles] (
        [MediaId] INT IDENTITY(1,1) NOT NULL,
        [CircleId] INT NOT NULL,
        [UploadedByUserId] INT NOT NULL,
        [StorageKey] NVARCHAR(260) NOT NULL,
        [ThumbnailStorageKey] NVARCHAR(260) NULL,
        [OriginalFileName] NVARCHAR(260) NOT NULL,
        [ContentType] NVARCHAR(100) NOT NULL,
        [MediaType] NVARCHAR(20) NOT NULL,
        [FileSize] BIGINT NOT NULL,
        [DurationSeconds] FLOAT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MediaFiles_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_MediaFiles_IsDeleted] DEFAULT 0,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [PK_MediaFiles] PRIMARY KEY CLUSTERED ([MediaId] ASC),
        CONSTRAINT [FK_MediaFiles_Circle] FOREIGN KEY ([CircleId]) REFERENCES [dbo].[BestieCircles]([CircleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MediaFiles_User] FOREIGN KEY ([UploadedByUserId]) REFERENCES [dbo].[Users]([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_MediaFiles_Circle_CreatedAt] ON [dbo].[MediaFiles]([CircleId] ASC, [CreatedAt] ASC);
    CREATE NONCLUSTERED INDEX [IX_MediaFiles_UploadedBy] ON [dbo].[MediaFiles]([UploadedByUserId] ASC);
END
GO

-- 15C. StickerCategories (Phase 7 Built-In Sticker Collections)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StickerCategories')
BEGIN
    CREATE TABLE [dbo].[StickerCategories] (
        [CategoryId] INT IDENTITY(1,1) NOT NULL,
        [CategoryName] NVARCHAR(50) NOT NULL,
        [Icon] NVARCHAR(50) NOT NULL,
        [DisplayOrder] INT NOT NULL CONSTRAINT [DF_StickerCategories_DisplayOrder] DEFAULT 0,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_StickerCategories_IsActive] DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_StickerCategories_CreatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_StickerCategories] PRIMARY KEY CLUSTERED ([CategoryId] ASC),
        CONSTRAINT [UQ_StickerCategories_Name] UNIQUE ([CategoryName])
    );
END
GO

-- 15D. Stickers (Phase 7 Stickers)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Stickers')
BEGIN
    CREATE TABLE [dbo].[Stickers] (
        [StickerId] INT IDENTITY(1,1) NOT NULL,
        [CategoryId] INT NOT NULL,
        [StickerName] NVARCHAR(100) NOT NULL,
        [ImageUrl] NVARCHAR(500) NOT NULL,
        [DisplayOrder] INT NOT NULL CONSTRAINT [DF_Stickers_DisplayOrder] DEFAULT 0,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Stickers_IsActive] DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Stickers_CreatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Stickers] PRIMARY KEY CLUSTERED ([StickerId] ASC),
        CONSTRAINT [FK_Stickers_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[StickerCategories]([CategoryId]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Stickers_Category_Order] ON [dbo].[Stickers]([CategoryId] ASC, [DisplayOrder] ASC);
END
GO

-- 15E. ChatMessageDeliveries (Phase 8: Independent delivery acknowledgment per recipient)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessageDeliveries')
BEGIN
    CREATE TABLE [dbo].[ChatMessageDeliveries] (
        [ChatMessageDeliveryId] INT IDENTITY(1,1) NOT NULL,
        [ChatMessageId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [DeliveredAt] DATETIME2 NOT NULL CONSTRAINT [DF_ChatMessageDeliveries_DeliveredAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_ChatMessageDeliveries] PRIMARY KEY CLUSTERED ([ChatMessageDeliveryId] ASC),
        CONSTRAINT [FK_ChatMessageDeliveries_Message] FOREIGN KEY ([ChatMessageId]) REFERENCES [dbo].[ChatMessages]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChatMessageDeliveries_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_ChatMessageDeliveries_Message_User] ON [dbo].[ChatMessageDeliveries]([ChatMessageId] ASC, [UserId] ASC);
    CREATE NONCLUSTERED INDEX [IX_ChatMessageDeliveries_User_DeliveredAt] ON [dbo].[ChatMessageDeliveries]([UserId] ASC, [DeliveredAt] ASC);
END
GO

-- 15F. Notifications (Phase 8: Durable user notifications)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE [dbo].[Notifications] (
        [NotificationId] INT IDENTITY(1,1) NOT NULL,
        [UserId] INT NOT NULL,
        [NotificationType] NVARCHAR(100) NOT NULL,
        [Title] NVARCHAR(250) NOT NULL,
        [Message] NVARCHAR(1000) NOT NULL,
        [RelatedEntityId] INT NULL,
        [RelatedEntityType] NVARCHAR(50) NULL,
        [IsRead] BIT NOT NULL CONSTRAINT [DF_Notifications_IsRead] DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Notifications_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [ReadAt] DATETIME2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([NotificationId] ASC),
        CONSTRAINT [FK_Notifications_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Notifications_User_IsRead_CreatedAt] ON [dbo].[Notifications]([UserId] ASC, [IsRead] ASC, [CreatedAt] DESC);
    CREATE NONCLUSTERED INDEX [IX_Notifications_User_CreatedAt] ON [dbo].[Notifications]([UserId] ASC, [CreatedAt] DESC);
END
GO

-- ==============================================================================
-- 16. Seed Initial System Roles & Permissions (Phase 3 RBAC)
-- ==============================================================================

-- Seed Protected System Roles
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'SuperAdmin')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('SuperAdmin', 'Supreme administrator with full control over all system operations, roles, and staff', 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'Admin')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('Admin', 'System administrator capable of managing users, roles, and permissions', 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'User')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('User', 'Default registered platform user with personal profile and social features', 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'Bestie')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('Bestie', 'VIP Bestie role with full access to private clubhouse features', 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'Owner')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('Owner', 'Owner and creator of a Bestie Circle with circle administration rights', 0);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'Member')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('Member', 'Regular member of a Bestie Circle with chat and memory sharing rights', 0);

-- Phase 5 Circle Roles
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'CircleOwner')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('CircleOwner', 'Sole owner and administrator of a Bestie Circle with full privileges', 0);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'CircleAdmin')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('CircleAdmin', 'Circle administrator capable of managing invitations and ordinary members', 0);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'CircleMember')
    INSERT INTO [dbo].[Roles] ([RoleName], [Description], [IsSystemRole]) VALUES ('CircleMember', 'Active circle member with viewing, chat, and departure privileges', 0);
GO

-- Seed Standard Dynamic Permissions Catalog
IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_VIEW', 'View user profiles and list platform members');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_CREATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_CREATE', 'Register or create new user accounts');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_UPDATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_UPDATE', 'Update user profiles and account details');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_DELETE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_DELETE', 'Delete or permanently remove user accounts');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_MANAGE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_MANAGE', 'Full user administration, status activation, and account management');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'ROLE_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('ROLE_VIEW', 'View system and custom security roles');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'ROLE_CREATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('ROLE_CREATE', 'Create new security and circle roles');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'ROLE_UPDATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('ROLE_UPDATE', 'Modify role descriptions and names');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'ROLE_DELETE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('ROLE_DELETE', 'Delete custom non-system roles');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'PERMISSION_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('PERMISSION_VIEW', 'View the global permission catalog and matrix');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'PERMISSION_ASSIGN')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('PERMISSION_ASSIGN', 'Assign or revoke permissions from security roles');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_ROLE_MANAGE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_ROLE_MANAGE', 'Assign or revoke roles from user accounts');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'FRIEND_REQUEST_SEND')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('FRIEND_REQUEST_SEND', 'Send friend requests to other users');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'FRIEND_REQUEST_ACCEPT')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('FRIEND_REQUEST_ACCEPT', 'Accept or decline received friend requests');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'FRIEND_REMOVE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('FRIEND_REMOVE', 'Remove friends or block users');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_CREATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_CREATE', 'Create new Bestie Circles (clubs)');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_VIEW', 'View circle details, member roster, and activities');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_UPDATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_UPDATE', 'Update circle name, description, and settings');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_DELETE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_DELETE', 'Disband or delete a Bestie Circle');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_MEMBER_MANAGE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_MEMBER_MANAGE', 'Invite friends and manage circle member roles');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_SEND')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_SEND', 'Send text, photo, voice notes, and stickers in chat');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_READ')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_READ', 'Read chat messages and send read receipts');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'MEMORY_CREATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('MEMORY_CREATE', 'Post new Polaroid memories to the gallery');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'MEMORY_UPDATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('MEMORY_UPDATE', 'Edit Polaroid memory captions and stickers');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'MEMORY_DELETE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('MEMORY_DELETE', 'Delete Polaroid memories from the gallery');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'SECRET_NOTE_MANAGE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('SECRET_NOTE_MANAGE', 'Read, unlock, and manage secret envelope notes');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'SYSTEM_INFO_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('SYSTEM_INFO_VIEW', 'View system statistics, metrics, and audit logs');

-- Phase 4 Permissions (Profiles, Social Discovery, and Friendships)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'PROFILE_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('PROFILE_VIEW', 'View own and other users public profiles');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'PROFILE_UPDATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('PROFILE_UPDATE', 'Update own user profile details, bio, and picture');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_SEARCH')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_SEARCH', 'Search and discover users in the clubhouse');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'FRIEND_REQUEST_REJECT')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('FRIEND_REQUEST_REJECT', 'Decline or reject incoming friend requests');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'FRIEND_REQUEST_CANCEL')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('FRIEND_REQUEST_CANCEL', 'Cancel outgoing pending friend requests');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_BLOCK')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_BLOCK', 'Block other users from social interactions');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'USER_UNBLOCK')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('USER_UNBLOCK', 'Unblock previously blocked users');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'BLOCKED_USERS_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('BLOCKED_USERS_VIEW', 'View own list of blocked accounts');

-- Phase 5 Permissions (Bestie Circles & Membership Management)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_INVITE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_INVITE', 'Invite accepted bestie friends to join the circle');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_INVITATION_CANCEL')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_INVITATION_CANCEL', 'Cancel outgoing pending circle invitations');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_MEMBER_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_MEMBER_VIEW', 'View members and roster in a circle');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_MEMBER_REMOVE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_MEMBER_REMOVE', 'Remove non-owner members from a circle');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_MEMBER_ROLE_UPDATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_MEMBER_ROLE_UPDATE', 'Change roles of circle members (promote to admin or demote)');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_LEAVE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_LEAVE', 'Leave a circle voluntarily as a non-owner member');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CIRCLE_TRANSFER_OWNERSHIP')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CIRCLE_TRANSFER_OWNERSHIP', 'Transfer sole circle ownership to another active member');

-- Phase 6 Permissions (Real-Time Circle Group Chat & Moderation)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_EDIT_OWN')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_EDIT_OWN', 'Edit own chat messages in an authorized circle');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_DELETE_OWN')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_DELETE_OWN', 'Soft delete own chat messages in an authorized circle');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_MODERATE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_MODERATE', 'Moderate (soft delete/censor) any circle message as owner or admin');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_VIEW_HISTORY')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_VIEW_HISTORY', 'View paginated message history for an authorized circle');

-- Phase 7 Permissions (Multimedia Messaging, Photos, Stickers & Voice Notes)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_SEND_IMAGE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_SEND_IMAGE', 'Upload and send photo messages in circle chat');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_SEND_STICKER')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_SEND_STICKER', 'Send cute stickers in circle chat');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_SEND_VOICE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_SEND_VOICE', 'Record and send voice notes in circle chat');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_VIEW_MEDIA')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_VIEW_MEDIA', 'View and play multimedia photos and voice messages');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'CHAT_DELETE_MEDIA')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('CHAT_DELETE_MEDIA', 'Delete multimedia messages in circle chat');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'STICKER_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('STICKER_VIEW', 'Browse and view available sticker packs and categories');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'STICKER_MANAGE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('STICKER_MANAGE', 'Create, update, and manage sticker categories and sticker collections');

-- Phase 8 Permissions (Offline Messaging, Read Receipts & Notifications)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'NOTIFICATION_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('NOTIFICATION_VIEW', 'View user notifications and notification history');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'NOTIFICATION_MANAGE')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('NOTIFICATION_MANAGE', 'Mark notifications as read or dismiss notifications');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'MESSAGE_READ_RECEIPT_VIEW')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('MESSAGE_READ_RECEIPT_VIEW', 'View per-user message read receipts in circle chat');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Permissions] WHERE [PermissionCode] = 'MESSAGE_SYNC')
    INSERT INTO [dbo].[Permissions] ([PermissionCode], [Description]) VALUES ('MESSAGE_SYNC', 'Synchronize offline and missed circle messages');
GO

-- Seed Initial Sticker Categories
IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Cute')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Cute', '🌸', 1, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Love')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Love', '💖', 2, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Happy')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Happy', '🥳', 3, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Sad')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Sad', '🥺', 4, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Funny')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Funny', '😹', 5, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Miss You')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Miss You', '💌', 6, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Good Morning')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Good Morning', '☀️', 7, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Good Night')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Good Night', '🌙', 8, 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Hearts')
    INSERT INTO [dbo].[StickerCategories] ([CategoryName], [Icon], [DisplayOrder], [IsActive]) VALUES ('Hearts', '💕', 9, 1);
GO

-- Seed Built-in Stickers
DECLARE @CatCute INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Cute');
DECLARE @CatLove INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Love');
DECLARE @CatHappy INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Happy');
DECLARE @CatSad INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Sad');
DECLARE @CatFunny INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Funny');
DECLARE @CatMissYou INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Miss You');
DECLARE @CatMorning INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Good Morning');
DECLARE @CatNight INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Good Night');
DECLARE @CatHearts INT = (SELECT [CategoryId] FROM [dbo].[StickerCategories] WHERE [CategoryName] = 'Hearts');

IF @CatCute IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatCute AND [StickerName] = 'Cherry Blossom')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatCute, 'Cherry Blossom', '🌸', 1, 1);
IF @CatCute IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatCute AND [StickerName] = 'Pastel Ribbon')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatCute, 'Pastel Ribbon', '🎀', 2, 1);
IF @CatCute IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatCute AND [StickerName] = 'Strawberry Kiss')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatCute, 'Strawberry Kiss', '🍓', 3, 1);
IF @CatCute IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatCute AND [StickerName] = 'Fuzzy Teddy')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatCute, 'Fuzzy Teddy', '🧸', 4, 1);

IF @CatLove IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatLove AND [StickerName] = 'Sparkling Heart')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatLove, 'Sparkling Heart', '💖', 1, 1);
IF @CatLove IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatLove AND [StickerName] = 'Pinky Promise')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatLove, 'Pinky Promise', '🤞', 2, 1);
IF @CatLove IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatLove AND [StickerName] = 'Bouquet Hug')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatLove, 'Bouquet Hug', '💐', 3, 1);
IF @CatLove IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatLove AND [StickerName] = 'Friendship Crown')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatLove, 'Friendship Crown', '👑', 4, 1);

IF @CatHappy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHappy AND [StickerName] = 'Party Popper')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHappy, 'Party Popper', '🥳', 1, 1);
IF @CatHappy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHappy AND [StickerName] = 'Stardust Magic')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHappy, 'Stardust Magic', '✨', 2, 1);
IF @CatHappy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHappy AND [StickerName] = 'Sweet Cupcake')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHappy, 'Sweet Cupcake', '🧁', 3, 1);
IF @CatHappy IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHappy AND [StickerName] = 'Rainbow Cheer')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHappy, 'Rainbow Cheer', '🌈', 4, 1);

IF @CatSad IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatSad AND [StickerName] = 'Teary Eyes')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatSad, 'Teary Eyes', '🥺', 1, 1);
IF @CatSad IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatSad AND [StickerName] = 'Bear Hug')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatSad, 'Bear Hug', '🫂', 2, 1);
IF @CatSad IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatSad AND [StickerName] = 'Raincloud')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatSad, 'Raincloud', '🌧️', 3, 1);
IF @CatSad IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatSad AND [StickerName] = 'Bandage Mend')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatSad, 'Bandage Mend', '🩹', 4, 1);

IF @CatFunny IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatFunny AND [StickerName] = 'Laughing Cat')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatFunny, 'Laughing Cat', '😹', 1, 1);
IF @CatFunny IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatFunny AND [StickerName] = 'Chaotic Bestie')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatFunny, 'Chaotic Bestie', '🤪', 2, 1);
IF @CatFunny IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatFunny AND [StickerName] = 'Boba Gulp')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatFunny, 'Boba Gulp', '🧋', 3, 1);
IF @CatFunny IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatFunny AND [StickerName] = 'Pizza Slice')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatFunny, 'Pizza Slice', '🍕', 4, 1);

IF @CatMissYou IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMissYou AND [StickerName] = 'Letter For You')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMissYou, 'Letter For You', '💌', 1, 1);
IF @CatMissYou IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMissYou AND [StickerName] = 'Paper Plane')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMissYou, 'Paper Plane', '✈️', 2, 1);
IF @CatMissYou IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMissYou AND [StickerName] = 'Call Me')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMissYou, 'Call Me', '📱', 3, 1);
IF @CatMissYou IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMissYou AND [StickerName] = 'Hourglass Wait')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMissYou, 'Hourglass Wait', '⏳', 4, 1);

IF @CatMorning IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMorning AND [StickerName] = 'Warm Sunshine')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMorning, 'Warm Sunshine', '☀️', 1, 1);
IF @CatMorning IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMorning AND [StickerName] = 'Morning Coffee')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMorning, 'Morning Coffee', '☕', 2, 1);
IF @CatMorning IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMorning AND [StickerName] = 'Fluffy Pancakes')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMorning, 'Fluffy Pancakes', '🥞', 3, 1);
IF @CatMorning IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatMorning AND [StickerName] = 'Sunflower Smile')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatMorning, 'Sunflower Smile', '🌻', 4, 1);

IF @CatNight IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatNight AND [StickerName] = 'Crescent Moon')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatNight, 'Crescent Moon', '🌙', 1, 1);
IF @CatNight IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatNight AND [StickerName] = 'Shooting Star')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatNight, 'Shooting Star', '🌠', 2, 1);
IF @CatNight IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatNight AND [StickerName] = 'Sleepy Cloud')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatNight, 'Sleepy Cloud', '☁️', 3, 1);
IF @CatNight IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatNight AND [StickerName] = 'Cozy Candle')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatNight, 'Cozy Candle', '🕯️', 4, 1);

IF @CatHearts IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHearts AND [StickerName] = 'Pink Ribbon Heart')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHearts, 'Pink Ribbon Heart', '💝', 1, 1);
IF @CatHearts IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHearts AND [StickerName] = 'Spinning Hearts')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHearts, 'Spinning Hearts', '💞', 2, 1);
IF @CatHearts IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHearts AND [StickerName] = 'Growing Heart')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHearts, 'Growing Heart', '💗', 3, 1);
IF @CatHearts IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Stickers] WHERE [CategoryId] = @CatHearts AND [StickerName] = 'Beating Heart')
    INSERT INTO [dbo].[Stickers] ([CategoryId], [StickerName], [ImageUrl], [DisplayOrder], [IsActive]) VALUES (@CatHearts, 'Beating Heart', '💓', 4, 1);
GO

-- Seed RolePermissions (Grant ALL permissions to SuperAdmin role)
DECLARE @SuperAdminRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'SuperAdmin');
IF @SuperAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @SuperAdminRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @SuperAdminRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant Admin permissions to Admin role)
DECLARE @AdminRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'Admin');
IF @AdminRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @AdminRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'USER_VIEW', 'USER_UPDATE', 'USER_MANAGE', 'ROLE_VIEW', 'ROLE_CREATE', 'ROLE_UPDATE', 'ROLE_DELETE',
        'PERMISSION_VIEW', 'PERMISSION_ASSIGN', 'USER_ROLE_MANAGE', 'FRIEND_REQUEST_SEND', 'FRIEND_REQUEST_ACCEPT',
        'FRIEND_REMOVE', 'CIRCLE_CREATE', 'CIRCLE_VIEW', 'CIRCLE_UPDATE', 'CIRCLE_DELETE', 'CIRCLE_MEMBER_MANAGE',
        'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_MODERATE', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'CHAT_DELETE_MEDIA', 'STICKER_VIEW', 'STICKER_MANAGE',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE', 'MEMORY_UPDATE', 'MEMORY_DELETE', 'SECRET_NOTE_MANAGE', 'SYSTEM_INFO_VIEW',
        'PROFILE_VIEW', 'PROFILE_UPDATE', 'USER_SEARCH', 'FRIEND_REQUEST_REJECT', 'FRIEND_REQUEST_CANCEL', 'USER_BLOCK', 'USER_UNBLOCK', 'BLOCKED_USERS_VIEW'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @AdminRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant permissions to User role)
DECLARE @UserRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'User');
IF @UserRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @UserRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'USER_VIEW', 'USER_UPDATE', 'FRIEND_REQUEST_SEND', 'FRIEND_REQUEST_ACCEPT', 'FRIEND_REMOVE',
        'CIRCLE_CREATE', 'CIRCLE_VIEW', 'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'STICKER_VIEW',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE', 'SECRET_NOTE_MANAGE',
        'PROFILE_VIEW', 'PROFILE_UPDATE', 'USER_SEARCH', 'FRIEND_REQUEST_REJECT', 'FRIEND_REQUEST_CANCEL', 'USER_BLOCK', 'USER_UNBLOCK', 'BLOCKED_USERS_VIEW'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @UserRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant permissions to Bestie VIP role)
DECLARE @BestieRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'Bestie');
IF @BestieRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @BestieRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'USER_VIEW', 'USER_UPDATE', 'FRIEND_REQUEST_SEND', 'FRIEND_REQUEST_ACCEPT', 'FRIEND_REMOVE',
        'CIRCLE_CREATE', 'CIRCLE_VIEW', 'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'STICKER_VIEW',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE', 'SECRET_NOTE_MANAGE',
        'PROFILE_VIEW', 'PROFILE_UPDATE', 'USER_SEARCH', 'FRIEND_REQUEST_REJECT', 'FRIEND_REQUEST_CANCEL', 'USER_BLOCK', 'USER_UNBLOCK', 'BLOCKED_USERS_VIEW'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @BestieRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant permissions to Owner role)
DECLARE @OwnerRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'Owner');
IF @OwnerRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @OwnerRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'CIRCLE_VIEW', 'CIRCLE_UPDATE', 'CIRCLE_DELETE', 'CIRCLE_MEMBER_MANAGE', 'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_MODERATE', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'CHAT_DELETE_MEDIA', 'STICKER_VIEW',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @OwnerRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant permissions to Member role)
DECLARE @MemberRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'Member');
IF @MemberRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @MemberRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'CIRCLE_VIEW', 'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'STICKER_VIEW',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @MemberRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant permissions to CircleOwner role)
DECLARE @CircleOwnerRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'CircleOwner');
IF @CircleOwnerRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @CircleOwnerRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'CIRCLE_VIEW', 'CIRCLE_UPDATE', 'CIRCLE_DELETE', 'CIRCLE_INVITE', 'CIRCLE_INVITATION_CANCEL',
        'CIRCLE_MEMBER_VIEW', 'CIRCLE_MEMBER_REMOVE', 'CIRCLE_MEMBER_ROLE_UPDATE', 'CIRCLE_TRANSFER_OWNERSHIP',
        'CIRCLE_MEMBER_MANAGE', 'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_MODERATE', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'CHAT_DELETE_MEDIA', 'STICKER_VIEW',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @CircleOwnerRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant permissions to CircleAdmin role)
DECLARE @CircleAdminRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'CircleAdmin');
IF @CircleAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @CircleAdminRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'CIRCLE_VIEW', 'CIRCLE_INVITE', 'CIRCLE_INVITATION_CANCEL', 'CIRCLE_MEMBER_VIEW',
        'CIRCLE_MEMBER_REMOVE', 'CIRCLE_LEAVE', 'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_MODERATE', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'CHAT_DELETE_MEDIA', 'STICKER_VIEW',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @CircleAdminRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- Seed RolePermissions (Grant permissions to CircleMember role)
DECLARE @CircleMemberRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'CircleMember');
IF @CircleMemberRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[RolePermissions] ([RoleId], [PermissionId])
    SELECT @CircleMemberRoleId, p.[PermissionId]
    FROM [dbo].[Permissions] p
    WHERE p.[PermissionCode] IN (
        'CIRCLE_VIEW', 'CIRCLE_MEMBER_VIEW', 'CIRCLE_LEAVE', 'CHAT_SEND', 'CHAT_READ', 'CHAT_EDIT_OWN', 'CHAT_DELETE_OWN', 'CHAT_VIEW_HISTORY',
        'CHAT_SEND_IMAGE', 'CHAT_SEND_STICKER', 'CHAT_SEND_VOICE', 'CHAT_VIEW_MEDIA', 'STICKER_VIEW',
        'NOTIFICATION_VIEW', 'NOTIFICATION_MANAGE', 'MESSAGE_READ_RECEIPT_VIEW', 'MESSAGE_SYNC',
        'MEMORY_CREATE'
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[RolePermissions] rp 
        WHERE rp.[RoleId] = @CircleMemberRoleId AND rp.[PermissionId] = p.[PermissionId]
    );
END
GO

-- ==============================================================================
-- 17. RefreshTokens (Phase 2 JWT token rotation & revocation)
-- ==============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RefreshTokens')
BEGIN
    CREATE TABLE [dbo].[RefreshTokens] (
        [RefreshTokenId] INT IDENTITY(1,1) NOT NULL,
        [UserId] INT NOT NULL,
        [TokenHash] NVARCHAR(256) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_RefreshTokens_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [RevokedAt] DATETIME2 NULL,
        [ReplacedByTokenHash] NVARCHAR(256) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED ([RefreshTokenId] ASC),
        CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_RefreshTokens_TokenHash] ON [dbo].[RefreshTokens]([TokenHash] ASC);
    CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens]([UserId] ASC);
    CREATE NONCLUSTERED INDEX [IX_RefreshTokens_ExpiresAt] ON [dbo].[RefreshTokens]([ExpiresAt] ASC);
END
GO

-- ==============================================================================
-- 18. Assign Default 'User' Role to Any Unassigned Users
-- ==============================================================================
DECLARE @DefaultUserRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'User');
IF @DefaultUserRoleId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[UserRoles] ([UserId], [RoleId])
    SELECT u.[UserId], @DefaultUserRoleId
    FROM [dbo].[Users] u
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[UserRoles] ur WHERE ur.[UserId] = u.[UserId]
    );
END
GO

-- ==============================================================================
-- 19. Ensure Initial SuperAdmin Account
-- ==============================================================================
DECLARE @SuperAdminRoleId INT = (SELECT [RoleId] FROM [dbo].[Roles] WHERE [RoleName] = 'SuperAdmin');
IF @SuperAdminRoleId IS NOT NULL
BEGIN
    DECLARE @AdminTargetUserId INT = (SELECT TOP 1 [UserId] FROM [dbo].[Users] WHERE [RoleKey] = 'Me' OR [Username] = 'mochi_maker');
    IF @AdminTargetUserId IS NULL
        SET @AdminTargetUserId = (SELECT TOP 1 [UserId] FROM [dbo].[Users] ORDER BY [UserId] ASC);

    IF @AdminTargetUserId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[UserRoles] WHERE [UserId] = @AdminTargetUserId AND [RoleId] = @SuperAdminRoleId)
    BEGIN
        INSERT INTO [dbo].[UserRoles] ([UserId], [RoleId]) VALUES (@AdminTargetUserId, @SuperAdminRoleId);
    END
END
GO


PRINT '==============================================================================';
PRINT ' BestieSpaceDb has been successfully created and configured in SSMS! ';
PRINT ' Refresh your Object Explorer (press F5 on Databases) to view BestieSpaceDb. ';
PRINT '==============================================================================';
GO

SELECT 
    t.name AS [TableName], 
    SUM(p.rows) AS [TotalRows]
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name;
GO
