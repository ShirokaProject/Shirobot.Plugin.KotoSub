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
                    AtomicJsonFile.Write(_subscriptionsPath, new SubscriptionsDocument
                    {
                        Targets = new(), GroupAliases = new(), FriendAliases = new()
                    });
                    BotLog.Info("[KotoSub] subscriptions.json 不存在，已创建空订阅文件，可使用 #sub add 添加订阅。");
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        var subscriptions = AtomicJsonFile.Read<SubscriptionsDocument>(_subscriptionsPath);
        var targetDefinitions = subscriptions.Targets ?? subscriptions.LegacyFeeds;
        if (targetDefinitions is null)
            throw new InvalidDataException("subscriptions.json 缺少必要的 targets 字段。");

        var upgraded = subscriptions.Version < SubscriptionsDocument.CurrentVersion ||
                       subscriptions.Targets is null ||
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

        var checkpoints = new Dictionary<string, PersistentTargetCheckpoint>(
            runtime.Targets ?? runtime.LegacyFeeds ?? new(), StringComparer.OrdinalIgnoreCase);
        var feeds = new Dictionary<string, PersistentTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, definition) in targetDefinitions)
        {
            var targetKey = definition?.Key ?? definition?.LegacyUrl;
            if (definition is null || string.IsNullOrWhiteSpace(targetKey))
                throw new InvalidDataException($"subscriptions.json 的 target={id} 缺少 key。");

            checkpoints.TryGetValue(id, out var checkpoint);
            feeds[id] = new PersistentTarget
            {
                Key = targetKey,
                ProviderId = definition.LegacyProviderId ?? definition.ProviderId,
                DisplayName = checkpoint?.DisplayName,
                SourceImageUrl = checkpoint?.SourceImageUrl ?? checkpoint?.LegacySourceImageUrl,
                Metadata = MergeMetadata(checkpoint?.Metadata, checkpoint?.LegacyGenerator),
                IntervalSeconds = definition.IntervalSeconds,
                CreatedBy = definition.CreatedBy,
                CreatedAt = definition.CreatedAt,
                SeenUpdateIds = (checkpoint?.SeenUpdateIds ?? checkpoint?.LegacySeenUpdateIds)?.ToList() ?? [],
                LastCheckedAt = checkpoint?.LastCheckedAt ?? checkpoint?.LegacyLastCheckedAt,
                ETag = checkpoint?.ETag,
                LastModified = checkpoint?.LastModified,
                ConsecutiveFailures = checkpoint?.ConsecutiveFailures ?? 0,
                ProviderState = checkpoint?.ProviderState ?? new(StringComparer.Ordinal)
            };
        }

        return new SubState
        {
            Targets = feeds,
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
        Targets = state.Targets.ToDictionary(
            feed => feed.Key,
            feed => new PersistentTargetDefinition
            {
                Key = feed.Value.Key,
                ProviderId = feed.Value.ProviderId,
                IntervalSeconds = feed.Value.IntervalSeconds,
                CreatedBy = feed.Value.CreatedBy,
                CreatedAt = feed.Value.CreatedAt
            }, StringComparer.OrdinalIgnoreCase),
        GroupAliases = state.GroupSubs,
        FriendAliases = state.FriendSubs
    };

    private static RuntimeDocument ToRuntimeDocument(SubState state) => new()
    {
        Targets = state.Targets.ToDictionary(
            feed => feed.Key,
            feed => new PersistentTargetCheckpoint
            {
                DisplayName = feed.Value.DisplayName,
                SourceImageUrl = feed.Value.SourceImageUrl,
                Metadata = new(feed.Value.Metadata, StringComparer.OrdinalIgnoreCase),
                SeenUpdateIds = feed.Value.SeenUpdateIds?.ToList() ?? [],
                LastCheckedAt = feed.Value.LastCheckedAt,
                ETag = feed.Value.ETag,
                LastModified = feed.Value.LastModified,
                ConsecutiveFailures = feed.Value.ConsecutiveFailures,
                ProviderState = new(feed.Value.ProviderState, StringComparer.Ordinal)
            }, StringComparer.OrdinalIgnoreCase),
        DeliveryReceipts = state.DeliveryReceipts
    };

    private static Dictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string>? metadata, string? legacyGenerator)
    {
        var result = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(legacyGenerator)) result["generator"] = legacyGenerator;
        return result;
    }
}
