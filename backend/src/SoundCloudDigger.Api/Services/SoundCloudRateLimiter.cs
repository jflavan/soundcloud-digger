namespace SoundCloudDigger.Api.Services;

public class SoundCloudRateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private DateTimeOffset _throttledUntil = DateTimeOffset.MinValue;
    private readonly object _throttleLock = new();

    public SoundCloudRateLimiter(int maxConcurrent = 6)
    {
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public void ReportThrottle(DateTimeOffset until)
    {
        lock (_throttleLock)
        {
            if (until > _throttledUntil) _throttledUntil = until;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        DateTimeOffset throttle;
        lock (_throttleLock) { throttle = _throttledUntil; }

        var now = DateTimeOffset.UtcNow;
        if (throttle > now)
        {
            var delay = throttle - now;
            await Task.Delay(delay, ct);
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            return await action(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
