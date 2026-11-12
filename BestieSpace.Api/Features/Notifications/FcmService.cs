#pragma warning disable CS0618

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BestieSpace.Api.Infrastructure.Data;
using BestieSpace.Api.Infrastructure.Data.Entities;
using BestieSpace.Shared.DTOs;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BestieSpace.Api.Features.Notifications;

public interface IFcmService
{
    bool IsConfigured { get; }
    FcmWebConfigDto GetWebConfig();
    Task<bool> RegisterTokenAsync(int userId, string token, string deviceType, string? deviceModel = null);
    Task<bool> UnregisterTokenAsync(int userId, string token);
    Task<int> SendPushToUserAsync(int userId, string title, string body, string? clickUrl = null, Dictionary<string, string>? data = null);
    Task<bool> SendTestPushAsync(int userId, string? title = null, string? body = null);
}

public class FcmService : IFcmService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FcmService> _logger;
    private readonly IHostEnvironment _env;
    private static readonly object _initLock = new();
    private static bool _initialized = false;
    private static bool _isFirebaseAdminReady = false;

    public bool IsConfigured => _isFirebaseAdminReady;

    public FcmService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<FcmService> logger,
        IHostEnvironment env)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _env = env;

        EnsureFirebaseInitialized();
    }

    private void EnsureFirebaseInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            try
            {
                if (FirebaseApp.DefaultInstance != null)
                {
                    _isFirebaseAdminReady = true;
                    _initialized = true;
                    return;
                }

                // 1. Check raw JSON from environment variable
                var envJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")
                    ?? _config["Firebase:ServiceAccountJson"];

                GoogleCredential? credential = null;
                string? projectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
                    ?? _config["Firebase:ProjectId"];

                if (!string.IsNullOrWhiteSpace(envJson))
                {
                    credential = GoogleCredential.FromJson(envJson);
                    _logger.LogInformation("Firebase Admin SDK: Loaded credentials from JSON string/env variable.");
                }
                else
                {
                    // 2. Check credentials file path
                    var configuredPath = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH")
                        ?? _config["Firebase:CredentialsPath"]
                        ?? "firebase-adminsdk.json";

                    var candidatePaths = new[]
                    {
                        configuredPath,
                        Path.Combine(_env.ContentRootPath, configuredPath),
                        Path.Combine(_env.ContentRootPath, "firebase-adminsdk.json"),
                        Path.Combine(_env.ContentRootPath, "serviceAccountKey.json"),
                        Path.Combine(AppContext.BaseDirectory, "firebase-adminsdk.json")
                    };

                    foreach (var path in candidatePaths)
                    {
                        if (File.Exists(path))
                        {
                            credential = GoogleCredential.FromFile(path);
                            _logger.LogInformation("Firebase Admin SDK: Loaded credentials from file '{Path}'.", path);
                            break;
                        }
                    }
                }

                if (credential != null)
                {
                    var options = new AppOptions
                    {
                        Credential = credential
                    };

                    if (!string.IsNullOrWhiteSpace(projectId))
                    {
                        options.ProjectId = projectId;
                    }

                    FirebaseApp.Create(options);
                    _isFirebaseAdminReady = true;
                    _logger.LogInformation("Firebase Admin SDK initialized successfully for project '{ProjectId}'! 🌸 Realtime FCM push is active.", projectId ?? "default");
                }
                else
                {
                    _isFirebaseAdminReady = false;
                    _logger.LogInformation("Firebase Admin credentials not found. FCM Service is running in simulated/mock mode. Realtime push will activate when serviceAccountKey.json is configured.");
                }
            }
            catch (Exception ex)
            {
                _isFirebaseAdminReady = false;
                _logger.LogWarning(ex, "Failed to initialize Firebase Admin SDK. FCM push will fall back to simulated mode.");
            }
            finally
            {
                _initialized = true;
            }
        }
    }

    public FcmWebConfigDto GetWebConfig()
    {
        var section = _config.GetSection("Firebase:WebConfig");
        var apiKey = Environment.GetEnvironmentVariable("FIREBASE_WEB_API_KEY") ?? section["ApiKey"];
        var authDomain = Environment.GetEnvironmentVariable("FIREBASE_AUTH_DOMAIN") ?? section["AuthDomain"] ?? "bestiespace-app.firebaseapp.com";
        var projectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID") ?? section["ProjectId"] ?? _config["Firebase:ProjectId"] ?? "bestiespace-app";
        var storageBucket = Environment.GetEnvironmentVariable("FIREBASE_STORAGE_BUCKET") ?? section["StorageBucket"] ?? $"{projectId}.appspot.com";
        var messagingSenderId = Environment.GetEnvironmentVariable("FIREBASE_MESSAGING_SENDER_ID") ?? section["MessagingSenderId"];
        var appId = Environment.GetEnvironmentVariable("FIREBASE_APP_ID") ?? section["AppId"];
        var vapidKey = Environment.GetEnvironmentVariable("FIREBASE_VAPID_KEY") ?? section["VapidKey"];

        bool isConfigured = !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(projectId);

        return new FcmWebConfigDto
        {
            ApiKey = apiKey,
            AuthDomain = authDomain,
            ProjectId = projectId,
            StorageBucket = storageBucket,
            MessagingSenderId = messagingSenderId,
            AppId = appId,
            VapidKey = vapidKey,
            IsConfigured = isConfigured
        };
    }

    public async Task<bool> RegisterTokenAsync(int userId, string token, string deviceType, string? deviceModel = null)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(token)) return false;

        token = token.Trim();
        deviceType = string.IsNullOrWhiteSpace(deviceType) ? "Web" : deviceType.Trim();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BestieDbContext>();

        var existing = await context.UserFcmTokens
            .FirstOrDefaultAsync(t => t.Token == token);

        if (existing != null)
        {
            existing.UserId = userId;
            existing.DeviceType = deviceType;
            existing.DeviceModel = deviceModel ?? existing.DeviceModel;
            existing.IsActive = true;
            existing.LastUsedAt = DateTime.UtcNow;
        }
        else
        {
            var newToken = new UserFcmToken
            {
                UserId = userId,
                Token = token,
                DeviceType = deviceType,
                DeviceModel = deviceModel,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };
            context.UserFcmTokens.Add(newToken);
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("FCM token registered for user {UserId} ({DeviceType})", userId, deviceType);
        return true;
    }

    public async Task<bool> UnregisterTokenAsync(int userId, string token)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(token)) return false;

        token = token.Trim();
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BestieDbContext>();

        var existing = await context.UserFcmTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);

        if (existing != null)
        {
            existing.IsActive = false;
            await context.SaveChangesAsync();
            _logger.LogInformation("FCM token unregistered for user {UserId}", userId);
            return true;
        }

        return false;
    }

    public async Task<int> SendPushToUserAsync(int userId, string title, string body, string? clickUrl = null, Dictionary<string, string>? data = null)
    {
        if (userId <= 0) return 0;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BestieDbContext>();

        var tokens = await context.UserFcmTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .ToListAsync();

        if (tokens.Count == 0)
        {
            _logger.LogDebug("User {UserId} has no registered FCM tokens for push notification.", userId);
            return 0;
        }

        title = string.IsNullOrWhiteSpace(title) ? "BestieSpace Alert 🌸" : title.Trim();
        body = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();
        clickUrl ??= "/";

        var payloadData = data ?? new Dictionary<string, string>();
        if (!payloadData.ContainsKey("clickUrl"))
        {
            payloadData["clickUrl"] = clickUrl;
        }

        if (!_isFirebaseAdminReady)
        {
            _logger.LogInformation("[FCM SIMULATED PUSH] To User {UserId} ({Count} devices) -> Title: '{Title}' | Body: '{Body}' | URL: '{Url}'",
                userId, tokens.Count, title, body, clickUrl);
            return tokens.Count;
        }

        try
        {
            var tokenStrings = tokens.Select(t => t.Token).Distinct().ToList();

            var message = new MulticastMessage
            {
                Tokens = tokenStrings,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = payloadData,
                Webpush = new WebpushConfig
                {
                    Notification = new WebpushNotification
                    {
                        Title = title,
                        Body = body,
                        Icon = "/favicon.png",
                        Badge = "/favicon.png"
                    },
                    FcmOptions = new WebpushFcmOptions
                    {
                        Link = clickUrl
                    }
                }
            };

            var batchResponse = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
            _logger.LogInformation("FCM push sent to user {UserId}: {Success}/{Total} delivered successfully.",
                userId, batchResponse.SuccessCount, tokenStrings.Count);

            // Clean up invalid or expired tokens
            var invalidTokens = new List<string>();
            for (int i = 0; i < batchResponse.Responses.Count; i++)
            {
                var resp = batchResponse.Responses[i];
                if (!resp.IsSuccess && resp.Exception is FirebaseMessagingException fex)
                {
                    if (fex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                        fex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                    {
                        invalidTokens.Add(tokenStrings[i]);
                    }
                }
            }

            if (invalidTokens.Count > 0)
            {
                var tokensToDeactivate = tokens.Where(t => invalidTokens.Contains(t.Token)).ToList();
                foreach (var t in tokensToDeactivate)
                {
                    t.IsActive = false;
                }
                await context.SaveChangesAsync();
                _logger.LogInformation("Deactivated {Count} stale FCM tokens for user {UserId}", tokensToDeactivate.Count, userId);
            }

            return batchResponse.SuccessCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM push to user {UserId}", userId);
            return 0;
        }
    }

    public async Task<bool> SendTestPushAsync(int userId, string? title = null, string? body = null)
    {
        title ??= "BestieSpace Realtime Push 🌸";
        body ??= "Hello Bestie! Realtime push notification is working perfectly! 💕✨";

        var count = await SendPushToUserAsync(userId, title, body, "/", new Dictionary<string, string>
        {
            { "test", "true" },
            { "sentAt", DateTime.UtcNow.ToString("o") }
        });

        return count > 0;
    }
}
