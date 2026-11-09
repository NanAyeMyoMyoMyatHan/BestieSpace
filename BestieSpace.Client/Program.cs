using BestieSpace.Client.Components;
using BestieSpace.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Authentication & Authorization
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<BestieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<BestieAuthenticationStateProvider>());

// Configure HttpClient & BestieApiClient
builder.Services.AddHttpClient<IBestieApiClient, BestieApiClient>(client =>
{
    var baseUri = builder.Configuration["ApiBaseUrl"] ?? "http://127.0.0.1:5000";
    client.BaseAddress = new Uri(baseUri);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseStaticFiles();

// Serve uploaded media directly from BestieSpace.Api/wwwroot so all assets work seamlessly through one public URL
var apiWebRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "BestieSpace.Api", "wwwroot"));
if (Directory.Exists(apiWebRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(apiWebRoot),
        RequestPath = ""
    });
}
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
