using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using RichardSzalay.MockHttp;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Services;

public class SoundCloudClientTests
{
    private readonly IConfiguration _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SoundCloud:ClientId"] = "cid",
            ["SoundCloud:ClientSecret"] = "csec",
        }).Build();

    [Fact]
    public async Task GetFollowings_ReturnsCollectionAndPagesUntilExhausted()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("https://api.soundcloud.com/me/followings*")
            .Respond("application/json", """
                {"collection":[{"urn":"soundcloud:users:1","username":"a"}],"next_href":null}
                """);

        var client = new SoundCloudClient(
            new HttpClient(mock), _config,
            new SoundCloudRateLimiter(4), new RetryPolicy(1, TimeSpan.FromMilliseconds(1)));

        var result = await client.GetFollowings("token", nextHref: null);

        Assert.Single(result.Collection);
        Assert.Equal("soundcloud:users:1", result.Collection[0].Urn);
    }

    [Fact]
    public async Task GetUserReposts_Returns429AsHttpRequestExceptionAndReportsReset()
    {
        var mock = new MockHttpMessageHandler();
        mock.When("https://api.soundcloud.com/users/*/reposts/tracks*")
            .Respond(req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """{"rate_limit":{"reset_time":"2099-01-01T00:00:00Z"}}""",
                        Encoding.UTF8, "application/json"),
                };
                return resp;
            });

        var limiter = new SoundCloudRateLimiter(4);
        var client = new SoundCloudClient(
            new HttpClient(mock), _config,
            limiter, new RetryPolicy(1, TimeSpan.FromMilliseconds(1)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetUserReposts("soundcloud:users:1", "token", null));
    }

    private SoundCloudClient Build(MockHttpMessageHandler mock) => new(
        new HttpClient(mock), _config,
        new SoundCloudRateLimiter(4), new RetryPolicy(1, TimeSpan.FromMilliseconds(1)));

    private const string TokenJson = """
        {"access_token":"at","refresh_token":"rt","expires_in":3600,"scope":"non-expiring"}
        """;

    [Fact]
    public async Task ExchangeCodeForToken_PostsAuthorizationCodeGrant()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
            .WithFormData(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "cid",
                ["client_secret"] = "csec",
                ["redirect_uri"] = "http://cb",
                ["code_verifier"] = "ver",
                ["code"] = "abc",
            })
            .Respond("application/json", TokenJson);

        var result = await Build(mock).ExchangeCodeForToken("abc", "ver", "http://cb");

        Assert.Equal("at", result.AccessToken);
        Assert.Equal("rt", result.RefreshToken);
        Assert.Equal(3600, result.ExpiresIn);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task ExchangeCodeForToken_ThrowsOnNonSuccess()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
            .Respond(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Build(mock).ExchangeCodeForToken("bad", "ver", "http://cb"));
    }

    [Fact]
    public async Task RefreshAccessToken_PostsRefreshTokenGrant()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
            .WithFormData(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = "cid",
                ["client_secret"] = "csec",
                ["refresh_token"] = "old_rt",
            })
            .Respond("application/json", TokenJson);

        var result = await Build(mock).RefreshAccessToken("old_rt");

        Assert.Equal("at", result.AccessToken);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task RefreshAccessToken_ThrowsOnNonSuccess()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, "https://secure.soundcloud.com/oauth/token")
            .Respond(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<HttpRequestException>(() => Build(mock).RefreshAccessToken("rt"));
    }

    [Fact]
    public async Task SignOut_PostsAccessTokenAsJson()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://secure.soundcloud.com/sign-out")
            .WithContent("""{"access_token":"tok"}""")
            .Respond(HttpStatusCode.OK);

        await Build(mock).SignOut("tok");

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task SignOut_ThrowsOnNonSuccess()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, "https://secure.soundcloud.com/sign-out")
            .Respond(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => Build(mock).SignOut("tok"));
    }

    [Fact]
    public async Task GetMe_SendsOAuthHeaderAndDeserializesUser()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.soundcloud.com/me")
            .WithHeaders("Authorization", "OAuth token")
            .Respond("application/json", """{"urn":"soundcloud:users:9","username":"me"}""");

        var me = await Build(mock).GetMe("token");

        Assert.Equal("soundcloud:users:9", me.Urn);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetFeedTracks_UsesNextHrefWhenProvided()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.soundcloud.com/me/feed/tracks?cursor=xyz")
            .Respond("application/json", """{"collection":[],"next_href":null}""");

        var result = await Build(mock).GetFeedTracks("token", 200, "https://api.soundcloud.com/me/feed/tracks?cursor=xyz");

        Assert.Empty(result.Collection);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task ResolveTrackUrn_ReadsTheUrnFromTheRedirectLocation()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.soundcloud.com/resolve")
            .WithQueryString("url", "https://soundcloud.com/artist/song")
            .WithHeaders("Authorization", "OAuth token")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.Found);
                r.Headers.Location = new Uri("https://api.soundcloud.com/tracks/soundcloud:tracks:123");
                return r;
            });

        var urn = await Build(mock).ResolveTrackUrn("https://soundcloud.com/artist/song", "token");

        Assert.Equal("soundcloud:tracks:123", urn);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task ResolveTrackUrn_StripsTrackingQueryAndReturnsNullOnNotFound()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.soundcloud.com/resolve")
            .WithQueryString("url", "https://soundcloud.com/artist/gone")
            .Respond(HttpStatusCode.NotFound);

        var urn = await Build(mock).ResolveTrackUrn("https://soundcloud.com/artist/gone?utm_source=api", "token");

        Assert.Null(urn);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetTrackStreams_DeserializesStreamUrls()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.soundcloud.com/tracks/soundcloud:tracks:123/streams")
            .WithHeaders("Authorization", "OAuth token")
            .Respond("application/json", """{"preview_mp3_128_url":"https://api.soundcloud.com/tracks/soundcloud:tracks:123/streams/abc"}""");

        var streams = await Build(mock).GetTrackStreams("soundcloud:tracks:123", "token");

        Assert.Null(streams.HttpMp3128Url);
        Assert.Equal("https://api.soundcloud.com/tracks/soundcloud:tracks:123/streams/abc", streams.PreviewMp3128Url);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetStreamRedirect_ReturnsTheSignedCdnLocation()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Get, "https://api.soundcloud.com/tracks/soundcloud:tracks:123/streams/abc")
            .WithHeaders("Authorization", "OAuth token")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.Found);
                r.Headers.Location = new Uri("https://cf-preview-media.sndcdn.com/p.mp3?Policy=x&Signature=y");
                return r;
            });

        var cdn = await Build(mock).GetStreamRedirect("https://api.soundcloud.com/tracks/soundcloud:tracks:123/streams/abc", "token");

        Assert.Equal("https://cf-preview-media.sndcdn.com/p.mp3?Policy=x&Signature=y", cdn);
    }

    [Fact]
    public async Task GetStreamRedirect_ReturnsNullWhenNotRedirected()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, "https://api.soundcloud.com/tracks/*/streams/*")
            .Respond(HttpStatusCode.NotFound);

        var cdn = await Build(mock).GetStreamRedirect("https://api.soundcloud.com/tracks/soundcloud:tracks:123/streams/abc", "token");

        Assert.Null(cdn);
    }
}
