using BestieSpace.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BestieSpace.Api.Infrastructure.Data;

public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, IWebHostEnvironment environment)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BestieDbContext>();
        var logger = scope.ServiceProvider.GetService<ILogger<BestieDbContext>>();

        // Ensure directories exist
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var memoriesDir = Path.Combine(webRoot, "uploads", "memories");
        var chatPhotosDir = Path.Combine(webRoot, "uploads", "chat-photos");
        var chatVoicesDir = Path.Combine(webRoot, "uploads", "chat-voices");
        var sampleMemoriesDir = Path.Combine(webRoot, "images", "sample-memories");

        Directory.CreateDirectory(memoriesDir);
        Directory.CreateDirectory(chatPhotosDir);
        Directory.CreateDirectory(chatVoicesDir);
        Directory.CreateDirectory(sampleMemoriesDir);

        // Ensure SVG sample images exist
        EnsureSampleMemoryImages(sampleMemoriesDir);

        // Ensure sample voice note audio exists
        EnsureSampleVoiceAudio(chatVoicesDir);

        // 1. Ensure SQL Server database and tables are created
        try
        {
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "context.Database.EnsureCreatedAsync encountered an issue. Attempting script initialization...");
        }

        var initSqlPath = Path.Combine(environment.ContentRootPath, "Infrastructure", "Data", "init.sql");
        if (File.Exists(initSqlPath))
        {
            try
            {
                var fullScript = await File.ReadAllTextAsync(initSqlPath);
                // Split SQL Server batches on GO separator
                var batches = System.Text.RegularExpressions.Regex.Split(
                    fullScript, 
                    @"^\s*GO\s*$", 
                    System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                await context.Database.OpenConnectionAsync();
                try
                {
                    using var command = context.Database.GetDbConnection().CreateCommand();
                    foreach (var batch in batches)
                    {
                        var trimmed = batch.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;
                        command.CommandText = trimmed;
                        await command.ExecuteNonQueryAsync();
                    }
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
                logger?.LogInformation("Successfully executed init.sql schema and seed batches against SQL Server.");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to execute init.sql batches directly against SQL Server.");
            }
        }

        // 2. Safe one-time SQLite-to-SQL Server data import
        try
        {
            var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
            var sqliteCandidate1 = !string.IsNullOrWhiteSpace(dataDir) ? Path.Combine(dataDir, "bestiespace.db") : null;
            var sqliteCandidate2 = Path.Combine(environment.ContentRootPath, "bestiespace.db");
            var sqliteDbPath = (sqliteCandidate1 != null && File.Exists(sqliteCandidate1))
                ? sqliteCandidate1
                : sqliteCandidate2;

            if (File.Exists(sqliteDbPath))
            {
                var migrator = scope.ServiceProvider.GetRequiredService<SqliteToSqlServerDataMigrator>();
                await migrator.MigrateIfSourceExistsAsync(sqliteDbPath);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error occurred during SQLite-to-SQL Server data migration.");
        }

        // 3. Seed initial heartfelt data & system roles/permissions if not yet populated
        await SeedDataAsync(context);
    }

    private static async Task SeedDataAsync(BestieDbContext context)
    {
        // Seed AppStats if not present
        if (!await context.AppStats.AnyAsync())
        {
            var appStat = new AppStat
            {
                MissYouCount = 1000,
                MainLetterTitle = "To My Favorite Human in the Entire World 🌸",
                MainLetterContent = "Dear Bestie,\n\nI built this cozy, pastel corner of the internet just for us! No matter how many miles stand between us or how crazy life gets, you will always be my safest place, my funniest laugh, and my favorite sister at heart.\n\nEvery time you miss me, press that little pink heart button below so I can feel your virtual hug! Browse through our polaroid memories, open the mystery envelopes when you need an instant smile, and send me silly stickers, photos, and voice notes anytime in our chat.\n\nI miss you more than words, boba cups, and stars in the sky combined. Thank you for being my constant sunshine! 💖✨",
                SenderNickname = "Me 🎀",
                RecipientNickname = "Bestie 🌸",
                LastUpdated = DateTime.UtcNow
            };
            context.AppStats.Add(appStat);
            await context.SaveChangesAsync();
        }

        // Seed 4 cute sample Polaroid memories
        if (!await context.Memories.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var sampleMemories = new List<Memory>
            {
                new()
                {
                    Caption = "Spontaneous late-night boba run & uncontrollable laughing fit! 🧋✨",
                    ImageUrl = "/images/sample-memories/boba-run.svg",
                    MemoryDate = now.AddMonths(-3),
                    StickerType = "bow",
                    RotationAngle = -2.5,
                    DisplayOrder = 1,
                    CreatedAt = now.AddMonths(-3)
                },
                new()
                {
                    Caption = "Golden hour picnic in the park with homemade cookies & cute outfits 🌸🧺",
                    ImageUrl = "/images/sample-memories/picnic-day.svg",
                    MemoryDate = now.AddMonths(-2),
                    StickerType = "sparkle",
                    RotationAngle = 2.0,
                    DisplayOrder = 2,
                    CreatedAt = now.AddMonths(-2)
                },
                new()
                {
                    Caption = "Crying-laughing photobooth session trying on silly oversized glasses 🍓📸",
                    ImageUrl = "/images/sample-memories/photobooth.svg",
                    MemoryDate = now.AddMonths(-1),
                    StickerType = "bear",
                    RotationAngle = -1.8,
                    DisplayOrder = 3,
                    CreatedAt = now.AddMonths(-1)
                },
                new()
                {
                    Caption = "Watching the pastel sunset by the beach making 10-year pinky promises 🌅💖",
                    ImageUrl = "/images/sample-memories/sunset-promise.svg",
                    MemoryDate = now.AddDays(-14),
                    StickerType = "heart",
                    RotationAngle = 2.4,
                    DisplayOrder = 4,
                    CreatedAt = now.AddDays(-14)
                }
            };

            context.Memories.AddRange(sampleMemories);
            await context.SaveChangesAsync();
        }

        // Seed 4 secret envelope notes
        if (!await context.SecretNotes.AnyAsync())
        {
            var sampleNotes = new List<SecretNote>
            {
                new()
                {
                    Title = "Open when you miss me 💌",
                    PreviewHint = "A warm, gentle reminder when you feel a little lonely...",
                    Content = "Close your eyes, take a deep breath, and wrap your arms tight around yourself. That is a giant, warm bear hug from me flying across the miles! Remember all those late-night FaceTime calls where we laughed until we couldn't breathe? I am always just one text or call away. You are never alone, bestie! 🧸💖",
                    StickerIcon = "💌",
                    IsUnlocked = false,
                    UnlockedAt = null
                },
                new()
                {
                    Title = "My absolute favorite memory of us 🌟",
                    PreviewHint = "That unforgettable day we couldn't stop laughing...",
                    Content = "Do you remember that sudden rainy afternoon when we got completely drenched running for cover, burst into that tiny aesthetic bakery shivering, ordered huge marshmallow hot chocolates, and laughed until tears streamed down our faces? I replay that memory in my mind whenever I need instant happiness. You make ordinary days pure magic! ☕✨",
                    StickerIcon = "🌟",
                    IsUnlocked = false,
                    UnlockedAt = null
                },
                new()
                {
                    Title = "10 reasons why you're my bestie 🌸",
                    PreviewHint = "A quick list of why nobody could ever replace you...",
                    Content = "1. You know exactly what I'm thinking with just one eye contact.\n2. Your laugh is completely contagious.\n3. You hype me up even on my worst hair days.\n4. You never judge my weird food combinations.\n5. You always save the best bite for me.\n6. You send the most chaotic memes at 2 AM.\n7. You listen with your entire heart.\n8. You give the warmest, safest hugs.\n9. You believe in my dreams even when I doubt myself.\n10. Simply because you are YOU, and there will never be anyone like you! 🎀👑",
                    StickerIcon = "🌸",
                    IsUnlocked = false,
                    UnlockedAt = null
                },
                new()
                {
                    Title = "A pinky promise to you 🤞",
                    PreviewHint = "A lifetime promise sealed with love and sprinkles...",
                    Content = "I promise to celebrate every single one of your victories like they are my own. I promise to sit quietly beside you whenever life feels heavy. And I promise that when we are old grandmas with matching knitted cardigans, we will still be sipping tea and gossiping! No distance, time, or busy schedule will ever change our bond. Pinky swear forever and ever! 👭💕",
                    StickerIcon = "🤞",
                    IsUnlocked = false,
                    UnlockedAt = null
                }
            };

            context.SecretNotes.AddRange(sampleNotes);
            await context.SaveChangesAsync();
        }

        // Seed chat messages (including 2 already read welcome messages and 4 unread messages for pop-up reveal)
        var hasUnreadSample = await context.ChatMessages.AnyAsync(m => !m.IsRead && m.SenderName == "Me 🎀");
        if (!hasUnreadSample)
        {
            var now = DateTime.UtcNow;

            // Make sure already existing welcome messages are marked read
            var existing = await context.ChatMessages.ToListAsync();
            foreach (var m in existing)
            {
                m.IsRead = true;
                m.ReadAt ??= now.AddHours(-1);
            }

            if (!existing.Any())
            {
                context.ChatMessages.AddRange(
                    new ChatMessage
                    {
                        SenderName = "Me 🎀",
                        MessageType = "text",
                        Content = "Hi sweet bestie! Welcome to our secret digital clubhouse! 🌸 I built this so we always have a special cute place together.",
                        Timestamp = now.AddMinutes(-30),
                        IsRead = true,
                        ReadAt = now.AddMinutes(-20)
                    },
                    new ChatMessage
                    {
                        SenderName = "Bestie 🌸",
                        MessageType = "sticker",
                        Content = "hugging-bear",
                        Timestamp = now.AddMinutes(-25),
                        IsRead = true,
                        ReadAt = now.AddMinutes(-20)
                    }
                );
            }

            // Seed 4 cute unread messages from "Me 🎀" waiting for Bestie to open the app!
            var unreadOfflineMessages = new List<ChatMessage>
            {
                new()
                {
                    SenderName = "Me 🎀",
                    MessageType = "text",
                    Content = "Good morning sunshine! ☀️ I woke up today thinking about our late night laughs and missed you a ton! Hope your day is filled with sweet smiles 🌸",
                    Timestamp = now.AddMinutes(-12),
                    IsRead = false,
                    ReadAt = null
                },
                new()
                {
                    SenderName = "Me 🎀",
                    MessageType = "sticker",
                    Content = "sparkling-heart",
                    Timestamp = now.AddMinutes(-10),
                    IsRead = false,
                    ReadAt = null
                },
                new()
                {
                    SenderName = "Me 🎀",
                    MessageType = "photo",
                    Content = "I found this aesthetic strawberry boba spot for us! We HAVE to go this weekend 🍓🧋",
                    MediaUrl = "/images/sample-memories/boba-run.svg",
                    Timestamp = now.AddMinutes(-7),
                    IsRead = false,
                    ReadAt = null
                },
                new()
                {
                    SenderName = "Me 🎀",
                    MessageType = "voice",
                    Content = "A little voice hug for you 🎤",
                    MediaUrl = "/uploads/chat-voices/sweet-voice-note.wav",
                    VoiceDurationSeconds = 5,
                    Timestamp = now.AddMinutes(-3),
                    IsRead = false,
                    ReadAt = null
                }
            };

            context.ChatMessages.AddRange(unreadOfflineMessages);
            await context.SaveChangesAsync();
        }

        // Seed or update Users
        if (!await context.Users.AnyAsync())
        {
            var seedUsers = new List<User>
            {
                new()
                {
                    Username = "strawberry_bestie",
                    NormalizedUsername = "STRAWBERRY_BESTIE",
                    Email = "strawberry_bestie@bestiespace.local",
                    NormalizedEmail = "STRAWBERRY_BESTIE@BESTIESPACE.LOCAL",
                    PasswordHash = "missyou3000!",
                    Password = "missyou3000!",
                    DisplayName = "Sweet Bestie 🌸",
                    RoleKey = "Bestie",
                    AvatarSticker = "🌸",
                    PasswordHint = "How much I miss you (missyou3000!)",
                    QuickPin = "3000",
                    IsOnline = false,
                    LastSeenAt = DateTime.UtcNow.AddMinutes(-15),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                new()
                {
                    Username = "mochi_maker",
                    NormalizedUsername = "MOCHI_MAKER",
                    Email = "mochi_maker@bestiespace.local",
                    NormalizedEmail = "MOCHI_MAKER@BESTIESPACE.LOCAL",
                    PasswordHash = "bestie4ever!",
                    Password = "bestie4ever!",
                    DisplayName = "Your Bestie 🎀",
                    RoleKey = "Me",
                    AvatarSticker = "🎀",
                    PasswordHint = "Friends forever (bestie4ever!)",
                    QuickPin = "4040",
                    IsOnline = false,
                    LastSeenAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                }
            };

            context.Users.AddRange(seedUsers);
            await context.SaveChangesAsync();
        }
        else
        {
            // Ensure QuickPin, Normalized fields are populated on existing rows
            var users = await context.Users.ToListAsync();
            foreach (var user in users)
            {
                if (string.IsNullOrWhiteSpace(user.NormalizedUsername))
                    user.NormalizedUsername = user.Username.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(user.Email))
                    user.Email = $"{user.Username.ToLowerInvariant()}@bestiespace.local";
                if (string.IsNullOrWhiteSpace(user.NormalizedEmail) && user.Email != null)
                    user.NormalizedEmail = user.Email.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(user.PasswordHash))
                    user.PasswordHash = user.Password;
            }

            var bestie = users.FirstOrDefault(u => u.RoleKey == "Bestie");
            if (bestie != null)
            {
                bestie.QuickPin = "3000";
                bestie.LastSeenAt ??= DateTime.UtcNow.AddMinutes(-15);
            }
            var me = users.FirstOrDefault(u => u.RoleKey == "Me");
            if (me != null)
            {
                me.QuickPin = "4040";
                me.LastSeenAt ??= DateTime.UtcNow;
            }
            await context.SaveChangesAsync();
        }

        // Seed default system roles if not present
        var existingRoleNames = await context.Roles.Select(r => r.RoleName).ToListAsync();
        var rolesToSeed = new List<Role>
        {
            new() { RoleName = "SuperAdmin", Description = "Supreme administrator with full control over all system operations, roles, and staff", IsSystemRole = true },
            new() { RoleName = "Admin", Description = "System administrator capable of managing users, roles, and permissions", IsSystemRole = true },
            new() { RoleName = "User", Description = "Default registered platform user with personal profile and social features", IsSystemRole = true },
            new() { RoleName = "Bestie", Description = "VIP Bestie role with full access to private clubhouse features", IsSystemRole = true },
            new() { RoleName = "Owner", Description = "Owner and creator of a Bestie Circle with circle administration rights", IsSystemRole = false },
            new() { RoleName = "Member", Description = "Regular member of a Bestie Circle with chat and memory sharing rights", IsSystemRole = false },
            new() { RoleName = "CircleOwner", Description = "Sole owner and administrator of a Bestie Circle with full privileges", IsSystemRole = false },
            new() { RoleName = "CircleAdmin", Description = "Circle administrator capable of managing invitations and ordinary members", IsSystemRole = false },
            new() { RoleName = "CircleMember", Description = "Active circle member with viewing, chat, and departure privileges", IsSystemRole = false }
        };

        foreach (var r in rolesToSeed)
        {
            if (!existingRoleNames.Contains(r.RoleName))
            {
                context.Roles.Add(r);
            }
        }
        await context.SaveChangesAsync();

        // Seed dynamic permissions catalog
        var existingPermCodes = await context.Permissions.Select(p => p.PermissionCode).ToListAsync();
        var permsToSeed = new List<Permission>
        {
            new() { PermissionCode = "USER_VIEW", Description = "View user profiles and list platform members" },
            new() { PermissionCode = "USER_CREATE", Description = "Register or create new user accounts" },
            new() { PermissionCode = "USER_UPDATE", Description = "Update user profiles and account details" },
            new() { PermissionCode = "USER_DELETE", Description = "Delete or permanently remove user accounts" },
            new() { PermissionCode = "USER_MANAGE", Description = "Full user administration, status activation, and account management" },
            new() { PermissionCode = "ROLE_VIEW", Description = "View system and custom security roles" },
            new() { PermissionCode = "ROLE_CREATE", Description = "Create new security and circle roles" },
            new() { PermissionCode = "ROLE_UPDATE", Description = "Modify role descriptions and names" },
            new() { PermissionCode = "ROLE_DELETE", Description = "Delete custom non-system roles" },
            new() { PermissionCode = "PERMISSION_VIEW", Description = "View the global permission catalog and matrix" },
            new() { PermissionCode = "PERMISSION_ASSIGN", Description = "Assign or revoke permissions from security roles" },
            new() { PermissionCode = "USER_ROLE_MANAGE", Description = "Assign or revoke roles from user accounts" },
            new() { PermissionCode = "FRIEND_REQUEST_SEND", Description = "Send friend requests to other users" },
            new() { PermissionCode = "FRIEND_REQUEST_ACCEPT", Description = "Accept or decline received friend requests" },
            new() { PermissionCode = "FRIEND_REMOVE", Description = "Remove friends or block users" },
            new() { PermissionCode = "CIRCLE_CREATE", Description = "Create new Bestie Circles (clubs)" },
            new() { PermissionCode = "CIRCLE_VIEW", Description = "View circle details, member roster, and activities" },
            new() { PermissionCode = "CIRCLE_UPDATE", Description = "Update circle name, description, and settings" },
            new() { PermissionCode = "CIRCLE_DELETE", Description = "Disband or delete a Bestie Circle" },
            new() { PermissionCode = "CIRCLE_MEMBER_MANAGE", Description = "Invite friends and manage circle member roles" },
            new() { PermissionCode = "CHAT_SEND", Description = "Send text, photo, voice notes, and stickers in chat" },
            new() { PermissionCode = "CHAT_READ", Description = "Read chat messages and send read receipts" },
            new() { PermissionCode = "MEMORY_CREATE", Description = "Post new Polaroid memories to the gallery" },
            new() { PermissionCode = "MEMORY_UPDATE", Description = "Edit Polaroid memory captions and stickers" },
            new() { PermissionCode = "MEMORY_DELETE", Description = "Delete Polaroid memories from the gallery" },
            new() { PermissionCode = "SECRET_NOTE_MANAGE", Description = "Read, unlock, and manage secret envelope notes" },
            new() { PermissionCode = "SYSTEM_INFO_VIEW", Description = "View system statistics, metrics, and audit logs" },
            new() { PermissionCode = "PROFILE_VIEW", Description = "View own and other users public profiles" },
            new() { PermissionCode = "PROFILE_UPDATE", Description = "Update own user profile details, bio, and picture" },
            new() { PermissionCode = "USER_SEARCH", Description = "Search and discover users in the clubhouse" },
            new() { PermissionCode = "FRIEND_REQUEST_REJECT", Description = "Decline or reject incoming friend requests" },
            new() { PermissionCode = "FRIEND_REQUEST_CANCEL", Description = "Cancel outgoing pending friend requests" },
            new() { PermissionCode = "USER_BLOCK", Description = "Block other users from social interactions" },
            new() { PermissionCode = "USER_UNBLOCK", Description = "Unblock previously blocked users" },
            new() { PermissionCode = "BLOCKED_USERS_VIEW", Description = "View own list of blocked accounts" },
            new() { PermissionCode = "CIRCLE_INVITE", Description = "Invite accepted bestie friends to join the circle" },
            new() { PermissionCode = "CIRCLE_INVITATION_CANCEL", Description = "Cancel outgoing pending circle invitations" },
            new() { PermissionCode = "CIRCLE_MEMBER_VIEW", Description = "View members and roster in a circle" },
            new() { PermissionCode = "CIRCLE_MEMBER_REMOVE", Description = "Remove non-owner members from a circle" },
            new() { PermissionCode = "CIRCLE_MEMBER_ROLE_UPDATE", Description = "Change roles of circle members (promote to admin or demote)" },
            new() { PermissionCode = "CIRCLE_LEAVE", Description = "Leave a circle voluntarily as a non-owner member" },
            new() { PermissionCode = "CIRCLE_TRANSFER_OWNERSHIP", Description = "Transfer sole circle ownership to another active member" },
            new() { PermissionCode = "CHAT_EDIT_OWN", Description = "Edit own chat messages in an authorized circle" },
            new() { PermissionCode = "CHAT_DELETE_OWN", Description = "Soft delete own chat messages in an authorized circle" },
            new() { PermissionCode = "CHAT_MODERATE", Description = "Moderate (soft delete/censor) any circle message as owner or admin" },
            new() { PermissionCode = "CHAT_VIEW_HISTORY", Description = "View paginated message history for an authorized circle" },
            new() { PermissionCode = "CHAT_SEND_IMAGE", Description = "Upload and send photo messages in circle chat" },
            new() { PermissionCode = "CHAT_SEND_STICKER", Description = "Send cute stickers in circle chat" },
            new() { PermissionCode = "CHAT_SEND_VOICE", Description = "Record and send voice notes in circle chat" },
            new() { PermissionCode = "CHAT_VIEW_MEDIA", Description = "View and play multimedia photos and voice messages" },
            new() { PermissionCode = "CHAT_DELETE_MEDIA", Description = "Delete multimedia messages in circle chat" },
            new() { PermissionCode = "STICKER_VIEW", Description = "Browse and view available sticker packs and categories" },
            new() { PermissionCode = "STICKER_MANAGE", Description = "Create, update, and manage sticker categories and sticker collections" }
        };

        foreach (var p in permsToSeed)
        {
            if (!existingPermCodes.Contains(p.PermissionCode))
            {
                context.Permissions.Add(p);
            }
        }
        await context.SaveChangesAsync();

        // Seed SuperAdmin RolePermissions (All permissions)
        var allPerms = await context.Permissions.ToListAsync();
        var superAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "SuperAdmin");
        if (superAdminRole != null)
        {
            var existingSuperPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == superAdminRole.RoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPerms)
            {
                if (!existingSuperPermIds.Contains(perm.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = superAdminRole.RoleId, PermissionId = perm.PermissionId });
                }
            }
        }

        // Seed Admin RolePermissions
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
        if (adminRole != null)
        {
            var adminCodes = new HashSet<string>
            {
                "USER_VIEW", "USER_UPDATE", "USER_MANAGE", "ROLE_VIEW", "ROLE_CREATE", "ROLE_UPDATE", "ROLE_DELETE",
                "PERMISSION_VIEW", "PERMISSION_ASSIGN", "USER_ROLE_MANAGE", "FRIEND_REQUEST_SEND", "FRIEND_REQUEST_ACCEPT",
                "FRIEND_REMOVE", "CIRCLE_CREATE", "CIRCLE_VIEW", "CIRCLE_UPDATE", "CIRCLE_DELETE", "CIRCLE_MEMBER_MANAGE",
                "CHAT_SEND", "CHAT_READ", "CHAT_EDIT_OWN", "CHAT_DELETE_OWN", "CHAT_MODERATE", "CHAT_VIEW_HISTORY",
                "CHAT_SEND_IMAGE", "CHAT_SEND_STICKER", "CHAT_SEND_VOICE", "CHAT_VIEW_MEDIA", "CHAT_DELETE_MEDIA", "STICKER_VIEW", "STICKER_MANAGE",
                "MEMORY_CREATE", "MEMORY_UPDATE", "MEMORY_DELETE", "SECRET_NOTE_MANAGE", "SYSTEM_INFO_VIEW",
                "PROFILE_VIEW", "PROFILE_UPDATE", "USER_SEARCH", "FRIEND_REQUEST_REJECT", "FRIEND_REQUEST_CANCEL", "USER_BLOCK", "USER_UNBLOCK", "BLOCKED_USERS_VIEW"
            };

            var existingAdminPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == adminRole.RoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPerms.Where(p => adminCodes.Contains(p.PermissionCode)))
            {
                if (!existingAdminPermIds.Contains(perm.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = adminRole.RoleId, PermissionId = perm.PermissionId });
                }
            }
        }

        // Seed User RolePermissions
        var userRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
        if (userRole != null)
        {
            var userCodes = new HashSet<string>
            {
                "USER_VIEW", "USER_UPDATE", "FRIEND_REQUEST_SEND", "FRIEND_REQUEST_ACCEPT", "FRIEND_REMOVE",
                "CIRCLE_CREATE", "CIRCLE_VIEW", "CHAT_SEND", "CHAT_READ", "CHAT_EDIT_OWN", "CHAT_DELETE_OWN", "CHAT_VIEW_HISTORY",
                "CHAT_SEND_IMAGE", "CHAT_SEND_STICKER", "CHAT_SEND_VOICE", "CHAT_VIEW_MEDIA", "STICKER_VIEW",
                "MEMORY_CREATE", "SECRET_NOTE_MANAGE",
                "PROFILE_VIEW", "PROFILE_UPDATE", "USER_SEARCH", "FRIEND_REQUEST_REJECT", "FRIEND_REQUEST_CANCEL", "USER_BLOCK", "USER_UNBLOCK", "BLOCKED_USERS_VIEW"
            };

            var existingUserPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == userRole.RoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPerms.Where(p => userCodes.Contains(p.PermissionCode)))
            {
                if (!existingUserPermIds.Contains(perm.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = userRole.RoleId, PermissionId = perm.PermissionId });
                }
            }
        }

        // Seed CircleOwner RolePermissions
        var circleOwnerRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "CircleOwner" || r.RoleName == "Owner");
        if (circleOwnerRole != null)
        {
            var circleOwnerCodes = new HashSet<string>
            {
                "CIRCLE_VIEW", "CIRCLE_UPDATE", "CIRCLE_DELETE", "CIRCLE_INVITE", "CIRCLE_INVITATION_CANCEL",
                "CIRCLE_MEMBER_VIEW", "CIRCLE_MEMBER_REMOVE", "CIRCLE_MEMBER_ROLE_UPDATE", "CIRCLE_TRANSFER_OWNERSHIP",
                "CIRCLE_MEMBER_MANAGE", "CHAT_SEND", "CHAT_READ", "CHAT_EDIT_OWN", "CHAT_DELETE_OWN", "CHAT_MODERATE", "CHAT_VIEW_HISTORY",
                "CHAT_SEND_IMAGE", "CHAT_SEND_STICKER", "CHAT_SEND_VOICE", "CHAT_VIEW_MEDIA", "CHAT_DELETE_MEDIA", "STICKER_VIEW",
                "MEMORY_CREATE"
            };

            var existingPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == circleOwnerRole.RoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPerms.Where(p => circleOwnerCodes.Contains(p.PermissionCode)))
            {
                if (!existingPermIds.Contains(perm.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = circleOwnerRole.RoleId, PermissionId = perm.PermissionId });
                }
            }
        }

        // Seed CircleAdmin RolePermissions
        var circleAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "CircleAdmin");
        if (circleAdminRole != null)
        {
            var circleAdminCodes = new HashSet<string>
            {
                "CIRCLE_VIEW", "CIRCLE_INVITE", "CIRCLE_INVITATION_CANCEL", "CIRCLE_MEMBER_VIEW",
                "CIRCLE_MEMBER_REMOVE", "CIRCLE_LEAVE", "CHAT_SEND", "CHAT_READ", "CHAT_EDIT_OWN", "CHAT_DELETE_OWN", "CHAT_MODERATE", "CHAT_VIEW_HISTORY",
                "CHAT_SEND_IMAGE", "CHAT_SEND_STICKER", "CHAT_SEND_VOICE", "CHAT_VIEW_MEDIA", "CHAT_DELETE_MEDIA", "STICKER_VIEW",
                "MEMORY_CREATE"
            };

            var existingPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == circleAdminRole.RoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPerms.Where(p => circleAdminCodes.Contains(p.PermissionCode)))
            {
                if (!existingPermIds.Contains(perm.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = circleAdminRole.RoleId, PermissionId = perm.PermissionId });
                }
            }
        }

        // Seed CircleMember RolePermissions
        var circleMemberRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "CircleMember" || r.RoleName == "Member");
        if (circleMemberRole != null)
        {
            var circleMemberCodes = new HashSet<string>
            {
                "CIRCLE_VIEW", "CIRCLE_MEMBER_VIEW", "CIRCLE_LEAVE", "CHAT_SEND", "CHAT_READ", "CHAT_EDIT_OWN", "CHAT_DELETE_OWN", "CHAT_VIEW_HISTORY",
                "CHAT_SEND_IMAGE", "CHAT_SEND_STICKER", "CHAT_SEND_VOICE", "CHAT_VIEW_MEDIA", "STICKER_VIEW",
                "MEMORY_CREATE"
            };

            var existingPermIds = await context.RolePermissions
                .Where(rp => rp.RoleId == circleMemberRole.RoleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            foreach (var perm in allPerms.Where(p => circleMemberCodes.Contains(p.PermissionCode)))
            {
                if (!existingPermIds.Contains(perm.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = circleMemberRole.RoleId, PermissionId = perm.PermissionId });
                }
            }
        }

        await context.SaveChangesAsync();

        // Ensure at least one SuperAdmin exists in UserRoles
        if (superAdminRole != null)
        {
            var hasSuperAdmin = await context.UserRoles.AnyAsync(ur => ur.RoleId == superAdminRole.RoleId);
            if (!hasSuperAdmin)
            {
                var adminUser = await context.Users.FirstOrDefaultAsync(u => u.RoleKey == "Me" || u.Username == "mochi_maker")
                                ?? await context.Users.OrderBy(u => u.UserId).FirstOrDefaultAsync();

                if (adminUser != null)
                {
                    context.UserRoles.Add(new UserRole { UserId = adminUser.UserId, RoleId = superAdminRole.RoleId });
                    await context.SaveChangesAsync();
                }
            }
        }

        // Ensure all users have the default 'User' role in UserRoles
        var defaultUserRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
        if (defaultUserRole != null)
        {
            var userIdsWithoutRole = await context.Users
                .Where(u => !context.UserRoles.Any(ur => ur.UserId == u.UserId))
                .Select(u => u.UserId)
                .ToListAsync();

            if (userIdsWithoutRole.Any())
            {
                var newAssignments = userIdsWithoutRole.Select(uid => new UserRole
                {
                    UserId = uid,
                    RoleId = defaultUserRole.RoleId
                });
                context.UserRoles.AddRange(newAssignments);
                await context.SaveChangesAsync();
            }
        }

        // Ensure VIP users also have the 'Bestie' role in UserRoles
        var bestieRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Bestie");
        if (bestieRole != null)
        {
            var bestieUsers = await context.Users
                .Where(u => (u.RoleKey == "Bestie" || u.RoleKey == "Me") &&
                            !context.UserRoles.Any(ur => ur.UserId == u.UserId && ur.RoleId == bestieRole.RoleId))
                .Select(u => u.UserId)
                .ToListAsync();

            if (bestieUsers.Any())
            {
                foreach (var uid in bestieUsers)
                {
                    context.UserRoles.Add(new UserRole { UserId = uid, RoleId = bestieRole.RoleId });
                }
                await context.SaveChangesAsync();
            }
        }

        // Seed StickerCategories and Stickers (Phase 7)
        if (!await context.StickerCategories.AnyAsync())
        {
            var categories = new List<StickerCategory>
            {
                new() { CategoryName = "Cute", Icon = "🌸", DisplayOrder = 1, IsActive = true },
                new() { CategoryName = "Love", Icon = "💖", DisplayOrder = 2, IsActive = true },
                new() { CategoryName = "Happy", Icon = "🥳", DisplayOrder = 3, IsActive = true },
                new() { CategoryName = "Sad", Icon = "🥺", DisplayOrder = 4, IsActive = true },
                new() { CategoryName = "Funny", Icon = "😹", DisplayOrder = 5, IsActive = true },
                new() { CategoryName = "Miss You", Icon = "💌", DisplayOrder = 6, IsActive = true },
                new() { CategoryName = "Good Morning", Icon = "☀️", DisplayOrder = 7, IsActive = true },
                new() { CategoryName = "Good Night", Icon = "🌙", DisplayOrder = 8, IsActive = true },
                new() { CategoryName = "Hearts", Icon = "💕", DisplayOrder = 9, IsActive = true }
            };
            context.StickerCategories.AddRange(categories);
            await context.SaveChangesAsync();

            var catMap = await context.StickerCategories.ToDictionaryAsync(c => c.CategoryName, c => c.CategoryId);

            var stickers = new List<Sticker>
            {
                // Cute
                new() { CategoryId = catMap["Cute"], StickerName = "Cherry Blossom", ImageUrl = "🌸", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Cute"], StickerName = "Pastel Ribbon", ImageUrl = "🎀", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Cute"], StickerName = "Strawberry Kiss", ImageUrl = "🍓", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Cute"], StickerName = "Fuzzy Teddy", ImageUrl = "🧸", DisplayOrder = 4, IsActive = true },

                // Love
                new() { CategoryId = catMap["Love"], StickerName = "Sparkling Heart", ImageUrl = "💖", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Love"], StickerName = "Pinky Promise", ImageUrl = "🤞", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Love"], StickerName = "Bouquet Hug", ImageUrl = "💐", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Love"], StickerName = "Friendship Crown", ImageUrl = "👑", DisplayOrder = 4, IsActive = true },

                // Happy
                new() { CategoryId = catMap["Happy"], StickerName = "Party Popper", ImageUrl = "🥳", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Happy"], StickerName = "Stardust Magic", ImageUrl = "✨", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Happy"], StickerName = "Sweet Cupcake", ImageUrl = "🧁", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Happy"], StickerName = "Rainbow Cheer", ImageUrl = "🌈", DisplayOrder = 4, IsActive = true },

                // Sad
                new() { CategoryId = catMap["Sad"], StickerName = "Teary Eyes", ImageUrl = "🥺", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Sad"], StickerName = "Bear Hug", ImageUrl = "🫂", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Sad"], StickerName = "Raincloud", ImageUrl = "🌧️", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Sad"], StickerName = "Bandage Mend", ImageUrl = "🩹", DisplayOrder = 4, IsActive = true },

                // Funny
                new() { CategoryId = catMap["Funny"], StickerName = "Laughing Cat", ImageUrl = "😹", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Funny"], StickerName = "Chaotic Bestie", ImageUrl = "🤪", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Funny"], StickerName = "Boba Gulp", ImageUrl = "🧋", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Funny"], StickerName = "Pizza Slice", ImageUrl = "🍕", DisplayOrder = 4, IsActive = true },

                // Miss You
                new() { CategoryId = catMap["Miss You"], StickerName = "Letter For You", ImageUrl = "💌", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Miss You"], StickerName = "Paper Plane", ImageUrl = "✈️", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Miss You"], StickerName = "Call Me", ImageUrl = "📱", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Miss You"], StickerName = "Hourglass Wait", ImageUrl = "⏳", DisplayOrder = 4, IsActive = true },

                // Good Morning
                new() { CategoryId = catMap["Good Morning"], StickerName = "Warm Sunshine", ImageUrl = "☀️", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Good Morning"], StickerName = "Morning Coffee", ImageUrl = "☕", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Good Morning"], StickerName = "Fluffy Pancakes", ImageUrl = "🥞", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Good Morning"], StickerName = "Sunflower Smile", ImageUrl = "🌻", DisplayOrder = 4, IsActive = true },

                // Good Night
                new() { CategoryId = catMap["Good Night"], StickerName = "Crescent Moon", ImageUrl = "🌙", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Good Night"], StickerName = "Shooting Star", ImageUrl = "🌠", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Good Night"], StickerName = "Sleepy Cloud", ImageUrl = "☁️", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Good Night"], StickerName = "Cozy Candle", ImageUrl = "🕯️", DisplayOrder = 4, IsActive = true },

                // Hearts
                new() { CategoryId = catMap["Hearts"], StickerName = "Pink Ribbon Heart", ImageUrl = "💝", DisplayOrder = 1, IsActive = true },
                new() { CategoryId = catMap["Hearts"], StickerName = "Spinning Hearts", ImageUrl = "💞", DisplayOrder = 2, IsActive = true },
                new() { CategoryId = catMap["Hearts"], StickerName = "Growing Heart", ImageUrl = "💗", DisplayOrder = 3, IsActive = true },
                new() { CategoryId = catMap["Hearts"], StickerName = "Beating Heart", ImageUrl = "💓", DisplayOrder = 4, IsActive = true }
            };
            context.Stickers.AddRange(stickers);
            await context.SaveChangesAsync();
        }
    }

    private static void EnsureSampleVoiceAudio(string dir)
    {
        var voicePath = Path.Combine(dir, "sweet-voice-note.wav");
        if (!File.Exists(voicePath))
        {
            var wavData = GenerateKawaiiChimeWav();
            File.WriteAllBytes(voicePath, wavData);
        }
    }

    private static byte[] GenerateKawaiiChimeWav()
    {
        // 16-bit Mono 22050Hz cute gentle chime (E5 -> G#5 -> B5 arpeggio)
        int sampleRate = 22050;
        double durationSeconds = 3.0;
        int totalSamples = (int)(sampleRate * durationSeconds);
        byte[] audioData = new byte[totalSamples * 2];

        double[] frequencies = { 659.25, 830.61, 987.77 }; // E5, G#5, B5 chime
        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double sampleValue = 0;

            for (int f = 0; f < frequencies.Length; f++)
            {
                double noteStartTime = f * 0.4;
                if (t >= noteStartTime)
                {
                    double noteTime = t - noteStartTime;
                    double env = Math.Exp(-noteTime * 2.5); // soft bell decay
                    sampleValue += Math.Sin(2.0 * Math.PI * frequencies[f] * noteTime) * env * 0.3;
                }
            }

            short s = (short)Math.Clamp(sampleValue * short.MaxValue, short.MinValue, short.MaxValue);
            audioData[i * 2] = (byte)(s & 0xff);
            audioData[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + audioData.Length);
        writer.Write("WAVE"u8.ToArray());

        // fmt chunk
        writer.Write("fmt "u8.ToArray());
        writer.Write(16); // Subchunk1Size
        writer.Write((short)1); // PCM
        writer.Write((short)1); // 1 channel
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // ByteRate
        writer.Write((short)2); // BlockAlign
        writer.Write((short)16); // BitsPerSample

        // data chunk
        writer.Write("data"u8.ToArray());
        writer.Write(audioData.Length);
        writer.Write(audioData);

        return ms.ToArray();
    }

    private static void EnsureSampleMemoryImages(string dir)
    {
        var bobaPath = Path.Combine(dir, "boba-run.svg");
        if (!File.Exists(bobaPath))
        {
            File.WriteAllText(bobaPath, GetBobaRunSvg());
        }

        var picnicPath = Path.Combine(dir, "picnic-day.svg");
        if (!File.Exists(picnicPath))
        {
            File.WriteAllText(picnicPath, GetPicnicDaySvg());
        }

        var photoboothPath = Path.Combine(dir, "photobooth.svg");
        if (!File.Exists(photoboothPath))
        {
            File.WriteAllText(photoboothPath, GetPhotoboothSvg());
        }

        var sunsetPath = Path.Combine(dir, "sunset-promise.svg");
        if (!File.Exists(sunsetPath))
        {
            File.WriteAllText(sunsetPath, GetSunsetPromiseSvg());
        }
    }

    private static string GetBobaRunSvg() => @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 500 500"">
  <defs>
    <linearGradient id=""bg1"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#FFE5EC""/>
      <stop offset=""100%"" stop-color=""#FFC2D1""/>
    </linearGradient>
    <linearGradient id=""tea"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#FDE2B8""/>
      <stop offset=""100%"" stop-color=""#E8B87D""/>
    </linearGradient>
  </defs>
  <rect width=""500"" height=""500"" rx=""24"" fill=""url(#bg1)""/>
  <circle cx=""80"" cy=""90"" r=""25"" fill=""#FFF"" opacity=""0.5""/>
  <circle cx=""420"" cy=""120"" r=""18"" fill=""#FFF"" opacity=""0.5""/>
  <!-- Boba Cup 1 -->
  <g transform=""translate(150, 160)"">
    <path d=""M 30,50 L 50,220 C 52,240 148,240 150,220 L 170,50 Z"" fill=""url(#tea)"" stroke=""#4A3B3C"" stroke-width=""6""/>
    <ellipse cx=""100"" cy=""50"" rx=""70"" ry=""15"" fill=""#FFF0F5"" stroke=""#4A3B3C"" stroke-width=""6""/>
    <rect x=""90"" y=""-30"" width=""20"" height=""90"" rx=""6"" fill=""#FF8FAB"" stroke=""#4A3B3C"" stroke-width=""5"" transform=""rotate(-10 100 20)""/>
    <!-- Boba Pearls -->
    <circle cx=""80"" cy=""200"" r=""14"" fill=""#4A3B3C""/>
    <circle cx=""115"" cy=""205"" r=""14"" fill=""#4A3B3C""/>
    <circle cx=""70"" cy=""180"" r=""13"" fill=""#4A3B3C""/>
    <circle cx=""100"" cy=""175"" r=""13"" fill=""#4A3B3C""/>
    <circle cx=""130"" cy=""190"" r=""13"" fill=""#4A3B3C""/>
    <!-- Cute Eyes -->
    <circle cx=""85"" cy=""120"" r=""6"" fill=""#4A3B3C""/>
    <circle cx=""115"" cy=""120"" r=""6"" fill=""#4A3B3C""/>
    <ellipse cx=""75"" cy=""126"" rx=""5"" ry=""3"" fill=""#FF8FAB""/>
    <ellipse cx=""125"" cy=""126"" rx=""5"" ry=""3"" fill=""#FF8FAB""/>
    <path d=""M 96,128 Q 100,135 104,128"" stroke=""#4A3B3C"" stroke-width=""4"" fill=""none"" stroke-linecap=""round""/>
  </g>
  <!-- Sparkles -->
  <text x=""250"" y=""440"" font-family=""'Fredoka', sans-serif"" font-size=""28"" font-weight=""bold"" fill=""#FB6F92"" text-anchor=""middle"">Boba Date Forever 🧋✨</text>
</svg>";

    private static string GetPicnicDaySvg() => @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 500 500"">
  <defs>
    <linearGradient id=""bg2"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#E2F0CB""/>
      <stop offset=""100%"" stop-color=""#C5E1A5""/>
    </linearGradient>
  </defs>
  <rect width=""500"" height=""500"" rx=""24"" fill=""url(#bg2)""/>
  <!-- Blanket -->
  <g transform=""translate(100, 200)"">
    <polygon points=""30,10 270,10 300,190 0,190"" fill=""#FFF"" stroke=""#FF8FAB"" stroke-width=""5""/>
    <path d=""M 30,10 L 300,190 M 270,10 L 0,190"" stroke=""#FFB3C6"" stroke-width=""3"" opacity=""0.5""/>
    <!-- Basket -->
    <rect x=""70"" y=""60"" width=""160"" height=""90"" rx=""16"" fill=""#DEB887"" stroke=""#8B5A2B"" stroke-width=""6""/>
    <path d=""M 100,60 C 100,0 200,0 200,60"" stroke=""#8B5A2B"" stroke-width=""7"" fill=""none""/>
    <circle cx=""120"" cy=""100"" r=""15"" fill=""#FF6B8B""/>
    <circle cx=""180"" cy=""100"" r=""15"" fill=""#FFD166""/>
    <!-- Strawberry -->
    <path d=""M 150,55 C 130,45 140,85 150,90 C 160,85 170,45 150,55 Z"" fill=""#FF4D6D""/>
  </g>
  <text x=""250"" y=""440"" font-family=""'Fredoka', sans-serif"" font-size=""28"" font-weight=""bold"" fill=""#4A3B3C"" text-anchor=""middle"">Sunny Picnic Day 🌸🧺</text>
</svg>";

    private static string GetPhotoboothSvg() => @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 500 500"">
  <defs>
    <linearGradient id=""bg3"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#E8D7F1""/>
      <stop offset=""100%"" stop-color=""#D8B4E2""/>
    </linearGradient>
  </defs>
  <rect width=""500"" height=""500"" rx=""24"" fill=""url(#bg3)""/>
  <!-- Two cute characters hugging -->
  <g transform=""translate(130, 140)"">
    <!-- Character 1 (Pink Bear) -->
    <circle cx=""90"" cy=""130"" r=""65"" fill=""#FFB3C6"" stroke=""#4A3B3C"" stroke-width=""5""/>
    <circle cx=""40"" cy=""80"" r=""22"" fill=""#FFB3C6"" stroke=""#4A3B3C"" stroke-width=""5""/>
    <circle cx=""140"" cy=""80"" r=""22"" fill=""#FFB3C6"" stroke=""#4A3B3C"" stroke-width=""5""/>
    <circle cx=""75"" cy=""125"" r=""6"" fill=""#4A3B3C""/>
    <circle cx=""105"" cy=""125"" r=""6"" fill=""#4A3B3C""/>
    <ellipse cx=""90"" cy=""135"" rx=""10"" ry=""7"" fill=""#FFF""/>
    <circle cx=""90"" cy=""132"" r=""4"" fill=""#4A3B3C""/>
    <ellipse cx=""65"" cy=""135"" rx=""6"" ry=""4"" fill=""#FF8FAB""/>
    <ellipse cx=""115"" cy=""135"" rx=""6"" ry=""4"" fill=""#FF8FAB""/>

    <!-- Character 2 (Cream Bunny) -->
    <circle cx=""180"" cy=""140"" r=""60"" fill=""#FFFDF9"" stroke=""#4A3B3C"" stroke-width=""5""/>
    <ellipse cx=""160"" cy=""60"" rx=""16"" ry=""45"" fill=""#FFFDF9"" stroke=""#4A3B3C"" stroke-width=""5""/>
    <ellipse cx=""200"" cy=""60"" rx=""16"" ry=""45"" fill=""#FFFDF9"" stroke=""#4A3B3C"" stroke-width=""5""/>
    <ellipse cx=""160"" cy=""60"" rx=""9"" ry=""28"" fill=""#FFCCD5""/>
    <ellipse cx=""200"" cy=""60"" rx=""9"" ry=""28"" fill=""#FFCCD5""/>
    <circle cx=""165"" cy=""135"" r=""5"" fill=""#4A3B3C""/>
    <circle cx=""195"" cy=""135"" r=""5"" fill=""#4A3B3C""/>
    <ellipse cx=""155"" cy=""142"" rx=""6"" ry=""4"" fill=""#FF8FAB""/>
    <ellipse cx=""205"" cy=""142"" rx=""6"" ry=""4"" fill=""#FF8FAB""/>
    <path d=""M 177,143 Q 180,148 183,143"" stroke=""#4A3B3C"" stroke-width=""3"" fill=""none""/>
    
    <!-- Heart between them -->
    <path d=""M 135,100 C 130,85 110,90 120,110 L 135,125 L 150,110 C 160,90 140,85 135,100 Z"" fill=""#FB6F92""/>
  </g>
  <text x=""250"" y=""440"" font-family=""'Fredoka', sans-serif"" font-size=""28"" font-weight=""bold"" fill=""#4A3B3C"" text-anchor=""middle"">Silly Photobooth 🍓📸</text>
</svg>";

    private static string GetSunsetPromiseSvg() => @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 500 500"">
  <defs>
    <linearGradient id=""sky"" x1=""0%"" y1=""0%"" x2=""0%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#FF8FAB""/>
      <stop offset=""40%"" stop-color=""#FFDAC1""/>
      <stop offset=""80%"" stop-color=""#FFE5EC""/>
      <stop offset=""100%"" stop-color=""#B5EAD7""/>
    </linearGradient>
  </defs>
  <rect width=""500"" height=""500"" rx=""24"" fill=""url(#sky)""/>
  <circle cx=""250"" cy=""260"" r=""75"" fill=""#FFF"" opacity=""0.9""/>
  <path d=""M 0,330 Q 120,310 250,330 T 500,325 L 500,500 L 0,500 Z"" fill=""#64B5F6"" opacity=""0.7""/>
  <path d=""M 0,370 Q 150,350 300,375 T 500,365 L 500,500 L 0,500 Z"" fill=""#42A5F5"" opacity=""0.8""/>
  <g transform=""translate(225, 200)"">
    <!-- Heart in sunset -->
    <path d=""M 25,15 C 10,-5 -15,10 5,35 L 25,55 L 45,35 C 65,10 40,-5 25,15 Z"" fill=""#FB6F92""/>
  </g>
  <text x=""250"" y=""440"" font-family=""'Fredoka', sans-serif"" font-size=""28"" font-weight=""bold"" fill=""#FFF"" text-anchor=""middle"">Sunset Pinky Promise 🌅💖</text>
</svg>";
}
