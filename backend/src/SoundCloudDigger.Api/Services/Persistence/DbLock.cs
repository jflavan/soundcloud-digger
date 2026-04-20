namespace SoundCloudDigger.Api.Services.Persistence;

// The whole app shares one SqliteConnection, and Microsoft.Data.Sqlite connections
// are not thread-safe. This serializes every access so concurrent feed + discover
// fetches can't trample each other mid-transaction.
public sealed class DbLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public Releaser Acquire()
    {
        _semaphore.Wait();
        return new Releaser(_semaphore);
    }

    public async ValueTask<Releaser> AcquireAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    public readonly struct Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        internal Releaser(SemaphoreSlim semaphore) { _semaphore = semaphore; }
        public void Dispose() => _semaphore.Release();
    }
}
