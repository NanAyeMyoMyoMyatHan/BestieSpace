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

        // 1. Create database and tables using init.sql if needed or EF Core
        var initSqlPath = Path.Combine(environment.ContentRootPath, "Infrastructure", "Data", "init.sql");
        if (File.Exists(initSqlPath))
        {
            var sql = await File.ReadAllTextAsync(initSqlPath);
            await context.Database.OpenConnectionAsync();
            try
            {
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();

                // Safe migration for existing databases: check if IsRead & ReadAt exist
                command.CommandText = "PRAGMA table_info(ChatMessages);";
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        columns.Add(reader.GetString(1));
                    }
                }

                if (!columns.Contains("IsRead"))
                {
                    command.CommandText = "ALTER TABLE ChatMessages ADD COLUMN IsRead INTEGER NOT NULL DEFAULT 0;";
                    await command.ExecuteNonQueryAsync();
                }
                if (!columns.Contains("ReadAt"))
                {
                    command.CommandText = "ALTER TABLE ChatMessages ADD COLUMN ReadAt TEXT NULL;";
                    await command.ExecuteNonQueryAsync();
                }

                // Ensure Users table exists
                command.CommandText = @"
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
                    );";
                await command.ExecuteNonQueryAsync();

                // Safe migration for Users columns
                command.CommandText = "PRAGMA table_info(Users);";
                var userCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        userCols.Add(reader.GetString(1));
                    }
                }

                if (!userCols.Contains("QuickPin"))
                {
                    command.CommandText = "ALTER TABLE Users ADD COLUMN QuickPin TEXT NOT NULL DEFAULT '3000';";
                    await command.ExecuteNonQueryAsync();
                }
                if (!userCols.Contains("IsOnline"))
                {
                    command.CommandText = "ALTER TABLE Users ADD COLUMN IsOnline INTEGER NOT NULL DEFAULT 0;";
                    await command.ExecuteNonQueryAsync();
                }
                if (!userCols.Contains("LastSeenAt"))
                {
                    command.CommandText = "ALTER TABLE Users ADD COLUMN LastSeenAt TEXT NULL;";
                    await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // 2. Seed initial heartfelt data
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
                    Password = "missyou3000!",
                    DisplayName = "Sweet Bestie 🌸",
                    RoleKey = "Bestie",
                    AvatarSticker = "🌸",
                    PasswordHint = "How much I miss you (missyou3000!)",
                    QuickPin = "3000",
                    IsOnline = false,
                    LastSeenAt = DateTime.UtcNow.AddMinutes(-15),
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Username = "mochi_maker",
                    Password = "bestie4ever!",
                    DisplayName = "Your Bestie 🎀",
                    RoleKey = "Me",
                    AvatarSticker = "🎀",
                    PasswordHint = "Friends forever (bestie4ever!)",
                    QuickPin = "4040",
                    IsOnline = false,
                    LastSeenAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Users.AddRange(seedUsers);
            await context.SaveChangesAsync();
        }
        else
        {
            // Ensure QuickPin is set on existing rows
            var bestie = await context.Users.FirstOrDefaultAsync(u => u.RoleKey == "Bestie");
            if (bestie != null)
            {
                bestie.QuickPin = "3000";
                bestie.LastSeenAt ??= DateTime.UtcNow.AddMinutes(-15);
            }
            var me = await context.Users.FirstOrDefaultAsync(u => u.RoleKey == "Me");
            if (me != null)
            {
                me.QuickPin = "4040";
                me.LastSeenAt ??= DateTime.UtcNow;
            }
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
