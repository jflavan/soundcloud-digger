using System.Net;

namespace SoundCloudDigger.Api.Services;

public class RetryPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;

    public RetryPolicy(int maxAttempts = 3, TimeSpan? baseDelay = null)
    {
        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(1);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                return await action(ct);
            }
            catch (HttpRequestException ex) when (IsRetryable(ex) && attempt < _maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, ct);
            }
        }
        // Unreachable: the loop either returns or rethrows.
        throw new InvalidOperationException("RetryPolicy exhausted without returning or throwing.");
    }

    private static bool IsRetryable(HttpRequestException ex)
    {
        if (ex.StatusCode is null) return true; // network / timeout
        var code = (int)ex.StatusCode.Value;
        if (code >= 500 && code < 600) return true;
        if (code == 429) return true;
        return false;
    }
}
