using System.Net.Http.Json;
using BestieSpace.Shared.DTOs;

namespace BestieSpace.Client.Services;

public interface IBestieApiClient
{
    string ApiBaseUrl { get; }
    Task<DashboardDto> GetDashboardAsync();
    Task<MissYouResponse?> IncrementMissYouAsync();
    Task<MemoryDto?> AddMemoryAsync(MultipartFormDataContent content);
    Task<SecretNoteDto?> UnlockNoteAsync(int id);
    Task<List<ChatMessageDto>> GetChatMessagesAsync(string? viewer = null);
    Task<ChatMessageDto?> SendTextOrStickerAsync(SendTextOrStickerRequest request);
    Task<ChatMessageDto?> UploadPhotoAsync(MultipartFormDataContent content);
    Task<bool> MarkMessagesReadAsync(MarkMessagesReadRequest request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request);
    Task<PasswordHintDto> GetPasswordHintAsync(string? username = null);
    Task<UserPresenceDto?> GetPresenceAsync(string roleKey);
}

public class BestieApiClient : IBestieApiClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<BestieApiClient> _logger;
    private readonly string _baseUrl;

    public string ApiBaseUrl => _baseUrl;

    public BestieApiClient(HttpClient http, IConfiguration config, ILogger<BestieApiClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _baseUrl = _config["ApiBaseUrl"]?.TrimEnd('/') ?? "http://127.0.0.1:5000";
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<DashboardDto>($"{_baseUrl}/api/dashboard");
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch dashboard from API, using fallback data.");
        }

        return CreateFallbackDashboard();
    }

    public async Task<MissYouResponse?> IncrementMissYouAsync()
    {
        try
        {
            var res = await _http.PostAsync($"{_baseUrl}/api/dashboard/miss-you", null);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<MissYouResponse>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment miss you count.");
        }
        return null;
    }

    public async Task<MemoryDto?> AddMemoryAsync(MultipartFormDataContent content)
    {
        try
        {
            var res = await _http.PostAsync($"{_baseUrl}/api/memories", content);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<MemoryDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload memory.");
        }
        return null;
    }

    public async Task<SecretNoteDto?> UnlockNoteAsync(int id)
    {
        try
        {
            var res = await _http.PutAsync($"{_baseUrl}/api/notes/{id}/unlock", null);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<SecretNoteDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock note ID {Id}", id);
        }
        return null;
    }

    public async Task<List<ChatMessageDto>> GetChatMessagesAsync(string? viewer = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(viewer) 
                ? $"{_baseUrl}/api/chat/messages" 
                : $"{_baseUrl}/api/chat/messages?viewer={Uri.EscapeDataString(viewer)}";

            var res = await _http.GetFromJsonAsync<List<ChatMessageDto>>(url);
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch chat messages from API, using default list.");
        }
        return CreateFallbackMessages();
    }

    public async Task<ChatMessageDto?> SendTextOrStickerAsync(SendTextOrStickerRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/chat/send", request);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<ChatMessageDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send text or sticker message.");
        }
        return null;
    }

    public async Task<ChatMessageDto?> UploadPhotoAsync(MultipartFormDataContent content)
    {
        try
        {
            var res = await _http.PostAsync($"{_baseUrl}/api/chat/upload-photo", content);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<ChatMessageDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload photo chat message.");
        }
        return null;
    }

    public async Task<bool> MarkMessagesReadAsync(MarkMessagesReadRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/chat/mark-read", request);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark messages as read.");
            return false;
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/login", request);
            var content = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (content != null)
            {
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auth API request failed, evaluating offline fallback credentials.");
        }

        // Offline client fallback
        var clean = request.Username?.Trim().ToLowerInvariant();
        if (clean == "strawberry_bestie" && request.Password?.Trim() == "missyou3000!")
        {
            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Welcome back, Sweet Bestie! 💖",
                User = new UserSessionDto
                {
                    Id = 1,
                    Username = "strawberry_bestie",
                    DisplayName = "Sweet Bestie 🌸",
                    RoleKey = "Bestie",
                    AvatarSticker = "🌸"
                }
            };
        }
        if (clean == "mochi_maker" && request.Password?.Trim() == "bestie4ever!")
        {
            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Welcome back, Bestie Creator! 🎀",
                User = new UserSessionDto
                {
                    Id = 2,
                    Username = "mochi_maker",
                    DisplayName = "Your Bestie 🎀",
                    RoleKey = "Me",
                    AvatarSticker = "🎀"
                }
            };
        }

        return new AuthResponseDto
        {
            IsSuccess = false,
            Message = "Oopsie! Wrong secret code 🥺 Try again!"
        };
    }

    public async Task<PasswordHintDto> GetPasswordHintAsync(string? username = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(username) 
                ? $"{_baseUrl}/api/auth/hint" 
                : $"{_baseUrl}/api/auth/hint?username={Uri.EscapeDataString(username)}";
            var res = await _http.GetFromJsonAsync<PasswordHintDto>(url);
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch password hint from API.");
        }

        var clean = username?.Trim().ToLowerInvariant();
        if (clean == "mochi_maker")
        {
            return new PasswordHintDto
            {
                Username = "mochi_maker",
                Hint = "Friends forever (bestie4ever!)"
            };
        }

        return new PasswordHintDto
        {
            Username = "strawberry_bestie",
            Hint = "How much I miss you (missyou3000!)"
        };
    }

    public async Task<AuthResponseDto> PinLoginAsync(PinLoginRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"{_baseUrl}/api/auth/pin-login", request);
            var content = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (content != null) return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PIN Login API request failed, testing offline PIN fallback.");
        }

        var cleanPin = request.PinCode?.Trim();
        if (cleanPin == "3000")
        {
            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Strawberry PIN accepted! Welcome, Sweet Bestie! 🍓💖",
                User = new UserSessionDto
                {
                    Id = 1,
                    Username = "strawberry_bestie",
                    DisplayName = "Sweet Bestie 🌸",
                    RoleKey = "Bestie",
                    AvatarSticker = "🌸",
                    QuickPin = "3000"
                }
            };
        }
        if (cleanPin == "4040")
        {
            return new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Strawberry PIN accepted! Welcome, Creator Bestie! 🍓🎀",
                User = new UserSessionDto
                {
                    Id = 2,
                    Username = "mochi_maker",
                    DisplayName = "Your Bestie 🎀",
                    RoleKey = "Me",
                    AvatarSticker = "🎀",
                    QuickPin = "4040"
                }
            };
        }

        return new AuthResponseDto
        {
            IsSuccess = false,
            Message = "Oopsie! Wrong strawberry PIN 🍓🥺 Try again!"
        };
    }

    public async Task<UserPresenceDto?> GetPresenceAsync(string roleKey)
    {
        try
        {
            var clean = roleKey.Contains("Bestie", StringComparison.OrdinalIgnoreCase) ? "Bestie" : "Me";
            var res = await _http.GetFromJsonAsync<UserPresenceDto>($"{_baseUrl}/api/chat/presence/{clean}");
            if (res != null) return res;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get presence from API.");
        }

        var isBestie = roleKey.Contains("Bestie", StringComparison.OrdinalIgnoreCase);
        return new UserPresenceDto
        {
            RoleKey = isBestie ? "Bestie" : "Me",
            DisplayName = isBestie ? "Sweet Bestie 🌸" : "Your Bestie 🎀",
            IsOnline = false,
            LastSeenAt = DateTime.UtcNow.AddMinutes(-10)
        };
    }

    private static List<ChatMessageDto> CreateFallbackMessages()
    {
        var now = DateTime.UtcNow;
        return new List<ChatMessageDto>
        {
            new()
            {
                Id = 1,
                SenderName = "Me 🎀",
                MessageType = "text",
                Content = "Hi sweet bestie! Welcome to our secret digital clubhouse! 🌸 I built this so we always have a special cute place together.",
                Timestamp = now.AddMinutes(-30),
                IsRead = true,
                ReadAt = now.AddMinutes(-20)
            },
            new()
            {
                Id = 2,
                SenderName = "Bestie 🌸",
                MessageType = "sticker",
                Content = "hugging-bear",
                Timestamp = now.AddMinutes(-25),
                IsRead = true,
                ReadAt = now.AddMinutes(-20)
            },
            // Fallback unread sample messages
            new()
            {
                Id = 3,
                SenderName = "Me 🎀",
                MessageType = "text",
                Content = "Good morning sunshine! ☀️ I woke up today thinking about our late night laughs and missed you a ton! Hope your day is filled with sweet smiles 🌸",
                Timestamp = now.AddMinutes(-12),
                IsRead = false,
                ReadAt = null
            },
            new()
            {
                Id = 4,
                SenderName = "Me 🎀",
                MessageType = "sticker",
                Content = "sparkling-heart",
                Timestamp = now.AddMinutes(-10),
                IsRead = false,
                ReadAt = null
            },
            new()
            {
                Id = 5,
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
                Id = 6,
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
    }

    private static DashboardDto CreateFallbackDashboard()
    {
        return new DashboardDto
        {
            AppStats = new AppStatDto
            {
                Id = 1,
                MissYouCount = 1000,
                MainLetterTitle = "To My Favorite Human in the Entire World 🌸",
                MainLetterContent = "Dear Bestie,\n\nI built this cozy, pastel corner of the internet just for us! No matter how many miles stand between us or how crazy life gets, you will always be my safest place, my funniest laugh, and my favorite sister at heart.\n\nEvery time you miss me, press that little pink heart button below so I can feel your virtual hug! Browse through our polaroid memories, open the mystery envelopes when you need an instant smile, and send me silly stickers, photos, and voice notes anytime in our chat.\n\nI miss you more than words, boba cups, and stars in the sky combined. Thank you for being my constant sunshine! 💖✨",
                SenderNickname = "Me 🎀",
                RecipientNickname = "Bestie 🌸",
                LastUpdated = DateTime.UtcNow
            },
            Memories = new List<MemoryDto>
            {
                new()
                {
                    Id = 1,
                    Caption = "Spontaneous late-night boba run & uncontrollable laughing fit! 🧋✨",
                    ImageUrl = "/images/sample-memories/boba-run.svg",
                    MemoryDate = DateTime.UtcNow.AddMonths(-3),
                    StickerType = "bow",
                    RotationAngle = -2.5,
                    DisplayOrder = 1
                },
                new()
                {
                    Id = 2,
                    Caption = "Golden hour picnic in the park with homemade cookies & cute outfits 🌸🧺",
                    ImageUrl = "/images/sample-memories/picnic-day.svg",
                    MemoryDate = DateTime.UtcNow.AddMonths(-2),
                    StickerType = "sparkle",
                    RotationAngle = 2.0,
                    DisplayOrder = 2
                },
                new()
                {
                    Id = 3,
                    Caption = "Crying-laughing photobooth session trying on silly oversized glasses 🍓📸",
                    ImageUrl = "/images/sample-memories/photobooth.svg",
                    MemoryDate = DateTime.UtcNow.AddMonths(-1),
                    StickerType = "bear",
                    RotationAngle = -1.8,
                    DisplayOrder = 3
                },
                new()
                {
                    Id = 4,
                    Caption = "Watching the pastel sunset by the beach making 10-year pinky promises 🌅💖",
                    ImageUrl = "/images/sample-memories/sunset-promise.svg",
                    MemoryDate = DateTime.UtcNow.AddDays(-14),
                    StickerType = "heart",
                    RotationAngle = 2.4,
                    DisplayOrder = 4
                }
            },
            SecretNotes = new List<SecretNoteDto>
            {
                new()
                {
                    Id = 1,
                    Title = "Open when you miss me 💌",
                    PreviewHint = "A warm, gentle reminder when you feel a little lonely...",
                    Content = "Close your eyes, take a deep breath, and wrap your arms tight around yourself. That is a giant, warm bear hug from me flying across the miles! Remember all those late-night FaceTime calls where we laughed until we couldn't breathe? I am always just one text or call away. You are never alone, bestie! 🧸💖",
                    StickerIcon = "💌",
                    IsUnlocked = false
                },
                new()
                {
                    Id = 2,
                    Title = "My absolute favorite memory of us 🌟",
                    PreviewHint = "That unforgettable day we couldn't stop laughing...",
                    Content = "Do you remember that sudden rainy afternoon when we got completely drenched running for cover, burst into that tiny aesthetic bakery shivering, ordered huge marshmallow hot chocolates, and laughed until tears streamed down our faces? I replay that memory in my mind whenever I need instant happiness. You make ordinary days pure magic! ☕✨",
                    StickerIcon = "🌟",
                    IsUnlocked = false
                },
                new()
                {
                    Id = 3,
                    Title = "10 reasons why you're my bestie 🌸",
                    PreviewHint = "A quick list of why nobody could ever replace you...",
                    Content = "1. You know exactly what I'm thinking with just one eye contact.\n2. Your laugh is completely contagious.\n3. You hype me up even on my worst hair days.\n4. You never judge my weird food combinations.\n5. You always save the best bite for me.\n6. You send the most chaotic memes at 2 AM.\n7. You listen with your entire heart.\n8. You give the warmest, safest hugs.\n9. You believe in my dreams even when I doubt myself.\n10. Simply because you are YOU, and there will never be anyone like you! 🎀👑",
                    StickerIcon = "🌸",
                    IsUnlocked = false
                },
                new()
                {
                    Id = 4,
                    Title = "A pinky promise to you 🤞",
                    PreviewHint = "A lifetime promise sealed with love and sprinkles...",
                    Content = "I promise to celebrate every single one of your victories like they are my own. I promise to sit quietly beside you whenever life feels heavy. And I promise that when we are old grandmas with matching knitted cardigans, we will still be sipping tea and gossiping! No distance, time, or busy schedule will ever change our bond. Pinky swear forever and ever! 👭💕",
                    StickerIcon = "🤞",
                    IsUnlocked = false
                }
            }
        };
    }
}
