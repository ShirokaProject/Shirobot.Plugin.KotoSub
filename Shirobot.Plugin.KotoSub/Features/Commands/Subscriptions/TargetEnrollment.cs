namespace Shirobot.Plugin.KotoSub.Features.Commands.Subscriptions;

/// <summary>Creates the shared feed entry for an address nobody followed yet.</summary>
internal sealed class TargetEnrollment(TargetRegistry targets, Func<KotoSubConfig> config)
{
    public SubscriptionTarget? Find(string providerId, string targetKey) => targets.Find(providerId, targetKey);

    /// <summary>Adds the feed and runs <paramref name="subscribe"/> atomically; false means somebody else just added it.</summary>
    public bool TryEnroll(
        string providerId, string targetKey, SourceUpdateBatch fetched, string createdBy,
        Action<SubscriptionTarget> subscribe, out SubscriptionTarget target) =>
        targets.TryAddInitialized(
            TargetIdentity.IdFor(providerId, targetKey), providerId, targetKey, fetched.SourceName, createdBy,
            fetched.Items, config().LastSeenCapacity, out target, subscribe, fetched.SourceImageUrl, fetched.Metadata);
}
