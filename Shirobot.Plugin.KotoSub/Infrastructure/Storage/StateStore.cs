using System.Text.Json;
using ShiroBot.SDK.Plugin;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;

internal sealed class StateStore
{
    private readonly string _statePath;
    private readonly string _subscriptionsPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public StateStore(string pluginConfigDirectory)
    {
        _statePath = Path.Combine(pluginConfigDirectory, "state.json");
        _subscriptionsPath = Path.Combine(pluginConfigDirectory, "subscriptions.json");
    }

    public string FilePath => _statePath;
    public string SubscriptionsFilePath => _subscriptionsPath;

    public SubState Load()
    {
        if (!File.Exists(_subscriptionsPath))
        {
            _writeLock.Wait();
            try
            {
                if (!File.Exists(_subscriptionsPath))
                {
                    AtomicJsonFile.Write(_subscriptionsPath, new SubscriptionsDocument { GroupAliases = new(), FriendAliases = new() });
                    BotLog.Info("[KotoSub] subscriptions.json 不存在，已创建空订阅文件，可使用 #sub add 添加订阅。");
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        var subscriptions = AtomicJsonFile.Read<SubscriptionsDocument>(_subscriptionsPath);
        if (subscriptions.Feeds is null)
            throw new InvalidDataException("subscriptions.json 缺少必要的订阅字段。");

        var upgraded = subscriptions.Version < SubscriptionsDocument.CurrentVersion ||
                       subscriptions.GroupAliases is null || subscriptions.FriendAliases is null;
        var groupAliases = subscriptions.GroupAliases ?? UpgradeV1(subscriptions.GroupSubs);
        var friendAliases = subscriptions.FriendAliases ?? UpgradeV1(subscriptions.FriendSubs);
        if (upgraded) BackupBeforeUpgrade();

        RuntimeDocument runtime;
        try
        {
            runtime = File.Exists(_statePath)
                ? AtomicJsonFile.Read<RuntimeDocument>(_statePath)
                : new RuntimeDocument();
        }
        catch (Exception ex)
        {
            BotLog.Warning($"[KotoSub] 加载 state.json 失败，运行状态将重新建立基线，订阅保持不变: {ex.GetType().Name}: {ex.Message}");
            runtime = new RuntimeDocument();
        }

        var checkpoints = new Dictionary<string, PersistentFeedCheckpoint>(
            runtime.Feeds ?? new(), StringComparer.OrdinalIgnoreCase);
        var feeds = new Dictionary<string, PersistentFeed>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, definition) in subscriptions.Feeds)
        {
            if (definition is null || string.IsNullOrWhiteSpace(definition.Url))
                throw new InvalidDataException($"subscriptions.json 的 feed={id} 缺少 URL。");

            checkpoints.TryGetValue(id, out var checkpoint);
            feeds[id] = new PersistentFeed
            {
                Url = definition.Url,
                SourceKind = definition.SourceKind,
                DisplayName = checkpoint?.DisplayName,
                FeedImageUrl = checkpoint?.FeedImageUrl,
                Generator = checkpoint?.Generator,
                IntervalSeconds = definition.IntervalSeconds,
                CreatedBy = definition.CreatedBy,
                CreatedAt = definition.CreatedAt,
                LastSeenGuids = checkpoint?.LastSeenGuids?.ToList() ?? [],
                LastFetchAt = checkpoint?.LastFetchAt,
                ETag = checkpoint?.ETag,
                LastModified = checkpoint?.LastModified,
                ConsecutiveFailures = checkpoint?.ConsecutiveFailures ?? 0
            };
        }

        return new SubState
        {
            Feeds = feeds,
            GroupSubs = groupAliases,
            FriendSubs = friendAliases,
            DeliveryReceipts = (runtime.DeliveryReceipts ?? new())
                .Where(receipt => feeds.ContainsKey(receipt.Key))
                .ToDictionary(receipt => receipt.Key, receipt => receipt.Value, StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task SaveAsync(SubState state, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await AtomicJsonFile.WriteAsync(_statePath, ToRuntimeDocument(state), cancellationToken);
        }
        catch (Exception ex)
        {
            BotLog.Error($"[KotoSub] 保存 state.json 失败: {ex}");
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void SaveSync(SubState state)
    {
        _writeLock.Wait();
        try
        {
            AtomicJsonFile.Write(_subscriptionsPath, ToSubscriptionsDocument(state));
            AtomicJsonFile.Write(_statePath, ToRuntimeDocument(state));
        }
        catch (Exception ex)
        {
            BotLog.Error($"[KotoSub] 保存订阅或运行状态失败: {ex}");
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // v1 stored plain feed ids per subscriber. Each id becomes that subscriber's own alias for the same feed,
    // so existing commands keep working while the alias is no longer shared across subscribers.
    private static Dictionary<string, Dictionary<string, string>> UpgradeV1(Dictionary<string, List<string>>? legacy) =>
        (legacy ?? new Dictionary<string, List<string>>())
        .Where(pair => pair.Value is { Count: > 0 })
        .ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(id => id, id => id, StringComparer.OrdinalIgnoreCase));

    private void BackupBeforeUpgrade()
    {
        var backup = _subscriptionsPath + ".v1.bak";
        try
        {
            if (!File.Exists(backup))
            {
                File.Copy(_subscriptionsPath, backup);
                BotLog.Info($"[KotoSub] 订阅文件升级到新格式，旧文件已备份为 {Path.GetFileName(backup)}。");
            }
        }
        catch (Exception ex) when (AtomicJsonFile.IsFileBusy(ex))
        {
            BotLog.Warning($"[KotoSub] 备份旧订阅文件失败: {ex.GetType().Name}");
        }
    }

    private static SubscriptionsDocument ToSubscriptionsDocument(SubState state) => new()
    {
        Feeds = state.Feeds.ToDictionary(
            feed => feed.Key,
            feed => new PersistentFeedDefinition
            {
                Url = feed.Value.Url,
                SourceKind = feed.Value.SourceKind,
                IntervalSeconds = feed.Value.IntervalSeconds,
                CreatedBy = feed.Value.CreatedBy,
                CreatedAt = feed.Value.CreatedAt
            }, StringComparer.OrdinalIgnoreCase),
        GroupAliases = state.GroupSubs,
        FriendAliases = state.FriendSubs
    };

    private static RuntimeDocument ToRuntimeDocument(SubState state) => new()
    {
        Feeds = state.Feeds.ToDictionary(
            feed => feed.Key,
            feed => new PersistentFeedCheckpoint
            {
                DisplayName = feed.Value.DisplayName,
                FeedImageUrl = feed.Value.FeedImageUrl,
                Generator = feed.Value.Generator,
                LastSeenGuids = feed.Value.LastSeenGuids?.ToList() ?? [],
                LastFetchAt = feed.Value.LastFetchAt,
                ETag = feed.Value.ETag,
                LastModified = feed.Value.LastModified,
                ConsecutiveFailures = feed.Value.ConsecutiveFailures
            }, StringComparer.OrdinalIgnoreCase),
        DeliveryReceipts = state.DeliveryReceipts
    };
}
