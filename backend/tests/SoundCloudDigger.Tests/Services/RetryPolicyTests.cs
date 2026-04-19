using System.Net;
using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Services;

public class RetryPolicyTests
{
    [Fact]
    public async Task Execute_ReturnsResultOnFirstSuccess()
    {
        var policy = new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(1));
        var calls = 0;

        var result = await policy.ExecuteAsync(async ct =>
        {
            calls++;
            return "ok";
        }, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Execute_RetriesOn5xx()
    {
        var policy = new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(1));
        var calls = 0;

        var result = await policy.ExecuteAsync(async ct =>
        {
            calls++;
            if (calls < 3) throw new HttpRequestException("fail", null, HttpStatusCode.InternalServerError);
            return "ok";
        }, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Execute_DoesNotRetryOn4xxOtherThan429()
    {
        var policy = new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(1));
        var calls = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(async ct =>
            {
                calls++;
                throw new HttpRequestException("nope", null, HttpStatusCode.NotFound);
            }, CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Execute_ThrowsAfterMaxAttempts()
    {
        var policy = new RetryPolicy(maxAttempts: 2, baseDelay: TimeSpan.FromMilliseconds(1));
        var calls = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<string>(async ct =>
            {
                calls++;
                throw new HttpRequestException("fail", null, HttpStatusCode.ServiceUnavailable);
            }, CancellationToken.None));

        Assert.Equal(2, calls);
    }
}
