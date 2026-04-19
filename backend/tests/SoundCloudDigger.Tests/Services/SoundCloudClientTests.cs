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
}
