using SoundCloudDigger.Api.Services;

namespace SoundCloudDigger.Tests.Services;

public class SoundCloudRateLimiterTests
{
    [Fact]
    public async Task Execute_RunsActionThroughSemaphore()
    {
        var limiter = new SoundCloudRateLimiter(maxConcurrent: 2);
        var ran = false;

        await limiter.ExecuteAsync(async ct =>
        {
            ran = true;
            await Task.CompletedTask;
            return 0;
        }, CancellationToken.None);

        Assert.True(ran);
    }

    [Fact]
    public async Task Execute_LimitsConcurrency()
    {
        var limiter = new SoundCloudRateLimiter(maxConcurrent: 2);
        var inFlight = 0;
        var peak = 0;
        var tasks = Enumerable.Range(0, 10).Select(_ => limiter.ExecuteAsync<int>(async ct =>
        {
            var current = Interlocked.Increment(ref inFlight);
            int observed;
            do
            {
                observed = peak;
            } while (current > observed && Interlocked.CompareExchange(ref peak, current, observed) != observed);
            await Task.Delay(20, ct);
            Interlocked.Decrement(ref inFlight);
            return 0;
        }, CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks);

        Assert.True(peak <= 2, $"peak was {peak}");
    }

    [Fact]
    public async Task ReportThrottle_PausesAllCallers()
    {
        var limiter = new SoundCloudRateLimiter(maxConcurrent: 4);
        var until = DateTimeOffset.UtcNow.AddMilliseconds(100);
        limiter.ReportThrottle(until);

        var start = DateTimeOffset.UtcNow;
        await limiter.ExecuteAsync<int>(ct => Task.FromResult(0), CancellationToken.None);
        var elapsed = DateTimeOffset.UtcNow - start;

        Assert.True(elapsed.TotalMilliseconds >= 80, $"elapsed was {elapsed.TotalMilliseconds}");
    }
}
