using System;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Infrastructure.Data;

/// <summary>
/// Safe one-time SQLite-to-SQL Server data importer for BestieSpace Phase 1.
/// - Never deletes or modifies original SQLite database (opened strictly in ReadOnly mode).
/// - Preserves exact primary key IDs using SQL Server IDENTITY_INSERT.
/// - Prevents duplicate imports by checking existing records per table.
/// - Safely handles nullable values, text-to-datetime conversions, and schema differences.
/// </summary>
public class SqliteToSqlServerDataMigrator
{
    private readonly BestieDbContext _context;
    private readonly ILogger<SqliteToSqlServerDataMigrator> _logger;

    public SqliteToSqlServerDataMigrator(BestieDbContext context, ILogger<SqliteToSqlServerDataMigrator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task MigrateIfSourceExistsAsync(string sqliteFilePath)
    {
        if (!File.Exists(sqliteFilePath))
        {
            _logger.LogInformation("No existing SQLite database found at '{Path}'. Skipping SQLite import.", sqliteFilePath);
            return;
        }

        var fileInfo = new FileInfo(sqliteFilePath);
        if (fileInfo.Length == 0)
        {
            _logger.LogInformation("SQLite database at '{Path}' is empty. Skipping import.", sqliteFilePath);
            return;
        }

        _logger.LogInformation("Found existing SQLite database at '{Path}' ({Bytes} bytes). Beginning migration to SQL Server...", 
            sqliteFilePath, fileInfo.Length);

        var sqliteConnString = new SqliteConnectionStringBuilder
        {
            DataSource = sqliteFilePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var sqliteConn = new SqliteConnection(sqliteConnString);
        await sqliteConn.OpenAsync();

        // 1. Migrate AppStats
        await MigrateAppStatsAsync(sqliteConn);

        // 2. Migrate Users
        await MigrateUsersAsync(sqliteConn);

        // 3. Migrate Memories
        await MigrateMemoriesAsync(sqliteConn);

        // 4. Migrate SecretNotes
        await MigrateSecretNotesAsync(sqliteConn);

        // 5. Migrate ChatMessages
        await MigrateChatMessagesAsync(sqliteConn);

        _logger.LogInformation("SQLite-to-SQL Server data migration completed successfully without modifying the source SQLite database.");
    }

    private async Task MigrateAppStatsAsync(SqliteConnection sqliteConn)
    {
        if (await _context.AppStats.AnyAsync())
        {
            _logger.LogInformation("SQL Server 'AppStats' table already contains data. Skipping AppStats import.");
            return;
        }

        if (!await TableExistsInSqliteAsync(sqliteConn, "AppStats")) return;

        using var cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "SELECT Id, MissYouCount, MainLetterTitle, MainLetterContent, SenderNickname, RecipientNickname, LastUpdated FROM AppStats";

        using var reader = await cmd.ExecuteReaderAsync();
        var recordsCount = 0;

        await _context.Database.OpenConnectionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[AppStats] ON;");

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var missYouCount = reader.GetInt32(1);
                var title = reader.GetString(2);
                var content = reader.GetString(3);
                var senderNick = reader.GetString(4);
                var recipientNick = reader.GetString(5);
                var lastUpdated = ParseDateTimeSafe(reader.GetValue(6));

                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [dbo].[AppStats] ([Id], [MissYouCount], [MainLetterTitle], [MainLetterContent], [SenderNickname], [RecipientNickname], [LastUpdated]) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                    id, missYouCount, title, content, senderNick, recipientNick, lastUpdated);

                recordsCount++;
            }
        }
        finally
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[AppStats] OFF;");
            }
            catch { }
            await _context.Database.CloseConnectionAsync();
        }

        _logger.LogInformation("Imported {Count} AppStats record(s) into SQL Server.", recordsCount);
    }

    private async Task MigrateUsersAsync(SqliteConnection sqliteConn)
    {
        if (await _context.Users.AnyAsync())
        {
            _logger.LogInformation("SQL Server 'Users' table already contains data. Skipping Users import.");
            return;
        }

        if (!await TableExistsInSqliteAsync(sqliteConn, "Users")) return;

        using var cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "SELECT Id, Username, Password, DisplayName, RoleKey, AvatarSticker, PasswordHint, QuickPin, IsOnline, LastSeenAt, CreatedAt, LastLoginAt FROM Users";

        using var reader = await cmd.ExecuteReaderAsync();
        var recordsCount = 0;

        await _context.Database.OpenConnectionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Users] ON;");

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var username = reader.GetString(1);
                var password = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var displayName = reader.IsDBNull(3) ? username : reader.GetString(3);
                var roleKey = reader.IsDBNull(4) ? "User" : reader.GetString(4);
                var avatar = reader.IsDBNull(5) ? "🌸" : reader.GetString(5);
                var hint = reader.IsDBNull(6) ? null : reader.GetString(6);
                var quickPin = reader.IsDBNull(7) ? "3000" : reader.GetString(7);
                var isOnline = !reader.IsDBNull(8) && (reader.GetInt32(8) == 1);
                var lastSeen = reader.IsDBNull(9) ? (DateTime?)null : ParseDateTimeSafe(reader.GetValue(9));
                var createdAt = reader.IsDBNull(10) ? DateTime.UtcNow : ParseDateTimeSafe(reader.GetValue(10));
                var lastLogin = reader.IsDBNull(11) ? (DateTime?)null : ParseDateTimeSafe(reader.GetValue(11));

                var normalizedUsername = username.ToUpperInvariant();
                var email = $"{username.ToLowerInvariant()}@bestiespace.local";
                var normalizedEmail = email.ToUpperInvariant();
                var passwordHash = password; // Preserve existing password / hash

                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [dbo].[Users] ([UserId], [Username], [NormalizedUsername], [Email], [NormalizedEmail], [PasswordHash], [CreatedAt], [UpdatedAt], [IsActive], " +
                    "[Password], [DisplayName], [RoleKey], [AvatarSticker], [PasswordHint], [QuickPin], [IsOnline], [LastSeenAt], [LastLoginAt]) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, NULL, 1, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15});",
                    new object[] { id, username, normalizedUsername, email, normalizedEmail, passwordHash, createdAt,
                    password, displayName, roleKey, avatar, (object?)hint ?? DBNull.Value, quickPin, isOnline, (object?)lastSeen ?? DBNull.Value, (object?)lastLogin ?? DBNull.Value });

                recordsCount++;
            }
        }
        finally
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Users] OFF;");
            }
            catch { }
            await _context.Database.CloseConnectionAsync();
        }

        _logger.LogInformation("Imported {Count} User record(s) into SQL Server.", recordsCount);
    }

    private async Task MigrateMemoriesAsync(SqliteConnection sqliteConn)
    {
        if (await _context.Memories.AnyAsync())
        {
            _logger.LogInformation("SQL Server 'Memories' table already contains data. Skipping Memories import.");
            return;
        }

        if (!await TableExistsInSqliteAsync(sqliteConn, "Memories")) return;

        using var cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "SELECT Id, Caption, ImageUrl, MemoryDate, StickerType, RotationAngle, DisplayOrder, CreatedAt FROM Memories";

        using var reader = await cmd.ExecuteReaderAsync();
        var recordsCount = 0;

        await _context.Database.OpenConnectionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Memories] ON;");

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var caption = reader.GetString(1);
                var imageUrl = reader.GetString(2);
                var memDate = ParseDateTimeSafe(reader.GetValue(3));
                var sticker = reader.IsDBNull(4) ? "heart" : reader.GetString(4);
                var angle = reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5);
                var order = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                var createdAt = reader.IsDBNull(7) ? DateTime.UtcNow : ParseDateTimeSafe(reader.GetValue(7));

                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [dbo].[Memories] ([Id], [Caption], [ImageUrl], [MemoryDate], [StickerType], [RotationAngle], [DisplayOrder], [CreatedAt]) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7});",
                    id, caption, imageUrl, memDate, sticker, angle, order, createdAt);

                recordsCount++;
            }
        }
        finally
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Memories] OFF;");
            }
            catch { }
            await _context.Database.CloseConnectionAsync();
        }

        _logger.LogInformation("Imported {Count} Memory record(s) into SQL Server.", recordsCount);
    }

    private async Task MigrateSecretNotesAsync(SqliteConnection sqliteConn)
    {
        if (await _context.SecretNotes.AnyAsync())
        {
            _logger.LogInformation("SQL Server 'SecretNotes' table already contains data. Skipping SecretNotes import.");
            return;
        }

        if (!await TableExistsInSqliteAsync(sqliteConn, "SecretNotes")) return;

        using var cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "SELECT Id, Title, PreviewHint, Content, StickerIcon, IsUnlocked, UnlockedAt FROM SecretNotes";

        using var reader = await cmd.ExecuteReaderAsync();
        var recordsCount = 0;

        await _context.Database.OpenConnectionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[SecretNotes] ON;");

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var title = reader.GetString(1);
                var hint = reader.GetString(2);
                var content = reader.GetString(3);
                var sticker = reader.IsDBNull(4) ? "💌" : reader.GetString(4);
                var isUnlocked = !reader.IsDBNull(5) && (reader.GetInt32(5) == 1);
                var unlockedAt = reader.IsDBNull(6) ? (DateTime?)null : ParseDateTimeSafe(reader.GetValue(6));

                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [dbo].[SecretNotes] ([Id], [Title], [PreviewHint], [Content], [StickerIcon], [IsUnlocked], [UnlockedAt]) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                    new object[] { id, title, hint, content, sticker, isUnlocked, (object?)unlockedAt ?? DBNull.Value });

                recordsCount++;
            }
        }
        finally
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[SecretNotes] OFF;");
            }
            catch { }
            await _context.Database.CloseConnectionAsync();
        }

        _logger.LogInformation("Imported {Count} SecretNote record(s) into SQL Server.", recordsCount);
    }

    private async Task MigrateChatMessagesAsync(SqliteConnection sqliteConn)
    {
        if (await _context.ChatMessages.AnyAsync())
        {
            _logger.LogInformation("SQL Server 'ChatMessages' table already contains data. Skipping ChatMessages import.");
            return;
        }

        if (!await TableExistsInSqliteAsync(sqliteConn, "ChatMessages")) return;

        using var cmd = sqliteConn.CreateCommand();
        cmd.CommandText = "SELECT Id, SenderName, MessageType, Content, MediaUrl, VoiceDurationSeconds, Timestamp, IsRead, ReadAt FROM ChatMessages";

        using var reader = await cmd.ExecuteReaderAsync();
        var recordsCount = 0;

        await _context.Database.OpenConnectionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[ChatMessages] ON;");

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var senderName = reader.GetString(1);
                var msgType = reader.GetString(2);
                var content = reader.IsDBNull(3) ? null : reader.GetString(3);
                var mediaUrl = reader.IsDBNull(4) ? null : reader.GetString(4);
                var voiceDuration = reader.IsDBNull(5) ? (double?)null : reader.GetDouble(5);
                var timestamp = ParseDateTimeSafe(reader.GetValue(6));
                var isRead = !reader.IsDBNull(7) && (reader.GetInt32(7) == 1);
                var readAt = reader.IsDBNull(8) ? (DateTime?)null : ParseDateTimeSafe(reader.GetValue(8));

                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO [dbo].[ChatMessages] ([Id], [CircleId], [SenderId], [SenderName], [MessageType], [Content], [MediaUrl], [VoiceDurationSeconds], [Timestamp], [IsRead], [ReadAt]) " +
                    "VALUES ({0}, NULL, NULL, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8});",
                    new object[] { id, senderName, msgType, (object?)content ?? DBNull.Value, (object?)mediaUrl ?? DBNull.Value, (object?)voiceDuration ?? DBNull.Value, timestamp, isRead, (object?)readAt ?? DBNull.Value });

                recordsCount++;
            }
        }
        finally
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[ChatMessages] OFF;");
            }
            catch { }
            await _context.Database.CloseConnectionAsync();
        }

        _logger.LogInformation("Imported {Count} ChatMessage record(s) into SQL Server.", recordsCount);
    }

    private static async Task<bool> TableExistsInSqliteAsync(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@table";
        cmd.Parameters.AddWithValue("@table", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static DateTime ParseDateTimeSafe(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return DateTime.UtcNow;
        }

        if (value is DateTime dt)
        {
            return dt;
        }

        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str))
        {
            return DateTime.UtcNow;
        }

        if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(str, out parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }
}
