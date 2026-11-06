-- BestieSpace Initial Database Schema (Database-First)

CREATE TABLE IF NOT EXISTS AppStats (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MissYouCount INTEGER NOT NULL DEFAULT 1000,
    MainLetterTitle TEXT NOT NULL,
    MainLetterContent TEXT NOT NULL,
    SenderNickname TEXT NOT NULL,
    RecipientNickname TEXT NOT NULL,
    LastUpdated TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Memories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Caption TEXT NOT NULL,
    ImageUrl TEXT NOT NULL,
    MemoryDate TEXT NOT NULL,
    StickerType TEXT NOT NULL,
    RotationAngle REAL NOT NULL DEFAULT 0,
    DisplayOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS SecretNotes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    PreviewHint TEXT NOT NULL,
    Content TEXT NOT NULL,
    StickerIcon TEXT NOT NULL,
    IsUnlocked INTEGER NOT NULL DEFAULT 0,
    UnlockedAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS ChatMessages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SenderName TEXT NOT NULL,
    MessageType TEXT NOT NULL,
    Content TEXT NULL,
    MediaUrl TEXT NULL,
    VoiceDurationSeconds REAL NULL,
    Timestamp TEXT NOT NULL,
    IsRead INTEGER NOT NULL DEFAULT 0,
    ReadAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,
    Password TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    RoleKey TEXT NOT NULL,
    AvatarSticker TEXT NOT NULL,
    PasswordHint TEXT NULL,
    QuickPin TEXT NOT NULL DEFAULT '3000',
    IsOnline INTEGER NOT NULL DEFAULT 0,
    LastSeenAt TEXT NULL,
    CreatedAt TEXT NOT NULL,
    LastLoginAt TEXT NULL
);
