namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;

/// <summary>Serialises writes of the whole plugin state; callers just say "save now".</summary>
internal sealed class StatePersister(StateStore store, Func<SubState> snapshot) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task PersistAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await store.SaveAsync(snapshot());
        }
        finally
        {
            _lock.Release();
        }
    }

    public void PersistSync()
    {
        _lock.Wait();
        try
        {
            store.SaveSync(snapshot());
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();
}
