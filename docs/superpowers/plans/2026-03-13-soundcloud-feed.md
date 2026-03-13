# SoundCloud Digger Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a web app that lets users log in with SoundCloud, view their feed sorted by likes, and filter by genre — with a .NET backend and Svelte frontend.

**Architecture:** .NET Web API backend proxies SoundCloud's OAuth and `/me/feed/tracks` endpoint, caching results in-memory. Svelte SPA receives the full dataset on load and handles all sorting/filtering client-side.

**Tech Stack:** .NET 10 Web API (C#), Svelte 5 (SvelteKit), xUnit + Moq for backend tests, Vitest + Testing Library for frontend tests.

**Spec:** `docs/superpowers/specs/2026-03-13-soundcloud-feed-design.md`
**API Reference:** `.context/soundcloud-api-notes.md`

---

## File Structure

### Backend (`backend/`)

```
backend/
├── SoundCloudDigger.sln
├── src/
│   └── SoundCloudDigger.Api/
│       ├── SoundCloudDigger.Api.csproj
│       ├── Program.cs                       # App config, DI, middleware, CORS
│       ├── appsettings.json                 # SoundCloud client_id, client_secret, redirect_uri
│       ├── appsettings.Development.json     # Dev overrides
│       ├── Models/
│       │   ├── FeedTrack.cs                 # Track model mapped from SoundCloud response
│       │   ├── FeedResponse.cs              # API response wrapper (tracks, totalCount, loadingComplete)
│       │   └── SoundCloudModels.cs          # Raw SoundCloud API response DTOs
│       ├── Services/
│       │   ├── ISoundCloudClient.cs         # Interface for SoundCloud API calls
│       │   ├── SoundCloudClient.cs          # HTTP client for SoundCloud API
│       │   ├── IFeedCache.cs                # Interface for per-user feed cache
│       │   ├── FeedCache.cs                 # In-memory feed cache implementation
│       │   ├── IFeedService.cs              # Interface for feed fetching orchestration
│       │   ├── FeedService.cs               # Background feed fetch, pagination, 429 backoff
│       │   ├── ITokenService.cs             # Interface for token storage/refresh
│       │   └── TokenService.cs              # In-memory session store, token refresh logic
│       ├── Controllers/
│       │   ├── AuthController.cs            # /auth/login, /auth/callback, /auth/logout
│       │   └── FeedController.cs            # /api/feed
│       └── Helpers/
│           └── PkceHelper.cs                # PKCE code_verifier + code_challenge generation
├── tests/
│   └── SoundCloudDigger.Tests/
│       ├── SoundCloudDigger.Tests.csproj
│       ├── Services/
│       │   ├── FeedServiceTests.cs          # Pagination, 429 backoff, stop conditions
│       │   ├── FeedCacheTests.cs            # Cache storage, retrieval, TTL
│       │   └── TokenServiceTests.cs         # Token storage, refresh rotation
│       ├── Controllers/
│       │   ├── AuthControllerTests.cs       # PKCE flow, callback handling
│       │   └── FeedControllerTests.cs       # Feed endpoint responses
│       └── Helpers/
│           └── PkceHelperTests.cs           # PKCE generation correctness
```

### Frontend (`frontend/`)

```
frontend/
├── package.json
├── svelte.config.js
├── vite.config.ts
├── src/
│   ├── app.html                             # SvelteKit shell
│   ├── lib/
│   │   ├── stores/
│   │   │   ├── feedStore.ts                 # Raw feed data from backend
│   │   │   ├── filterStore.ts               # Sort, time range, genre selections
│   │   │   └── filteredFeedStore.ts         # Derived: sorted + filtered view
│   │   ├── api.ts                           # Fetch wrapper for /api/feed
│   │   └── types.ts                         # FeedTrack, FeedResponse TypeScript types
│   ├── routes/
│   │   ├── +page.svelte                     # LoginPage
│   │   └── feed/
│   │       └── +page.svelte                 # FeedPage (assembles components)
│   └── lib/
│       └── components/
│           ├── ControlsBar.svelte               # Sort, time range, genre filter controls
│           ├── TrackList.svelte                  # Scrollable track list
│           ├── TrackRow.svelte                   # Single track row
│           └── LoadingIndicator.svelte           # Progress during initial fetch
├── tests/
│   ├── stores/
│   │   └── filteredFeedStore.test.ts        # Sorting, filtering, derived computation
│   └── components/
│       ├── ControlsBar.test.ts              # Control state changes
│       └── TrackList.test.ts                # Rendering, empty states
```

---

## Chunk 1: Backend Foundation

### Task 1: Scaffold .NET project

**Files:**
- Create: `backend/SoundCloudDigger.sln`
- Create: `backend/src/SoundCloudDigger.Api/SoundCloudDigger.Api.csproj`
- Create: `backend/src/SoundCloudDigger.Api/Program.cs`
- Create: `backend/src/SoundCloudDigger.Api/appsettings.json`
- Create: `backend/src/SoundCloudDigger.Api/appsettings.Development.json`
- Create: `backend/tests/SoundCloudDigger.Tests/SoundCloudDigger.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo
mkdir -p backend
cd backend
dotnet new sln -n SoundCloudDigger
dotnet new webapi -n SoundCloudDigger.Api -o src/SoundCloudDigger.Api --no-openapi
dotnet sln add src/SoundCloudDigger.Api/SoundCloudDigger.Api.csproj
dotnet new xunit -n SoundCloudDigger.Tests -o tests/SoundCloudDigger.Tests
dotnet sln add tests/SoundCloudDigger.Tests/SoundCloudDigger.Tests.csproj
dotnet add tests/SoundCloudDigger.Tests reference src/SoundCloudDigger.Api
```

- [ ] **Step 2: Add test dependencies**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet add tests/SoundCloudDigger.Tests package Moq
dotnet add tests/SoundCloudDigger.Tests package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 3: Configure appsettings.json**

Write `backend/src/SoundCloudDigger.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "SoundCloud": {
    "ClientId": "",
    "ClientSecret": "",
    "RedirectUri": "http://localhost:5000/auth/callback"
  },
  "FrontendUrl": "http://localhost:5173"
}
```

- [ ] **Step 4: Configure Program.cs (minimal stub)**

Write `backend/src/SoundCloudDigger.Api/Program.cs` — start with a minimal stub. DI registrations will be added in Task 9 after all services exist.

```csharp
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

app.Run();
```

- [ ] **Step 5: Verify it builds**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "feat: scaffold .NET backend project with CORS and session config"
```

---

### Task 2: Models

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Models/FeedTrack.cs`
- Create: `backend/src/SoundCloudDigger.Api/Models/FeedResponse.cs`
- Create: `backend/src/SoundCloudDigger.Api/Models/SoundCloudModels.cs`

- [ ] **Step 1: Create SoundCloud API response DTOs**

Write `backend/src/SoundCloudDigger.Api/Models/SoundCloudModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace SoundCloudDigger.Api.Models;

public class SoundCloudActivitiesResponse
{
    [JsonPropertyName("collection")]
    public List<SoundCloudActivity> Collection { get; set; } = [];

    [JsonPropertyName("next_href")]
    public string? NextHref { get; set; }

    [JsonPropertyName("future_href")]
    public string? FutureHref { get; set; }
}

public class SoundCloudActivity
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("origin")]
    public SoundCloudTrack? Origin { get; set; }
}

public class SoundCloudTrack
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("artwork_url")]
    public string? ArtworkUrl { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("tag_list")]
    public string? TagList { get; set; }

    [JsonPropertyName("favoritings_count")]
    public int FavoritingsCount { get; set; }

    [JsonPropertyName("playback_count")]
    public int PlaybackCount { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("permalink_url")]
    public string? PermalinkUrl { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("access")]
    public string? Access { get; set; }

    [JsonPropertyName("user")]
    public SoundCloudUser? User { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

public class SoundCloudUser
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
}

public class SoundCloudTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
```

- [ ] **Step 2: Create FeedTrack model**

Write `backend/src/SoundCloudDigger.Api/Models/FeedTrack.cs`:

```csharp
namespace SoundCloudDigger.Api.Models;

public class FeedTrack
{
    public string Title { get; set; } = "";
    public string ArtistName { get; set; } = "";
    public string? ArtworkUrl { get; set; }
    public string? Genre { get; set; }
    public List<string> Tags { get; set; } = [];
    public int LikesCount { get; set; }
    public int PlaybackCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? PermalinkUrl { get; set; }
    public int Duration { get; set; }
    public string? Access { get; set; }
    public string ActivityType { get; set; } = "";
    public DateTime AppearedAt { get; set; }

    public static FeedTrack FromActivity(SoundCloudActivity activity)
    {
        var track = activity.Origin;
        if (track is null) throw new ArgumentException("Activity has no origin track");

        return new FeedTrack
        {
            Title = track.Title,
            ArtistName = track.User?.Username ?? "",
            ArtworkUrl = track.ArtworkUrl,
            Genre = track.Genre,
            Tags = ParseTagList(track.TagList),
            LikesCount = track.FavoritingsCount,
            PlaybackCount = track.PlaybackCount,
            CreatedAt = track.CreatedAt,
            PermalinkUrl = track.PermalinkUrl,
            Duration = track.Duration,
            Access = track.Access,
            ActivityType = activity.Type,
            AppearedAt = activity.CreatedAt,
        };
    }

    private static List<string> ParseTagList(string? tagList)
    {
        if (string.IsNullOrWhiteSpace(tagList)) return [];

        var tags = new List<string>();
        var span = tagList.AsSpan();
        var inQuote = false;
        var start = 0;

        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || (span[i] == ' ' && !inQuote))
            {
                if (i > start)
                {
                    var tag = span[start..i].Trim('"').ToString().Trim();
                    if (tag.Length > 0) tags.Add(tag);
                }
                start = i + 1;
            }
            else if (span[i] == '"')
            {
                inQuote = !inQuote;
            }
        }

        return tags;
    }
}
```

- [ ] **Step 3: Create FeedResponse model**

Write `backend/src/SoundCloudDigger.Api/Models/FeedResponse.cs`:

```csharp
namespace SoundCloudDigger.Api.Models;

public class FeedResponse
{
    public List<FeedTrack> Tracks { get; set; } = [];
    public int TotalCount { get; set; }
    public bool LoadingComplete { get; set; }
}
```

- [ ] **Step 4: Verify it builds**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Models/
git commit -m "feat: add data models for SoundCloud API and feed tracks"
```

---

### Task 3: PKCE Helper

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Helpers/PkceHelper.cs`
- Create: `backend/tests/SoundCloudDigger.Tests/Helpers/PkceHelperTests.cs`

- [ ] **Step 1: Write failing tests**

Write `backend/tests/SoundCloudDigger.Tests/Helpers/PkceHelperTests.cs`:

```csharp
using SoundCloudDigger.Api.Helpers;

namespace SoundCloudDigger.Tests.Helpers;

public class PkceHelperTests
{
    [Fact]
    public void GenerateCodeVerifier_Returns43To128CharBase64UrlString()
    {
        var verifier = PkceHelper.GenerateCodeVerifier();
        Assert.InRange(verifier.Length, 43, 128);
        Assert.Matches("^[A-Za-z0-9_-]+$", verifier);
    }

    [Fact]
    public void GenerateCodeChallenge_ReturnsSha256Base64UrlHash()
    {
        var verifier = "test_verifier_string_for_pkce_challenge";
        var challenge = PkceHelper.GenerateCodeChallenge(verifier);

        Assert.NotEmpty(challenge);
        Assert.Matches("^[A-Za-z0-9_-]+$", challenge);
        Assert.DoesNotContain("=", challenge);
    }

    [Fact]
    public void GenerateCodeChallenge_IsDeterministic()
    {
        var verifier = "deterministic_test";
        var challenge1 = PkceHelper.GenerateCodeChallenge(verifier);
        var challenge2 = PkceHelper.GenerateCodeChallenge(verifier);
        Assert.Equal(challenge1, challenge2);
    }

    [Fact]
    public void GenerateCodeVerifier_ProducesUniqueValues()
    {
        var v1 = PkceHelper.GenerateCodeVerifier();
        var v2 = PkceHelper.GenerateCodeVerifier();
        Assert.NotEqual(v1, v2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "PkceHelperTests" --verbosity normal
```

Expected: FAIL — `PkceHelper` doesn't exist.

- [ ] **Step 3: Implement PkceHelper**

Write `backend/src/SoundCloudDigger.Api/Helpers/PkceHelper.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace SoundCloudDigger.Api.Helpers;

public static class PkceHelper
{
    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "PkceHelperTests" --verbosity normal
```

Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Helpers/ backend/tests/SoundCloudDigger.Tests/Helpers/
git commit -m "feat: add PKCE helper with code verifier and challenge generation"
```

---

### Task 4: Token Service

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Services/ITokenService.cs`
- Create: `backend/src/SoundCloudDigger.Api/Services/TokenService.cs`
- Create: `backend/tests/SoundCloudDigger.Tests/Services/TokenServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Write `backend/tests/SoundCloudDigger.Tests/Services/TokenServiceTests.cs`:

```csharp
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Services;

public class TokenServiceTests
{
    private readonly TokenService _sut = new();

    [Fact]
    public void StoreAndRetrieve_ReturnsStoredTokens()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", 3600);

        var tokens = _sut.Get("session1");

        Assert.NotNull(tokens);
        Assert.Equal("access_abc", tokens.Value.AccessToken);
        Assert.Equal("refresh_xyz", tokens.Value.RefreshToken);
    }

    [Fact]
    public void Get_UnknownSession_ReturnsNull()
    {
        var tokens = _sut.Get("nonexistent");
        Assert.Null(tokens);
    }

    [Fact]
    public void UpdateRefreshToken_ReplacesOldToken()
    {
        _sut.Store("session1", "access_abc", "refresh_old", 3600);
        _sut.UpdateTokens("session1", "access_new", "refresh_new", 3600);

        var tokens = _sut.Get("session1");

        Assert.NotNull(tokens);
        Assert.Equal("access_new", tokens.Value.AccessToken);
        Assert.Equal("refresh_new", tokens.Value.RefreshToken);
    }

    [Fact]
    public void Remove_ClearsSession()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", 3600);
        _sut.Remove("session1");

        Assert.Null(_sut.Get("session1"));
    }

    [Fact]
    public void IsExpired_ReturnsTrueForExpiredToken()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", expiresIn: 0);

        Assert.True(_sut.IsExpired("session1"));
    }

    [Fact]
    public void IsExpired_ReturnsFalseForValidToken()
    {
        _sut.Store("session1", "access_abc", "refresh_xyz", expiresIn: 3600);

        Assert.False(_sut.IsExpired("session1"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "TokenServiceTests" --verbosity normal
```

Expected: FAIL — types don't exist.

- [ ] **Step 3: Implement ITokenService and TokenService**

Write `backend/src/SoundCloudDigger.Api/Services/ITokenService.cs`:

```csharp
namespace SoundCloudDigger.Api.Services;

public interface ITokenService
{
    void Store(string sessionId, string accessToken, string refreshToken, int expiresIn);
    (string AccessToken, string RefreshToken)? Get(string sessionId);
    void UpdateTokens(string sessionId, string accessToken, string refreshToken, int expiresIn);
    void Remove(string sessionId);
    bool IsExpired(string sessionId);
}
```

Write `backend/src/SoundCloudDigger.Api/Services/TokenService.cs`:

```csharp
using System.Collections.Concurrent;

namespace SoundCloudDigger.Api.Services;

public class TokenService : ITokenService
{
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();

    public void Store(string sessionId, string accessToken, string refreshToken, int expiresIn)
    {
        _tokens[sessionId] = new TokenEntry(accessToken, refreshToken, DateTime.UtcNow.AddSeconds(expiresIn));
    }

    public (string AccessToken, string RefreshToken)? Get(string sessionId)
    {
        if (_tokens.TryGetValue(sessionId, out var entry))
            return (entry.AccessToken, entry.RefreshToken);
        return null;
    }

    public void UpdateTokens(string sessionId, string accessToken, string refreshToken, int expiresIn)
    {
        _tokens[sessionId] = new TokenEntry(accessToken, refreshToken, DateTime.UtcNow.AddSeconds(expiresIn));
    }

    public void Remove(string sessionId)
    {
        _tokens.TryRemove(sessionId, out _);
    }

    public bool IsExpired(string sessionId)
    {
        if (!_tokens.TryGetValue(sessionId, out var entry)) return true;
        return DateTime.UtcNow >= entry.ExpiresAt;
    }

    private record TokenEntry(string AccessToken, string RefreshToken, DateTime ExpiresAt);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "TokenServiceTests" --verbosity normal
```

Expected: All 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Services/ITokenService.cs backend/src/SoundCloudDigger.Api/Services/TokenService.cs backend/tests/SoundCloudDigger.Tests/Services/
git commit -m "feat: add token service for in-memory session token storage"
```

---

### Task 5: SoundCloud HTTP Client

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Services/ISoundCloudClient.cs`
- Create: `backend/src/SoundCloudDigger.Api/Services/SoundCloudClient.cs`

This is a thin HTTP wrapper — tested indirectly through FeedService and AuthController tests. No unit tests for the client itself.

- [ ] **Step 1: Write ISoundCloudClient interface**

Write `backend/src/SoundCloudDigger.Api/Services/ISoundCloudClient.cs`:

```csharp
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public interface ISoundCloudClient
{
    Task<SoundCloudTokenResponse> ExchangeCodeForToken(string code, string codeVerifier, string redirectUri);
    Task<SoundCloudTokenResponse> RefreshAccessToken(string refreshToken);
    Task<SoundCloudActivitiesResponse> GetFeedTracks(string accessToken, int limit = 200, string? nextHref = null);
    Task SignOut(string accessToken);
}
```

- [ ] **Step 2: Implement SoundCloudClient**

Write `backend/src/SoundCloudDigger.Api/Services/SoundCloudClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class SoundCloudClient : ISoundCloudClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public SoundCloudClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<SoundCloudTokenResponse> ExchangeCodeForToken(string code, string codeVerifier, string redirectUri)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _config["SoundCloud:ClientId"]!,
            ["client_secret"] = _config["SoundCloud:ClientSecret"]!,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["code"] = code,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
        {
            Content = content,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SoundCloudTokenResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize token response");
    }

    public async Task<SoundCloudTokenResponse> RefreshAccessToken(string refreshToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _config["SoundCloud:ClientId"]!,
            ["client_secret"] = _config["SoundCloud:ClientSecret"]!,
            ["refresh_token"] = refreshToken,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
        {
            Content = content,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SoundCloudTokenResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize token response");
    }

    public async Task<SoundCloudActivitiesResponse> GetFeedTracks(string accessToken, int limit = 200, string? nextHref = null)
    {
        var url = nextHref ?? $"https://api.soundcloud.com/me/feed/tracks?limit={limit}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SoundCloudActivitiesResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize feed response");
    }

    public async Task SignOut(string accessToken)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { access_token = accessToken }),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("https://secure.soundcloud.com/sign-out", content);
        response.EnsureSuccessStatusCode();
    }
}
```

- [ ] **Step 3: Verify it builds**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Services/ISoundCloudClient.cs backend/src/SoundCloudDigger.Api/Services/SoundCloudClient.cs
git commit -m "feat: add SoundCloud HTTP client for OAuth and feed API calls"
```

---

### Task 6: Feed Cache

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Services/IFeedCache.cs`
- Create: `backend/src/SoundCloudDigger.Api/Services/FeedCache.cs`
- Create: `backend/tests/SoundCloudDigger.Tests/Services/FeedCacheTests.cs`

- [ ] **Step 1: Write failing tests**

Write `backend/tests/SoundCloudDigger.Tests/Services/FeedCacheTests.cs`:

```csharp
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Services;

public class FeedCacheTests
{
    private readonly FeedCache _sut = new();

    private static FeedTrack MakeTrack(string title, DateTime createdAt, int likes = 0, string? genre = null)
    {
        return new FeedTrack
        {
            Title = title,
            ArtistName = "artist",
            CreatedAt = createdAt,
            LikesCount = likes,
            Genre = genre,
            AppearedAt = createdAt,
        };
    }

    [Fact]
    public void GetTracks_EmptyCache_ReturnsEmptyList()
    {
        var result = _sut.GetTracks("user1");
        Assert.Empty(result);
    }

    [Fact]
    public void AddTracks_AndRetrieve_ReturnsTracks()
    {
        var tracks = new List<FeedTrack>
        {
            MakeTrack("Track A", DateTime.UtcNow),
            MakeTrack("Track B", DateTime.UtcNow),
        };

        _sut.AddTracks("user1", tracks);
        var result = _sut.GetTracks("user1");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AddTracks_MultipleBatches_Accumulates()
    {
        _sut.AddTracks("user1", [MakeTrack("A", DateTime.UtcNow)]);
        _sut.AddTracks("user1", [MakeTrack("B", DateTime.UtcNow)]);

        Assert.Equal(2, _sut.GetTracks("user1").Count);
    }

    [Fact]
    public void GetTracks_SeparateUsers_AreIsolated()
    {
        _sut.AddTracks("user1", [MakeTrack("A", DateTime.UtcNow)]);
        _sut.AddTracks("user2", [MakeTrack("B", DateTime.UtcNow)]);

        Assert.Single(_sut.GetTracks("user1"));
        Assert.Single(_sut.GetTracks("user2"));
    }

    [Fact]
    public void SetLoadingComplete_ReflectsState()
    {
        Assert.False(_sut.IsLoadingComplete("user1"));

        _sut.SetLoadingComplete("user1", true);

        Assert.True(_sut.IsLoadingComplete("user1"));
    }

    [Fact]
    public void Clear_RemovesUserData()
    {
        _sut.AddTracks("user1", [MakeTrack("A", DateTime.UtcNow)]);
        _sut.SetLoadingComplete("user1", true);

        _sut.Clear("user1");

        Assert.Empty(_sut.GetTracks("user1"));
        Assert.False(_sut.IsLoadingComplete("user1"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "FeedCacheTests" --verbosity normal
```

Expected: FAIL — types don't exist.

- [ ] **Step 3: Implement IFeedCache and FeedCache**

Write `backend/src/SoundCloudDigger.Api/Services/IFeedCache.cs`:

```csharp
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public interface IFeedCache
{
    List<FeedTrack> GetTracks(string sessionId);
    void AddTracks(string sessionId, List<FeedTrack> tracks);
    bool IsLoadingComplete(string sessionId);
    void SetLoadingComplete(string sessionId, bool complete);
    void Clear(string sessionId);
}
```

Write `backend/src/SoundCloudDigger.Api/Services/FeedCache.cs`:

```csharp
using System.Collections.Concurrent;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class FeedCache : IFeedCache
{
    private readonly ConcurrentDictionary<string, UserCache> _cache = new();

    public List<FeedTrack> GetTracks(string sessionId)
    {
        if (_cache.TryGetValue(sessionId, out var userCache))
        {
            lock (userCache.Lock)
            {
                return [.. userCache.Tracks];
            }
        }
        return [];
    }

    public void AddTracks(string sessionId, List<FeedTrack> tracks)
    {
        var userCache = _cache.GetOrAdd(sessionId, _ => new UserCache());
        lock (userCache.Lock)
        {
            userCache.Tracks.AddRange(tracks);
        }
    }

    public bool IsLoadingComplete(string sessionId)
    {
        if (_cache.TryGetValue(sessionId, out var userCache))
            return userCache.LoadingComplete;
        return false;
    }

    public void SetLoadingComplete(string sessionId, bool complete)
    {
        var userCache = _cache.GetOrAdd(sessionId, _ => new UserCache());
        userCache.LoadingComplete = complete;
    }

    public void Clear(string sessionId)
    {
        _cache.TryRemove(sessionId, out _);
    }

    private class UserCache
    {
        public readonly object Lock = new();
        public List<FeedTrack> Tracks { get; } = [];
        public volatile bool LoadingComplete;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "FeedCacheTests" --verbosity normal
```

Expected: All 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Services/IFeedCache.cs backend/src/SoundCloudDigger.Api/Services/FeedCache.cs backend/tests/SoundCloudDigger.Tests/Services/FeedCacheTests.cs
git commit -m "feat: add in-memory per-user feed cache"
```

---

### Task 7: Feed Service

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Services/IFeedService.cs`
- Create: `backend/src/SoundCloudDigger.Api/Services/FeedService.cs`
- Create: `backend/tests/SoundCloudDigger.Tests/Services/FeedServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Write `backend/tests/SoundCloudDigger.Tests/Services/FeedServiceTests.cs`:

```csharp
using Moq;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Services;

public class FeedServiceTests
{
    private readonly Mock<ISoundCloudClient> _mockClient = new();
    private readonly FeedCache _cache = new();
    private readonly Mock<ITokenService> _mockTokenService = new();
    private readonly FeedService _sut;

    public FeedServiceTests()
    {
        _sut = new FeedService(_mockClient.Object, _cache, _mockTokenService.Object);
    }

    private static SoundCloudActivitiesResponse MakeResponse(
        List<(string title, DateTime createdAt, int likes)> tracks,
        string? nextHref = null)
    {
        return new SoundCloudActivitiesResponse
        {
            Collection = tracks.Select(t => new SoundCloudActivity
            {
                Type = "track",
                CreatedAt = t.createdAt,
                Origin = new SoundCloudTrack
                {
                    Title = t.title,
                    CreatedAt = t.createdAt,
                    FavoritingsCount = t.likes,
                    User = new SoundCloudUser { Username = "artist" },
                },
            }).ToList(),
            NextHref = nextHref,
        };
    }

    [Fact]
    public async Task StartFetch_FetchesSinglePage_MarksComplete()
    {
        var now = DateTime.UtcNow;
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(MakeResponse([("Track A", now, 100)]));

        await _sut.StartFetchAsync("s1");

        Assert.True(_cache.IsLoadingComplete("s1"));
        Assert.Single(_cache.GetTracks("s1"));
    }

    [Fact]
    public async Task StartFetch_PaginatesThroughMultiplePages()
    {
        var now = DateTime.UtcNow;
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(MakeResponse([("A", now, 10)], "https://next-page"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, "https://next-page"))
            .ReturnsAsync(MakeResponse([("B", now, 20)]));

        await _sut.StartFetchAsync("s1");

        Assert.Equal(2, _cache.GetTracks("s1").Count);
        Assert.True(_cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public async Task StartFetch_StopsWhenAllTracksOlderThan24Hours()
    {
        var old = DateTime.UtcNow.AddDays(-2);
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(MakeResponse([("Old Track", old, 5)], "https://next-page"));

        await _sut.StartFetchAsync("s1");

        // Should not follow next_href since all tracks are > 24h old
        _mockClient.Verify(c => c.GetFeedTracks("token", 200, "https://next-page"), Times.Never);
        Assert.True(_cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public async Task StartFetch_BacksOffOn429()
    {
        var now = DateTime.UtcNow;
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));

        var callCount = 0;
        _mockClient.Setup(c => c.GetFeedTracks("token", 200, null))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests);
                return MakeResponse([("Track A", now, 100)]);
            });

        await _sut.StartFetchAsync("s1");

        Assert.Single(_cache.GetTracks("s1"));
        Assert.True(_cache.IsLoadingComplete("s1"));
    }

    [Fact]
    public async Task StartFetch_NoToken_DoesNotFetch()
    {
        _mockTokenService.Setup(t => t.Get("s1")).Returns((ValueTuple<string, string>?)null);

        await _sut.StartFetchAsync("s1");

        Assert.Empty(_cache.GetTracks("s1"));
        _mockClient.Verify(c => c.GetFeedTracks(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task StartFetch_On401_RefreshesTokenAndRetries()
    {
        var now = DateTime.UtcNow;
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("token", "refresh"));
        _mockTokenService.Setup(t => t.IsExpired("s1")).Returns(false);

        var callCount = 0;
        _mockClient.Setup(c => c.GetFeedTracks(It.IsAny<string>(), 200, null))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
                return MakeResponse([("Track A", now, 100)]);
            });

        _mockTokenService.Setup(t => t.Get("s1"))
            .Returns(("token", "refresh"));
        _mockClient.Setup(c => c.RefreshAccessToken("refresh"))
            .ReturnsAsync(new SoundCloudTokenResponse
            {
                AccessToken = "new_token",
                RefreshToken = "new_refresh",
                ExpiresIn = 3600,
            });

        // After refresh, return different token
        _mockTokenService.SetupSequence(t => t.Get("s1"))
            .Returns(("token", "refresh"))
            .Returns(("token", "refresh"))
            .Returns(("new_token", "new_refresh"));

        await _sut.StartFetchAsync("s1");

        Assert.Single(_cache.GetTracks("s1"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "FeedServiceTests" --verbosity normal
```

Expected: FAIL — `FeedService` doesn't exist.

- [ ] **Step 3: Implement IFeedService and FeedService**

Write `backend/src/SoundCloudDigger.Api/Services/IFeedService.cs`:

```csharp
namespace SoundCloudDigger.Api.Services;

public interface IFeedService
{
    Task StartFetchAsync(string sessionId);
    Task RefreshAsync(string sessionId);
}
```

Write `backend/src/SoundCloudDigger.Api/Services/FeedService.cs`:

```csharp
using System.Net;
using SoundCloudDigger.Api.Models;

namespace SoundCloudDigger.Api.Services;

public class FeedService : IFeedService
{
    private readonly ISoundCloudClient _client;
    private readonly IFeedCache _cache;
    private readonly ITokenService _tokenService;
    private const int MaxTracks = 10_000;
    private const int MaxRetries = 3;

    public FeedService(ISoundCloudClient client, IFeedCache cache, ITokenService tokenService)
    {
        _client = client;
        _cache = cache;
        _tokenService = tokenService;
    }

    public async Task StartFetchAsync(string sessionId)
    {
        var accessToken = await GetValidAccessToken(sessionId);
        if (accessToken is null) return;

        _cache.Clear(sessionId);
        var cutoff = DateTime.UtcNow.AddHours(-24);
        string? nextHref = null;
        var totalFetched = 0;

        while (totalFetched < MaxTracks)
        {
            SoundCloudActivitiesResponse response;
            try
            {
                response = await FetchWithRetryAndRefresh(sessionId, ref accessToken, nextHref);
            }
            catch (HttpRequestException)
            {
                break;
            }

            if (response.Collection.Count == 0) break;

            var tracks = response.Collection
                .Where(a => a.Origin is not null)
                .Select(FeedTrack.FromActivity)
                .ToList();

            _cache.AddTracks(sessionId, tracks);
            totalFetched += tracks.Count;

            var allOlderThanCutoff = response.Collection
                .All(a => a.CreatedAt < cutoff);

            if (allOlderThanCutoff || response.NextHref is null) break;

            nextHref = response.NextHref;
        }

        _cache.SetLoadingComplete(sessionId, true);
    }

    public async Task RefreshAsync(string sessionId)
    {
        var accessToken = await GetValidAccessToken(sessionId);
        if (accessToken is null) return;

        var existingTracks = _cache.GetTracks(sessionId);
        var existingUrls = new HashSet<string>(
            existingTracks.Where(t => t.PermalinkUrl is not null).Select(t => t.PermalinkUrl!));

        string? nextHref = null;
        var foundExisting = false;

        while (!foundExisting)
        {
            SoundCloudActivitiesResponse response;
            try
            {
                response = await FetchWithRetryAndRefresh(sessionId, ref accessToken, nextHref);
            }
            catch (HttpRequestException)
            {
                break;
            }

            if (response.Collection.Count == 0) break;

            var newTracks = new List<FeedTrack>();
            foreach (var activity in response.Collection.Where(a => a.Origin is not null))
            {
                var track = FeedTrack.FromActivity(activity);
                if (track.PermalinkUrl is not null && existingUrls.Contains(track.PermalinkUrl))
                {
                    foundExisting = true;
                    break;
                }
                newTracks.Add(track);
            }

            if (newTracks.Count > 0)
                _cache.AddTracks(sessionId, newTracks);

            if (response.NextHref is null) break;
            nextHref = response.NextHref;
        }
    }

    private async Task<string?> GetValidAccessToken(string sessionId)
    {
        if (_tokenService.IsExpired(sessionId))
        {
            var tokens = _tokenService.Get(sessionId);
            if (tokens is null) return null;

            try
            {
                var refreshed = await _client.RefreshAccessToken(tokens.Value.RefreshToken);
                _tokenService.UpdateTokens(sessionId, refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresIn);
                return refreshed.AccessToken;
            }
            catch
            {
                return null;
            }
        }

        return _tokenService.Get(sessionId)?.AccessToken;
    }

    private async Task<SoundCloudActivitiesResponse> FetchWithRetryAndRefresh(
        string sessionId, ref string accessToken, string? nextHref)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                return await _client.GetFeedTracks(accessToken, 200, nextHref);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Try refreshing the token once
                var newToken = await GetValidAccessToken(sessionId);
                if (newToken is null || newToken == accessToken) throw;
                accessToken = newToken;
                continue;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == MaxRetries - 1) throw;
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        throw new InvalidOperationException("Unreachable");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "FeedServiceTests" --verbosity normal
```

Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Services/IFeedService.cs backend/src/SoundCloudDigger.Api/Services/FeedService.cs backend/tests/SoundCloudDigger.Tests/Services/FeedServiceTests.cs
git commit -m "feat: add feed service with pagination, 24h cutoff, and 429 backoff"
```

---

### Task 8: Auth Controller

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Controllers/AuthController.cs`
- Create: `backend/tests/SoundCloudDigger.Tests/Controllers/AuthControllerTests.cs`

- [ ] **Step 1: Write failing tests**

Write `backend/tests/SoundCloudDigger.Tests/Controllers/AuthControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<ISoundCloudClient> _mockClient = new();
    private readonly Mock<ITokenService> _mockTokenService = new();
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();
    private readonly Mock<IFeedCache> _mockFeedCache = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SoundCloud:ClientId"] = "test_client_id",
                ["SoundCloud:RedirectUri"] = "http://localhost:5000/auth/callback",
                ["FrontendUrl"] = "http://localhost:5173",
            })
            .Build();

        _sut = new AuthController(config, _mockClient.Object, _mockTokenService.Object, _mockScopeFactory.Object, _mockFeedCache.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
    }

    [Fact]
    public void Login_RedirectsToSoundCloudWithPkceParams()
    {
        var result = _sut.Login() as RedirectResult;

        Assert.NotNull(result);
        Assert.Contains("secure.soundcloud.com/authorize", result.Url);
        Assert.Contains("client_id=test_client_id", result.Url);
        Assert.Contains("response_type=code", result.Url);
        Assert.Contains("code_challenge_method=S256", result.Url);
        Assert.Contains("code_challenge=", result.Url);
        Assert.Contains("state=", result.Url);
    }

    [Fact]
    public async Task Callback_ExchangesCodeAndStoresTokens()
    {
        // Set up session with PKCE verifier and state
        _sut.HttpContext.Session.SetString("pkce_verifier", "test_verifier");
        _sut.HttpContext.Session.SetString("oauth_state", "test_state");

        _mockClient.Setup(c => c.ExchangeCodeForToken("auth_code", "test_verifier", "http://localhost:5000/auth/callback"))
            .ReturnsAsync(new SoundCloudTokenResponse
            {
                AccessToken = "access_123",
                RefreshToken = "refresh_456",
                ExpiresIn = 3600,
            });

        var result = await _sut.Callback("auth_code", "test_state") as RedirectResult;

        Assert.NotNull(result);
        Assert.Equal("http://localhost:5173/feed", result.Url);
        _mockTokenService.Verify(t => t.Store(It.IsAny<string>(), "access_123", "refresh_456", 3600));
    }

    [Fact]
    public async Task Callback_InvalidState_ReturnsBadRequest()
    {
        _sut.HttpContext.Session.SetString("pkce_verifier", "test_verifier");
        _sut.HttpContext.Session.SetString("oauth_state", "correct_state");

        var result = await _sut.Callback("auth_code", "wrong_state");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Logout_CallsSignOutAndClearsSession()
    {
        _sut.HttpContext.Session.SetString("session_id", "s1");
        _mockTokenService.Setup(t => t.Get("s1")).Returns(("access_123", "refresh_456"));

        var result = await _sut.Logout() as OkResult;

        Assert.NotNull(result);
        _mockClient.Verify(c => c.SignOut("access_123"));
        _mockTokenService.Verify(t => t.Remove("s1"));
    }
}

// Minimal in-memory ISession for testing
public class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();
    public string Id => Guid.NewGuid().ToString();
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _store.Keys;
    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "AuthControllerTests" --verbosity normal
```

Expected: FAIL — `AuthController` doesn't exist.

- [ ] **Step 3: Implement AuthController**

Write `backend/src/SoundCloudDigger.Api/Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Helpers;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class AuthController : Controller
{
    private readonly IConfiguration _config;
    private readonly ISoundCloudClient _client;
    private readonly ITokenService _tokenService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFeedCache _feedCache;

    public AuthController(
        IConfiguration config,
        ISoundCloudClient client,
        ITokenService tokenService,
        IServiceScopeFactory scopeFactory,
        IFeedCache feedCache)
    {
        _config = config;
        _client = client;
        _tokenService = tokenService;
        _scopeFactory = scopeFactory;
        _feedCache = feedCache;
    }

    [HttpGet("/auth/login")]
    public IActionResult Login()
    {
        var verifier = PkceHelper.GenerateCodeVerifier();
        var challenge = PkceHelper.GenerateCodeChallenge(verifier);
        var state = Guid.NewGuid().ToString("N");

        HttpContext.Session.SetString("pkce_verifier", verifier);
        HttpContext.Session.SetString("oauth_state", state);

        var clientId = _config["SoundCloud:ClientId"];
        var redirectUri = _config["SoundCloud:RedirectUri"];
        var url = $"https://secure.soundcloud.com/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri!)}&response_type=code&code_challenge={challenge}&code_challenge_method=S256&state={state}";

        return Redirect(url);
    }

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        var expectedState = HttpContext.Session.GetString("oauth_state");
        if (state != expectedState)
            return BadRequest("Invalid state parameter");

        var verifier = HttpContext.Session.GetString("pkce_verifier")!;
        var redirectUri = _config["SoundCloud:RedirectUri"]!;
        var tokenResponse = await _client.ExchangeCodeForToken(code, verifier, redirectUri);

        var sessionId = Guid.NewGuid().ToString("N");
        _tokenService.Store(sessionId, tokenResponse.AccessToken, tokenResponse.RefreshToken, tokenResponse.ExpiresIn);
        HttpContext.Session.SetString("session_id", sessionId);

        // Use IServiceScopeFactory to avoid scoped service disposal issues
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var feedService = scope.ServiceProvider.GetRequiredService<IFeedService>();
            await feedService.StartFetchAsync(sessionId);
        });

        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:5173";
        return Redirect($"{frontendUrl}/feed");
    }

    [HttpPost("/auth/logout")]
    public async Task<IActionResult> Logout()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (sessionId is not null)
        {
            var tokens = _tokenService.Get(sessionId);
            if (tokens is not null)
            {
                try { await _client.SignOut(tokens.Value.AccessToken); }
                catch { /* best effort */ }
            }
            _tokenService.Remove(sessionId);
            _feedCache.Clear(sessionId);
        }
        HttpContext.Session.Clear();
        return Ok();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "AuthControllerTests" --verbosity normal
```

Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Controllers/AuthController.cs backend/tests/SoundCloudDigger.Tests/Controllers/
git commit -m "feat: add auth controller with PKCE OAuth flow and logout"
```

---

### Task 9: Feed Controller

**Files:**
- Create: `backend/src/SoundCloudDigger.Api/Controllers/FeedController.cs`
- Create: `backend/tests/SoundCloudDigger.Tests/Controllers/FeedControllerTests.cs`

- [ ] **Step 1: Write failing tests**

Write `backend/tests/SoundCloudDigger.Tests/Controllers/FeedControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SoundCloudDigger.Api.Controllers;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Controllers;

public class FeedControllerTests
{
    private readonly Mock<IFeedCache> _mockCache = new();
    private readonly FeedController _sut;

    public FeedControllerTests()
    {
        _sut = new FeedController(_mockCache.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        httpContext.Session.SetString("session_id", "s1");
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public void GetFeed_ReturnsCachedTracks()
    {
        var tracks = new List<FeedTrack>
        {
            new() { Title = "Track A", LikesCount = 100 },
            new() { Title = "Track B", LikesCount = 200 },
        };
        _mockCache.Setup(c => c.GetTracks("s1")).Returns(tracks);
        _mockCache.Setup(c => c.IsLoadingComplete("s1")).Returns(true);

        var result = _sut.GetFeed() as OkObjectResult;
        var response = result?.Value as FeedResponse;

        Assert.NotNull(response);
        Assert.Equal(2, response.TotalCount);
        Assert.True(response.LoadingComplete);
    }

    [Fact]
    public void GetFeed_NoSession_ReturnsUnauthorized()
    {
        _sut.HttpContext.Session.Clear();

        var result = _sut.GetFeed();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void GetFeed_LoadingInProgress_ReturnsPartialWithFlag()
    {
        _mockCache.Setup(c => c.GetTracks("s1")).Returns([new FeedTrack { Title = "A" }]);
        _mockCache.Setup(c => c.IsLoadingComplete("s1")).Returns(false);

        var result = _sut.GetFeed() as OkObjectResult;
        var response = result?.Value as FeedResponse;

        Assert.NotNull(response);
        Assert.False(response.LoadingComplete);
        Assert.Equal(1, response.TotalCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "FeedControllerTests" --verbosity normal
```

Expected: FAIL — `FeedController` doesn't exist.

- [ ] **Step 3: Implement FeedController**

Write `backend/src/SoundCloudDigger.Api/Controllers/FeedController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using SoundCloudDigger.Api.Models;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Api.Controllers;

[ApiController]
public class FeedController : Controller
{
    private readonly IFeedCache _cache;

    public FeedController(IFeedCache cache)
    {
        _cache = cache;
    }

    [HttpGet("/api/feed")]
    public IActionResult GetFeed()
    {
        var sessionId = HttpContext.Session.GetString("session_id");
        if (string.IsNullOrEmpty(sessionId))
            return Unauthorized();

        var tracks = _cache.GetTracks(sessionId);
        var loadingComplete = _cache.IsLoadingComplete(sessionId);

        return Ok(new FeedResponse
        {
            Tracks = tracks,
            TotalCount = tracks.Count,
            LoadingComplete = loadingComplete,
        });
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --filter "FeedControllerTests" --verbosity normal
```

Expected: All 3 tests PASS.

- [ ] **Step 5: Run all backend tests**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --verbosity normal
```

Expected: All tests PASS (PKCE: 4, TokenService: 6, FeedCache: 6, FeedService: 5, AuthController: 4, FeedController: 3 = 28 total).

- [ ] **Step 6: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/Controllers/FeedController.cs backend/tests/SoundCloudDigger.Tests/Controllers/FeedControllerTests.cs
git commit -m "feat: add feed controller returning cached tracks with loading state"
```

---

### Task 9a: Wire up Program.cs with DI and background refresh

**Files:**
- Modify: `backend/src/SoundCloudDigger.Api/Program.cs`

- [ ] **Step 1: Update Program.cs with all DI registrations and background refresh**

Write `backend/src/SoundCloudDigger.Api/Program.cs`:

```csharp
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
```

- [ ] **Step 2: Add GetActiveSessionIds to IFeedCache and FeedCache**

Add to `backend/src/SoundCloudDigger.Api/Services/IFeedCache.cs`:

```csharp
List<string> GetActiveSessionIds();
```

Add implementation to `backend/src/SoundCloudDigger.Api/Services/FeedCache.cs`:

```csharp
public List<string> GetActiveSessionIds()
{
    return [.. _cache.Keys];
}
```

- [ ] **Step 3: Verify it builds and all tests pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet build && dotnet test --verbosity normal
```

Expected: Build succeeds, all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add backend/src/SoundCloudDigger.Api/
git commit -m "feat: wire up DI registrations and background feed refresh loop"
```

---

## Chunk 2: Frontend

### Task 10: Scaffold Svelte project

**Files:**
- Create: `frontend/` (SvelteKit scaffold)

- [ ] **Step 1: Create SvelteKit project**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo
npx sv create frontend --template minimal --types ts --no-add-ons
```

- [ ] **Step 2: Install test dependencies**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npm install
npm install -D vitest @testing-library/svelte @testing-library/jest-dom jsdom
```

- [ ] **Step 3: Configure Vitest**

Add to `frontend/vite.config.ts`:

```typescript
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vitest/config';

export default defineConfig({
	plugins: [sveltekit()],
	test: {
		include: ['tests/**/*.test.ts'],
		environment: 'jsdom',
	},
});
```

- [ ] **Step 4: Verify it builds**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npm run build
```

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add frontend/
git commit -m "feat: scaffold SvelteKit frontend project with Vitest"
```

---

### Task 11: TypeScript types and API client

**Files:**
- Create: `frontend/src/lib/types.ts`
- Create: `frontend/src/lib/api.ts`

- [ ] **Step 1: Create types**

Write `frontend/src/lib/types.ts`:

```typescript
export interface FeedTrack {
	title: string;
	artistName: string;
	artworkUrl: string | null;
	genre: string | null;
	tags: string[];
	likesCount: number;
	playbackCount: number;
	createdAt: string;
	permalinkUrl: string | null;
	duration: number;
	access: string | null;
	activityType: string;
	appearedAt: string;
}

export interface FeedResponse {
	tracks: FeedTrack[];
	totalCount: number;
	loadingComplete: boolean;
}

export type SortBy = 'likes' | 'date';
export type TimeRange = '24h' | '7d' | '30d' | 'all';
```

- [ ] **Step 2: Create API client**

Write `frontend/src/lib/api.ts`:

```typescript
import type { FeedResponse } from './types';

const API_BASE = '/api';

export async function fetchFeed(): Promise<FeedResponse> {
	const response = await fetch(`${API_BASE}/feed`, {
		credentials: 'include',
	});

	if (response.status === 401) {
		window.location.href = '/';
		throw new Error('Unauthorized');
	}

	if (!response.ok) {
		throw new Error(`Failed to fetch feed: ${response.statusText}`);
	}

	return response.json();
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/lib/types.ts frontend/src/lib/api.ts
git commit -m "feat: add TypeScript types and API client for feed"
```

---

### Task 12: Svelte stores

**Files:**
- Create: `frontend/src/lib/stores/feedStore.ts`
- Create: `frontend/src/lib/stores/filterStore.ts`
- Create: `frontend/src/lib/stores/filteredFeedStore.ts`
- Create: `frontend/tests/stores/filteredFeedStore.test.ts`

- [ ] **Step 1: Write failing tests for filtered feed store**

Write `frontend/tests/stores/filteredFeedStore.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { filterAndSort } from '$lib/stores/filteredFeedStore';
import type { FeedTrack, SortBy, TimeRange } from '$lib/types';

function makeTrack(overrides: Partial<FeedTrack> = {}): FeedTrack {
	return {
		title: 'Test Track',
		artistName: 'Artist',
		artworkUrl: null,
		genre: 'Electronic',
		tags: [],
		likesCount: 100,
		playbackCount: 500,
		createdAt: new Date().toISOString(),
		permalinkUrl: null,
		duration: 180000,
		access: 'playable',
		activityType: 'track',
		appearedAt: new Date().toISOString(),
		...overrides,
	};
}

describe('filterAndSort', () => {
	it('sorts by likes descending', () => {
		const tracks = [
			makeTrack({ title: 'Low', likesCount: 10 }),
			makeTrack({ title: 'High', likesCount: 1000 }),
			makeTrack({ title: 'Mid', likesCount: 100 }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', []);
		expect(result.map((t) => t.title)).toEqual(['High', 'Mid', 'Low']);
	});

	it('sorts by date descending', () => {
		const tracks = [
			makeTrack({ title: 'Old', createdAt: '2026-01-01T00:00:00Z' }),
			makeTrack({ title: 'New', createdAt: '2026-03-13T00:00:00Z' }),
			makeTrack({ title: 'Mid', createdAt: '2026-02-01T00:00:00Z' }),
		];

		const result = filterAndSort(tracks, 'date', 'all', []);
		expect(result.map((t) => t.title)).toEqual(['New', 'Mid', 'Old']);
	});

	it('filters by time range 24h', () => {
		const now = new Date();
		const yesterday = new Date(now.getTime() - 12 * 60 * 60 * 1000);
		const twoDaysAgo = new Date(now.getTime() - 48 * 60 * 60 * 1000);

		const tracks = [
			makeTrack({ title: 'Recent', createdAt: yesterday.toISOString() }),
			makeTrack({ title: 'Old', createdAt: twoDaysAgo.toISOString() }),
		];

		const result = filterAndSort(tracks, 'likes', '24h', []);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Recent');
	});

	it('filters by time range 7d', () => {
		const now = new Date();
		const threeDaysAgo = new Date(now.getTime() - 3 * 24 * 60 * 60 * 1000);
		const tenDaysAgo = new Date(now.getTime() - 10 * 24 * 60 * 60 * 1000);

		const tracks = [
			makeTrack({ title: 'Recent', createdAt: threeDaysAgo.toISOString() }),
			makeTrack({ title: 'Old', createdAt: tenDaysAgo.toISOString() }),
		];

		const result = filterAndSort(tracks, 'likes', '7d', []);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('Recent');
	});

	it('filters by genre', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
			makeTrack({ title: 'C', genre: 'Electronic' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', ['Electronic']);
		expect(result).toHaveLength(2);
		expect(result.every((t) => t.genre === 'Electronic')).toBe(true);
	});

	it('multiple genre filters use OR logic', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
			makeTrack({ title: 'C', genre: 'Ambient' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', ['Electronic', 'Ambient']);
		expect(result).toHaveLength(2);
	});

	it('empty genre filter shows all tracks', () => {
		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic' }),
			makeTrack({ title: 'B', genre: 'Hip-hop' }),
		];

		const result = filterAndSort(tracks, 'likes', 'all', []);
		expect(result).toHaveLength(2);
	});

	it('combines time range and genre filters', () => {
		const now = new Date();
		const recent = new Date(now.getTime() - 12 * 60 * 60 * 1000);
		const old = new Date(now.getTime() - 48 * 60 * 60 * 1000);

		const tracks = [
			makeTrack({ title: 'A', genre: 'Electronic', createdAt: recent.toISOString() }),
			makeTrack({ title: 'B', genre: 'Hip-hop', createdAt: recent.toISOString() }),
			makeTrack({ title: 'C', genre: 'Electronic', createdAt: old.toISOString() }),
		];

		const result = filterAndSort(tracks, 'likes', '24h', ['Electronic']);
		expect(result).toHaveLength(1);
		expect(result[0].title).toBe('A');
	});
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npx vitest run tests/stores/filteredFeedStore.test.ts
```

Expected: FAIL — module doesn't exist.

- [ ] **Step 3: Implement stores**

Write `frontend/src/lib/stores/feedStore.ts`:

```typescript
import { writable } from 'svelte/store';
import type { FeedTrack } from '$lib/types';

export const feedTracks = writable<FeedTrack[]>([]);
export const loadingComplete = writable(false);
export const totalCount = writable(0);
```

Write `frontend/src/lib/stores/filterStore.ts`:

```typescript
import { writable } from 'svelte/store';
import type { SortBy, TimeRange } from '$lib/types';

export const sortBy = writable<SortBy>('likes');
export const timeRange = writable<TimeRange>('24h');
export const selectedGenres = writable<string[]>([]);
```

Write `frontend/src/lib/stores/filteredFeedStore.ts`:

```typescript
import { derived } from 'svelte/store';
import { feedTracks } from './feedStore';
import { sortBy, timeRange, selectedGenres } from './filterStore';
import type { FeedTrack, SortBy, TimeRange } from '$lib/types';

const TIME_RANGE_MS: Record<TimeRange, number> = {
	'24h': 24 * 60 * 60 * 1000,
	'7d': 7 * 24 * 60 * 60 * 1000,
	'30d': 30 * 24 * 60 * 60 * 1000,
	all: Infinity,
};

export function filterAndSort(
	tracks: FeedTrack[],
	sort: SortBy,
	range: TimeRange,
	genres: string[]
): FeedTrack[] {
	const now = Date.now();
	const cutoff = TIME_RANGE_MS[range];

	let filtered = tracks;

	if (range !== 'all') {
		filtered = filtered.filter((t) => now - new Date(t.createdAt).getTime() <= cutoff);
	}

	if (genres.length > 0) {
		filtered = filtered.filter((t) => t.genre !== null && genres.includes(t.genre));
	}

	const sorted = [...filtered];
	if (sort === 'likes') {
		sorted.sort((a, b) => b.likesCount - a.likesCount);
	} else {
		sorted.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
	}

	return sorted;
}

export const filteredFeed = derived(
	[feedTracks, sortBy, timeRange, selectedGenres],
	([$tracks, $sortBy, $timeRange, $genres]) => filterAndSort($tracks, $sortBy, $timeRange, $genres)
);

export const availableGenres = derived(feedTracks, ($tracks) => {
	const genres = new Set<string>();
	for (const track of $tracks) {
		if (track.genre) genres.add(track.genre);
	}
	return [...genres].sort();
});
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npx vitest run tests/stores/filteredFeedStore.test.ts
```

Expected: All 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/lib/stores/ frontend/tests/stores/
git commit -m "feat: add Svelte stores for feed data, filters, and derived sorted/filtered view"
```

---

### Task 13: Svelte components

**Files:**
- Create: `frontend/src/lib/components/TrackRow.svelte`
- Create: `frontend/src/lib/components/TrackList.svelte`
- Create: `frontend/src/lib/components/ControlsBar.svelte`
- Create: `frontend/src/lib/components/LoadingIndicator.svelte`

- [ ] **Step 1: Create TrackRow component**

Write `frontend/src/lib/components/TrackRow.svelte`:

```svelte
<script lang="ts">
	import type { FeedTrack } from '$lib/types';

	let { track }: { track: FeedTrack } = $props();

	function formatDuration(ms: number): string {
		const minutes = Math.floor(ms / 60000);
		const seconds = Math.floor((ms % 60000) / 1000);
		return `${minutes}:${seconds.toString().padStart(2, '0')}`;
	}

	function formatLikes(count: number): string {
		if (count >= 1000) return `${(count / 1000).toFixed(1)}k`;
		return count.toString();
	}
</script>

<a
	href={track.permalinkUrl}
	target="_blank"
	rel="noopener noreferrer"
	class="track-row"
>
	<img
		src={track.artworkUrl ?? '/placeholder.png'}
		alt={track.title}
		class="artwork"
		width="40"
		height="40"
	/>
	<div class="info">
		<span class="title">{track.title}</span>
		<span class="meta">{track.artistName}{track.genre ? ` · ${track.genre}` : ''}</span>
	</div>
	<span class="likes">♥ {formatLikes(track.likesCount)}</span>
	<span class="duration">{formatDuration(track.duration)}</span>
</a>

<style>
	.track-row {
		display: flex;
		align-items: center;
		gap: 12px;
		padding: 8px 12px;
		border-radius: 6px;
		background: #16213e;
		text-decoration: none;
		color: inherit;
	}
	.track-row:hover {
		background: #1a2744;
	}
	.artwork {
		border-radius: 4px;
		flex-shrink: 0;
		object-fit: cover;
	}
	.info {
		flex: 1;
		min-width: 0;
		display: flex;
		flex-direction: column;
	}
	.title {
		color: #eee;
		font-size: 14px;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}
	.meta {
		color: #888;
		font-size: 12px;
	}
	.likes {
		color: #e94560;
		font-size: 13px;
		flex-shrink: 0;
	}
	.duration {
		color: #888;
		font-size: 12px;
		flex-shrink: 0;
	}
</style>
```

- [ ] **Step 2: Create TrackList component**

Write `frontend/src/lib/components/TrackList.svelte`:

```svelte
<script lang="ts">
	import type { FeedTrack } from '$lib/types';
	import TrackRow from './TrackRow.svelte';

	let { tracks }: { tracks: FeedTrack[] } = $props();
</script>

{#if tracks.length === 0}
	<div class="empty">
		<p>No tracks match your filters.</p>
	</div>
{:else}
	<div class="track-list">
		{#each tracks as track (track.permalinkUrl ?? track.title + track.artistName)}
			<TrackRow {track} />
		{/each}
	</div>
{/if}

<style>
	.track-list {
		display: flex;
		flex-direction: column;
		gap: 4px;
	}
	.empty {
		text-align: center;
		color: #888;
		padding: 48px 0;
	}
</style>
```

- [ ] **Step 3: Create ControlsBar component**

Write `frontend/src/lib/components/ControlsBar.svelte`:

```svelte
<script lang="ts">
	import type { SortBy, TimeRange } from '$lib/types';
	import { sortBy, timeRange, selectedGenres } from '$lib/stores/filterStore';
	import { availableGenres } from '$lib/stores/filteredFeedStore';

	let dropdownOpen = $state(false);

	const sortOptions: { value: SortBy; label: string }[] = [
		{ value: 'likes', label: 'Likes' },
		{ value: 'date', label: 'Date' },
	];

	const timeOptions: { value: TimeRange; label: string }[] = [
		{ value: '24h', label: '24h' },
		{ value: '7d', label: '7d' },
		{ value: '30d', label: '30d' },
		{ value: 'all', label: 'All' },
	];

	function toggleGenre(genre: string) {
		selectedGenres.update((current) => {
			if (current.includes(genre)) {
				return current.filter((g) => g !== genre);
			}
			return [...current, genre];
		});
	}
</script>

<div class="controls-bar">
	<div class="control-group">
		<span class="label">Sort:</span>
		{#each sortOptions as opt}
			<button
				class="toggle"
				class:active={$sortBy === opt.value}
				onclick={() => sortBy.set(opt.value)}
			>
				{opt.label}
			</button>
		{/each}
	</div>

	<div class="control-group">
		<span class="label">Time:</span>
		{#each timeOptions as opt}
			<button
				class="toggle"
				class:active={$timeRange === opt.value}
				onclick={() => timeRange.set(opt.value)}
			>
				{opt.label}
			</button>
		{/each}
	</div>

	{#if $availableGenres.length > 0}
		<div class="control-group genre-group">
			<span class="label">Genre:</span>
			<div class="genre-dropdown">
				<button class="dropdown-toggle" onclick={() => (dropdownOpen = !dropdownOpen)}>
					{$selectedGenres.length === 0
						? 'All genres'
						: `${$selectedGenres.length} selected`}
				</button>
				{#if dropdownOpen}
					<div class="dropdown-menu">
						{#each $availableGenres as genre}
							<label class="dropdown-item">
								<input
									type="checkbox"
									checked={$selectedGenres.includes(genre)}
									onchange={() => toggleGenre(genre)}
								/>
								{genre}
							</label>
						{/each}
					</div>
				{/if}
			</div>
		</div>
	{/if}
</div>

<style>
	.controls-bar {
		display: flex;
		gap: 16px;
		align-items: center;
		flex-wrap: wrap;
		padding: 12px 16px;
		background: #16213e;
		border-radius: 8px;
	}
	.control-group {
		display: flex;
		align-items: center;
		gap: 4px;
	}
	.label {
		color: #aaa;
		font-size: 13px;
		margin-right: 4px;
	}
	.toggle {
		background: transparent;
		border: none;
		color: #666;
		padding: 4px 10px;
		border-radius: 4px;
		cursor: pointer;
		font-size: 13px;
	}
	.toggle.active {
		background: #e94560;
		color: white;
	}
	.genre-group {
		position: relative;
	}
	.dropdown-toggle {
		background: transparent;
		border: 1px solid #333;
		color: #aaa;
		padding: 4px 12px;
		border-radius: 4px;
		cursor: pointer;
		font-size: 13px;
	}
	.dropdown-menu {
		position: absolute;
		top: 100%;
		left: 0;
		background: #16213e;
		border: 1px solid #333;
		border-radius: 6px;
		padding: 8px 0;
		max-height: 240px;
		overflow-y: auto;
		z-index: 10;
		min-width: 180px;
		margin-top: 4px;
	}
	.dropdown-item {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 4px 12px;
		color: #ccc;
		font-size: 13px;
		cursor: pointer;
	}
	.dropdown-item:hover {
		background: #1a2744;
	}
</style>
```

- [ ] **Step 4: Create LoadingIndicator component**

Write `frontend/src/lib/components/LoadingIndicator.svelte`:

```svelte
<script lang="ts">
	let { totalCount }: { totalCount: number } = $props();
</script>

<div class="loading">
	<p>Loading your feed... {totalCount} tracks fetched</p>
</div>

<style>
	.loading {
		text-align: center;
		color: #aaa;
		padding: 24px 0;
		font-size: 14px;
	}
</style>
```

- [ ] **Step 5: Verify it builds**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npm run build
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/lib/components/
git commit -m "feat: add ControlsBar, TrackList, TrackRow, and LoadingIndicator components"
```

---

### Task 13a: Component tests

**Files:**
- Create: `frontend/tests/components/TrackList.test.ts`
- Create: `frontend/tests/components/ControlsBar.test.ts`

- [ ] **Step 1: Write TrackList test**

Write `frontend/tests/components/TrackList.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/svelte';
import TrackList from '$lib/components/TrackList.svelte';
import type { FeedTrack } from '$lib/types';

function makeTrack(overrides: Partial<FeedTrack> = {}): FeedTrack {
	return {
		title: 'Test Track',
		artistName: 'Artist',
		artworkUrl: null,
		genre: 'Electronic',
		tags: [],
		likesCount: 100,
		playbackCount: 500,
		createdAt: new Date().toISOString(),
		permalinkUrl: 'https://soundcloud.com/test',
		duration: 180000,
		access: 'playable',
		activityType: 'track',
		appearedAt: new Date().toISOString(),
		...overrides,
	};
}

describe('TrackList', () => {
	it('renders empty state when no tracks', () => {
		render(TrackList, { props: { tracks: [] } });
		expect(screen.getByText('No tracks match your filters.')).toBeTruthy();
	});

	it('renders track rows for each track', () => {
		const tracks = [
			makeTrack({ title: 'Track A' }),
			makeTrack({ title: 'Track B' }),
		];
		render(TrackList, { props: { tracks } });
		expect(screen.getByText('Track A')).toBeTruthy();
		expect(screen.getByText('Track B')).toBeTruthy();
	});
});
```

- [ ] **Step 2: Run tests to verify they pass**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npx vitest run tests/components/
```

Expected: All component tests PASS.

- [ ] **Step 3: Commit**

```bash
git add frontend/tests/components/
git commit -m "test: add TrackList component tests"
```

---

### Task 14: Pages and routing

**Files:**
- Modify: `frontend/src/routes/+page.svelte`
- Create: `frontend/src/routes/feed/+page.svelte`

- [ ] **Step 1: Create LoginPage**

Write `frontend/src/routes/+page.svelte`:

```svelte
<div class="login-page">
	<h1>SoundCloud Digger</h1>
	<p>Sort your feed by likes. Filter by genre.</p>
	<a href="/auth/login" class="login-button">Log in with SoundCloud</a>
</div>

<style>
	.login-page {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		min-height: 80vh;
		text-align: center;
	}
	h1 {
		color: #e94560;
		font-size: 32px;
		margin-bottom: 8px;
	}
	p {
		color: #aaa;
		margin-bottom: 32px;
	}
	.login-button {
		background: #e94560;
		color: white;
		padding: 12px 32px;
		border-radius: 6px;
		text-decoration: none;
		font-size: 16px;
	}
	.login-button:hover {
		background: #d63851;
	}
</style>
```

- [ ] **Step 2: Create FeedPage**

Write `frontend/src/routes/feed/+page.svelte`:

```svelte
<script lang="ts">
	import { onMount } from 'svelte';
	import { fetchFeed } from '$lib/api';
	import { feedTracks, loadingComplete, totalCount } from '$lib/stores/feedStore';
	import { filteredFeed } from '$lib/stores/filteredFeedStore';
	import ControlsBar from '$lib/components/ControlsBar.svelte';
	import TrackList from '$lib/components/TrackList.svelte';
	import LoadingIndicator from '$lib/components/LoadingIndicator.svelte';

	let error = $state('');
	let intervalId: ReturnType<typeof setInterval> | null = null;

	async function pollFeed() {
		try {
			const data = await fetchFeed();
			feedTracks.set(data.tracks);
			totalCount.set(data.totalCount);
			loadingComplete.set(data.loadingComplete);
			error = '';
			return data.loadingComplete;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load feed';
			return false;
		}
	}

	function clearPoll() {
		if (intervalId !== null) {
			clearInterval(intervalId);
			intervalId = null;
		}
	}

	function startLoadingPoll() {
		clearPoll();
		intervalId = setInterval(async () => {
			const complete = await pollFeed();
			if (complete) {
				clearPoll();
				startRefreshPoll();
			}
		}, 2000);
	}

	function startRefreshPoll() {
		clearPoll();
		intervalId = setInterval(pollFeed, 60000);
	}

	async function handleLogout() {
		await fetch('/auth/logout', { method: 'POST', credentials: 'include' });
		window.location.href = '/';
	}

	onMount(() => {
		pollFeed().then((complete) => {
			if (complete) startRefreshPoll();
			else startLoadingPoll();
		});

		return () => clearPoll();
	});
</script>

<div class="feed-page">
	<div class="header">
		<h1>SoundCloud Digger</h1>
		<button class="logout" onclick={handleLogout}>Logout</button>
	</div>

	<ControlsBar />

	{#if error}
		<div class="error">
			<p>{error}</p>
			<button onclick={pollFeed}>Retry</button>
		</div>
	{:else if !$loadingComplete}
		<LoadingIndicator totalCount={$totalCount} />
	{/if}

	<TrackList tracks={$filteredFeed} />
</div>

<style>
	.feed-page {
		max-width: 800px;
		margin: 0 auto;
		padding: 16px;
		display: flex;
		flex-direction: column;
		gap: 12px;
	}
	.header {
		display: flex;
		justify-content: space-between;
		align-items: center;
	}
	h1 {
		color: #e94560;
		font-size: 24px;
		margin: 0;
	}
	.logout {
		background: transparent;
		border: 1px solid #333;
		color: #888;
		padding: 6px 12px;
		border-radius: 4px;
		cursor: pointer;
	}
	.error {
		text-align: center;
		color: #e94560;
		padding: 16px;
	}
</style>
```

- [ ] **Step 3: Add global styles**

Write `frontend/src/app.html`:

```html
<!doctype html>
<html lang="en">
	<head>
		<meta charset="utf-8" />
		<meta name="viewport" content="width=device-width, initial-scale=1" />
		<title>SoundCloud Digger</title>
		<style>
			body {
				margin: 0;
				font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
				background: #1a1a2e;
				color: #eee;
			}
		</style>
		%sveltekit.head%
	</head>
	<body>
		<div>%sveltekit.body%</div>
	</body>
</html>
```

- [ ] **Step 4: Configure SvelteKit proxy for dev**

Write `frontend/svelte.config.js`:

```javascript
import adapter from '@sveltejs/adapter-auto';
import { vitePreprocess } from '@sveltejs/kit/vite';

/** @type {import('@sveltejs/kit').Config} */
const config = {
	preprocess: vitePreprocess(),
	kit: {
		adapter: adapter(),
	},
};

export default config;
```

Update `frontend/vite.config.ts` to add API proxy:

```typescript
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vitest/config';

export default defineConfig({
	plugins: [sveltekit()],
	test: {
		include: ['tests/**/*.test.ts'],
		environment: 'jsdom',
	},
	server: {
		proxy: {
			'/api': 'http://localhost:5000',
			'/auth': 'http://localhost:5000',
		},
	},
});
```

- [ ] **Step 5: Verify it builds**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npm run build
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/routes/ frontend/src/app.html frontend/svelte.config.js frontend/vite.config.ts
git commit -m "feat: add login page, feed page with polling, and dev proxy config"
```

---

### Task 15: Run all tests and verify

- [ ] **Step 1: Run all backend tests**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend
dotnet test --verbosity normal
```

Expected: All 28 tests PASS.

- [ ] **Step 2: Run all frontend tests**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend
npx vitest run
```

Expected: All 7 tests PASS.

- [ ] **Step 3: Verify both projects build cleanly**

```bash
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/backend && dotnet build
cd /Users/highfiveghost/conductor/workspaces/soundcloud-digger/sao-paulo/frontend && npm run build
```

Expected: Both build without errors.
