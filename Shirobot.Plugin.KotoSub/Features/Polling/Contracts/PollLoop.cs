namespace Shirobot.Plugin.KotoSub.Features.Polling.Contracts;

/// <summary>Once a second, starts a check for every feed that is due.</summary>
internal sealed class PollLoop(
    FeedRegistry feeds, BaselineTracker baselines, DeliveryReceiptStore receipts,
    FeedCheckRunner runner, Func<KotoSubConfig> config) : IDisposable
{
    private readonly Dictionary<string, Task> _activeChecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _activeLock = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public void Start()
    {
        if (_loopTask is not null) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loopTask = Task.Run(() => RunAsync(token), token);
    }

    /// <summary>Returns false if the loop or a running check did not finish within five seconds each.</summary>
    public async Task<bool> StopAsync()
    {
        if (_cts is null) return true;

        try { _cts.Cancel(); } catch (ObjectDisposedException) { }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                BotLog.Warning("[KotoSub] 调度循环未能在 5 秒内停止。");
                return false;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                BotLog.Error($"[KotoSub] 停止调度循环时异常: {ex}");
            }
        }

        Task[] active;
        lock (_activeLock) active = _activeChecks.Values.ToArray();
        try
        {
            await Task.WhenAll(active).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            lock (_activeLock) BotLog.Warning($"[KotoSub] 停止时仍有 {_activeChecks.Count} 个检查未结束。");
            return false;
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
        return true;
    }

    /// <summary>Reloads receipts and makes every feed re-learn its baseline (used after a state reload).</summary>
    public void Reset(Dictionary<string, Dictionary<string, List<string>>> savedReceipts, IEnumerable<string> feedIds)
    {
        receipts.Load(savedReceipts);
        baselines.Reset(feedIds);
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        BotLog.Info("[KotoSub] 调度器已启动。");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Tick(cancellationToken);
                }
                catch (Exception ex)
                {
                    BotLog.Error($"[KotoSub] 调度器迭代异常: {ex}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            BotLog.Info("[KotoSub] 调度器已停止。");
        }
    }

    private void Tick(CancellationToken cancellationToken)
    {
        var settings = config();
        if (!settings.Enabled) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var feed in feeds.All())
        {
            var (pending, attempted) = baselines.Status(feed.Id);
            var force = pending && !attempted;
            if (!force && BackoffPolicy.NextDueAt(feed, settings) > now) continue;

            lock (_activeLock)
            {
                if (_activeChecks.ContainsKey(feed.Id)) continue;
                if (pending) baselines.MarkAttempt(feed.Id);
                _activeChecks[feed.Id] = Task.Run(() => CheckAsync(feed, pending, cancellationToken), CancellationToken.None);
            }
        }
    }

    private async Task CheckAsync(FeedSource feed, bool baselineMode, CancellationToken cancellationToken)
    {
        try
        {
            await runner.RunAsync(feed, baselineMode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            BotLog.Error($"[KotoSub] feed={feed.Id} 检查异常: {ex}");
        }
        finally
        {
            lock (_activeLock) _activeChecks.Remove(feed.Id);
        }
    }
}
