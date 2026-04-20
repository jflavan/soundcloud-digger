using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
        _sut = new FeedController(_mockCache.Object, Mock.Of<IServiceScopeFactory>());

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
