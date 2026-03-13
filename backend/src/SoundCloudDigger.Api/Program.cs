using SoundCloudDigger.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(2);
});

builder.Services.AddHttpClient<ISoundCloudClient, SoundCloudClient>();
builder.Services.AddSingleton<IFeedCache, FeedCache>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IFeedService, FeedService>();

var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.UseSession();
app.MapControllers();

// Background refresh: every 5 minutes, refresh all active user caches
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromMinutes(5));
        var cache = app.Services.GetRequiredService<IFeedCache>();
        var tokenService = app.Services.GetRequiredService<ITokenService>();

        foreach (var sessionId in cache.GetActiveSessionIds())
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var feedService = scope.ServiceProvider.GetRequiredService<IFeedService>();
                await feedService.RefreshAsync(sessionId);
            }
            catch
            {
                // Best effort — don't crash the refresh loop
            }
        }
    }
});

app.Run();
