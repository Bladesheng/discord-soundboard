namespace Bot.Utils;

public class AsyncLock
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public async Task<Lock> AcquireAsync()
    {
        await _semaphoreSlim.WaitAsync();
        return new Lock(_semaphoreSlim);
    }

    public class Lock(SemaphoreSlim semaphoreSlim) : IDisposable
    {
        public void Dispose()
        {
            semaphoreSlim.Release();
        }
    }
}