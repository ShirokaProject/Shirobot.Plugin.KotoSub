namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

internal sealed class AddSubscription(
    SourceCatalog sources, OnDemandFetch fetch, TargetEnrollment enrollment,
    SubscriptionRegistry subscriptions, StatePersister persister) : ISubCommand
{
    public IReadOnlyList<string> Names { get; } = ["add"];
    public string Usage => "add <url> [别名]";
    public string Summary => "添加地址并订阅";
    public CommandAccess Access => CommandAccess.Manager;

    public async Task HandleAsync(CommandCall call)
    {
        if (call.Args.Count is < 1 or > 2 ||
            !Uri.TryCreate(call.Args[0].Trim(), UriKind.Absolute, out var parsed) ||
            string.IsNullOrWhiteSpace(parsed.Scheme))
        {
            await call.ReplyUsageAsync(this);
            return;
        }

        ISubscriptionSource source;
        try
        {
            source = sources.ForAddress(call.Args[0].Trim());
        }
        catch (ArgumentException ex)
        {
            await call.ReplyMentionAsync($"[订阅] {ex.Message}");
            return;
        }

        var address = source.Normalize(parsed);
        var targetKey = address.TargetKey;
        var requested = call.Args.Count == 2 ? FeedIdGenerator.Sanitize(call.Args[1]) : null;
        if (requested is not null && AliasPolicy.IsTaken(subscriptions, call.Scope, requested))
        {
            await call.ReplyMentionAsync($"[订阅] 别名 {requested} 已被当前会话使用，请换一个。");
            return;
        }

        var existing = enrollment.Find(source.Id, targetKey);
        if (existing is not null)
        {
            await SubscribeAsync(existing, requested, source, address, call);
            return;
        }

        SourceUpdateBatch fetched;
        try
        {
            fetched = await fetch.FetchNewAsync(source, targetKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            BotLog.Warning($"[KotoSub] add 验证订阅源失败 provider={source.Id}: {ex.GetType().Name}: {ex.Message}");
            await call.ReplyMentionAsync($"[订阅] 该地址不是有效的订阅源或当前无法访问，请重新添加。\n原因: {ex.Message}");
            return;
        }

        existing = enrollment.Find(source.Id, targetKey);
        if (existing is not null)
        {
            await SubscribeAsync(existing, requested, source, address, call);
            return;
        }

        var alias = requested ?? AliasPolicy.Choose(subscriptions, call.Scope, address.SuggestedAlias, targetKey);
        if (!enrollment.TryEnroll(source.Id, targetKey, fetched, call.Scope.Format(),
                feed => subscriptions.Add(call.Scope, alias, feed.Id), out var added))
        {
            await SubscribeAsync(added, requested, source, address, call);
            return;
        }

        persister.PersistSync();
        await ReplySubscribedAsync(alias, source, targetKey, call);
    }

    private async Task SubscribeAsync(
        SubscriptionTarget feed, string? requested, ISubscriptionSource source, AddressInput address, CommandCall call)
    {
        var alias = requested ?? AliasPolicy.Choose(subscriptions, call.Scope, address.SuggestedAlias, feed.Key);
        var outcome = subscriptions.Add(call.Scope, alias, feed.Id);
        switch (outcome.Status)
        {
            case AddStatus.AlreadySubscribed:
                await call.ReplyMentionAsync($"[订阅] 当前会话已订阅该地址（别名：{outcome.Alias}）。");
                return;
            case AddStatus.AliasTaken:
                await call.ReplyMentionAsync($"[订阅] 别名 {alias} 已被当前会话使用，请换一个。");
                return;
        }

        persister.PersistSync();
        await ReplySubscribedAsync(alias, source, feed.Key, call);
    }

    private static Task ReplySubscribedAsync(string alias, ISubscriptionSource source, string url, CommandCall call)
    {
        var typed = call.Args[0].Trim();
        var normalized = string.Equals(typed, url, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"\n已转换订阅地址为: {url}";
        return call.ReplyMentionAsync(
            $"[订阅] 已订阅 {alias}（{source.DisplayName}），下次新增条目将推送到本会话。{normalized}");
    }
}
