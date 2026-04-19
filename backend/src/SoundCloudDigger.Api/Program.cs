using SoundCloudDigger.Api;
using SoundCloudDigger.Api.Services;
using SoundCloudDigger.Api.Services.Persistence;
using SoundCloudDigger.Api.Services.Persistence.Migrations;

EnvFileLoader.Load();

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

builder.Services.AddSingleton<SoundCloudRateLimiter>(_ => new SoundCloudRateLimiter(maxConcurrent: 6));
builder.Services.AddSingleton<RetryPolicy>(_ => new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromSeconds(1)));

builder.Services.AddHttpClient<ISoundCloudClient, SoundCloudClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });
builder.Services.AddSingleton<Microsoft.Data.Sqlite.SqliteConnection>(_ =>
{
    var conn = Db.Open();
    SchemaMigrator.Migrate(conn, new IMigration[] { new V1_InitialSchema() });
    return conn;
});

builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<IFollowingsService, FollowingsService>();
builder.Services.AddSingleton<IFeedCache, FeedCache>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IFeedService, FeedService>();
builder.Services.AddSingleton<DiscoverRepository>();
builder.Services.AddSingleton<IDiscoverFeedService, DiscoverFeedService>();

var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://scdigger.localhost:5173";
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
