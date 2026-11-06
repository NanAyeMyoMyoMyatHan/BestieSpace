using BestieSpace.Client.Components;
using BestieSpace.Client.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
