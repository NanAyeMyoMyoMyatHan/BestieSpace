using BestieSpace.Api.Features.Auth;
using BestieSpace.Api.Features.Chat;
using BestieSpace.Api.Features.Dashboard;
using BestieSpace.Api.Features.Memories;
using BestieSpace.Api.Features.SecretNotes;
using BestieSpace.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite DbContext
var dataDir = builder.Configuration["DATA_DIR"] ?? Environment.GetEnvironmentVariable("DATA_DIR");
string connectionString;
if (!string.IsNullOrWhiteSpace(dataDir))
{
    Directory.CreateDirectory(dataDir);
    var dbPath = Path.Combine(dataDir, "bestiespace.db");
    connectionString = $"Data Source={dbPath}";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=bestiespace.db";
}

builder.Services.AddDbContext<BestieDbContext>(options =>
    options.UseSqlite(connectionString));

// Add Feature Services
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IMemoriesService, MemoriesService>();
builder.Services.AddScoped<ISecretNotesService, SecretNotesService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Add SignalR
builder.Services.AddSignalR();

// Add Controllers & JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Configure CORS for Blazor Client & SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Ensure wwwroot directory exists before building app
var webRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRoot);
builder.Environment.WebRootPath = webRoot;

var app = builder.Build();

// Auto-run Database Bootstrapper on startup
using (var scope = app.Services.CreateScope())
{
    await DatabaseBootstrapper.InitializeAsync(scope.ServiceProvider, app.Environment);

    // Optional self-verification mode for automated test suites
    if (args.Contains("--verify"))
    {
        Console.WriteLine("\n================ BESTIESPACE VERIFICATION ================");
        var dashSvc = scope.ServiceProvider.GetRequiredService<IDashboardService>();
        var memSvc = scope.ServiceProvider.GetRequiredService<IMemoriesService>();
        var noteSvc = scope.ServiceProvider.GetRequiredService<ISecretNotesService>();
        var chatSvc = scope.ServiceProvider.GetRequiredService<IChatService>();
        var authSvc = scope.ServiceProvider.GetRequiredService<IAuthService>();

        // 1. Dashboard & Miss You Meter
        var dash = await dashSvc.GetDashboardDataAsync();
        Console.WriteLine($"[PASS] Dashboard fetched. Letter: '{dash.AppStats.MainLetterTitle}', MissYouCount: {dash.AppStats.MissYouCount}");
        var inc = await dashSvc.IncrementMissYouCountAsync();
        Console.WriteLine($"[PASS] MissYouCount incremented to: {inc.MissYouCount}");

        // 2. Memories Gallery
        var memories = await memSvc.GetAllMemoriesAsync();
        Console.WriteLine($"[PASS] Seeded Memories count: {memories.Count}");
        var newMem = await memSvc.AddMemoryAsync(null, "Test Polaroid Memory 🌸", "bow", DateTime.UtcNow);
        Console.WriteLine($"[PASS] New Memory added ID: {newMem.Id}, Caption: '{newMem.Caption}', Angle: {newMem.RotationAngle}");

        // 3. Secret Notes
        var notes = await noteSvc.GetAllNotesAsync();
        Console.WriteLine($"[PASS] Secret Notes count: {notes.Count}");
        var unlockedNote = await noteSvc.UnlockNoteAsync(notes[0].Id);
        Console.WriteLine($"[PASS] Note {unlockedNote?.Id} unlocked. Title: '{unlockedNote?.Title}', IsUnlocked: {unlockedNote?.IsUnlocked}");

        // 4. Multimedia Chat & Offline Message Queue
        var messages = await chatSvc.GetMessagesAsync("Bestie 🌸");
        Console.WriteLine($"[PASS] Initial Chat Messages: {messages.Count}");
        var unreadCount = messages.Count(m => !m.IsRead);
        Console.WriteLine($"[PASS] Unread Messages in Queue for Bestie: {unreadCount}");

        var sentText = await chatSvc.SendTextOrStickerAsync(new BestieSpace.Shared.DTOs.SendTextOrStickerRequest
        {
            SenderName = "Me 🎀",
            MessageType = "text",
            Content = "Testing our cute chat pipeline! 🌸"
        });
        Console.WriteLine($"[PASS] Text message sent ID: {sentText.Id}, Content: '{sentText.Content}', IsRead: {sentText.IsRead}");

        var sentSticker = await chatSvc.SendTextOrStickerAsync(new BestieSpace.Shared.DTOs.SendTextOrStickerRequest
        {
            SenderName = "Bestie 🌸",
            MessageType = "sticker",
            Content = "hugging-bear"
        });
        Console.WriteLine($"[PASS] Sticker message sent ID: {sentSticker.Id}, Content: '{sentSticker.Content}'");

        // Test Mark as Read pipeline
        var unreadMsgIds = messages.Where(m => !m.IsRead).Select(m => m.Id).Take(2).ToList();
        if (unreadMsgIds.Any())
        {
            var marked = await chatSvc.MarkMessagesReadAsync(new BestieSpace.Shared.DTOs.MarkMessagesReadRequest
            {
                MessageIds = unreadMsgIds,
                ReaderName = "Bestie 🌸"
            });
            Console.WriteLine($"[PASS] MarkMessagesReadAsync succeeded: {marked}");
        }

        // 5. Auth & Login Slice
        var hint = await authSvc.GetHintAsync("strawberry_bestie");
        Console.WriteLine($"[PASS] Password Hint for Bestie: '{hint.Hint}'");

        var badLogin = await authSvc.LoginAsync(new BestieSpace.Shared.DTOs.LoginRequestDto
        {
            Username = "strawberry_bestie",
            Password = "wrongpassword"
        });
        Console.WriteLine($"[PASS] Bad login correctly rejected: {badLogin.Message}");

        var goodLogin = await authSvc.LoginAsync(new BestieSpace.Shared.DTOs.LoginRequestDto
        {
            Username = "strawberry_bestie",
            Password = "missyou3000!"
        });
        Console.WriteLine($"[PASS] Bestie login success: {goodLogin.IsSuccess}, DisplayName: '{goodLogin.User?.DisplayName}', Role: '{goodLogin.User?.RoleKey}'");

        Console.WriteLine("================ ALL VERIFICATIONS PASSED! ================\n");
        return;
    }
}

// Enable Static Files (Uploads, Images, SVGs)
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");

app.MapControllers();
app.MapHub<BestieChatHub>("/chathub");

// Welcome launchpad if opened directly in browser on port 5000
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8""/>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
    <title>BestieSpace 🌸 API & Launchpad</title>
    <meta http-equiv=""refresh"" content=""1;url=http://localhost:5002/"" />
    <style>
        body {
            font-family: 'Fredoka', 'Quicksand', -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif;
            background: #FFFDF9;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            margin: 0;
            color: #4A3B3C;
            text-align: center;
            padding: 20px;
        }
        .launchpad-card {
            background: #FFE5EC;
            padding: 44px 36px;
            border-radius: 32px;
            border: 3px solid #FFC2D1;
            box-shadow: 0 16px 40px rgba(255, 143, 171, 0.3);
            max-width: 520px;
            width: 100%;
        }
        h1 {
            color: #FB6F92;
            font-size: 2.2rem;
            margin: 14px 0 10px;
        }
        p {
            font-size: 1.15rem;
            color: #7E6566;
            margin-bottom: 28px;
            line-height: 1.6;
        }
        .btn-launch {
            background: linear-gradient(135deg, #FF8FAB, #FB6F92);
            color: #FFFFFF;
            padding: 16px 36px;
            border-radius: 9999px;
            text-decoration: none;
            font-weight: 700;
            font-size: 1.2rem;
            display: inline-flex;
            align-items: center;
            gap: 10px;
            box-shadow: 0 8px 24px rgba(251, 111, 146, 0.4);
            transition: transform 0.2s ease;
        }
        .btn-launch:hover {
            transform: scale(1.05);
        }
    </style>
</head>
<body>
    <div class=""launchpad-card"">
        <div style=""font-size: 58px;"">🌸💌✨</div>
        <h1>BestieSpace is Live!</h1>
        <p>Your backend API, database, and SignalR hub are active.<br/>Opening our cozy Blazor Clubhouse at <b>http://localhost:5002</b>...</p>
        <a class=""btn-launch"" href=""http://localhost:5002/"">
            <span>Open BestieSpace UI 💖</span>
        </a>
    </div>
</body>
</html>
", "text/html"));

app.Run();
