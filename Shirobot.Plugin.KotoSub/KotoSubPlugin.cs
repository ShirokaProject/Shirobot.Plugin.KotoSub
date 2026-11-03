using ShiroBot.SDK.Core;
using ShiroBot.SDK.Models;
using ShiroBot.SDK.Plugin;
using Shirobot.Plugin.KotoSub.Features.Commands;

[assembly: RequiresShiroBotPackage("shirobot.model.qq", MinimumVersion = "1.0.0")]

namespace Shirobot.Plugin.KotoSub;

[BotPlugin(
    "Shirobot.Plugin.KotoSub",
    Name = "KotoSub",
    Version = "0.3.0",
    Description = "订阅推送插件，支持 RSS / Atom 等订阅源。",
    Author = "PVPGOOD",
    GithubRepo = "ShirokaProject/Shirobot.Plugin.KotoSub",
    Category = PluginCategory.Integration,
    SharedAssemblies = "ShiroBot.Model.QQ")]
public sealed class KotoSubPlugin : PluginBase<KotoSubConfig>
{
    private KotoSubConfig _config = new();
    private HttpClient? _http;
    private HttpClient? _feedHttp;
    private StateStore? _store;
    private KotoSubComposition? _app;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public override string Name => "KotoSub";

    protected override Task OnConfigChangedAsync(KotoSubConfig previous, KotoSubConfig current, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _config = current;
        BotLog.Info("[KotoSub] 配置已热重载。");
        return Task.CompletedTask;
    }

    protected override void ConfigureRoutes()
    {
        DirectCommands.MapWhen(IsCommand, message => _app is { } app && _config.Enabled
            ? app.Router.HandleDirectAsync(message) : Task.CompletedTask);
        GroupCommands.MapWhen(IsCommand, message => _app is { } app && _config.Enabled
            ? app.Router.HandleGroupAsync(message) : Task.CompletedTask);
        Events.Map<MessageEvent>(message =>
        {
            _app?.Dispatcher.RegisterIncoming(message);
            return Task.CompletedTask;
        });
    }

    protected override Task LoadAsync()
    {
        BotLog.Info("[KotoSub] 开始初始化。");

        _config = Settings;
        Context.Config.Save(_config);

        _store = new StateStore(Path.GetDirectoryName(Context.Config.ConfigPath) ?? AppContext.BaseDirectory);
        var state = _store.Load();
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _feedHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        _app = KotoSubComposition.Create(new CompositionOptions(
            Context, () => _config, SaveConfig, ReloadAsync, _http, _feedHttp, _store, state, Context.Render));
        _app.Poll.Start();

        BotLog.Success(
            $"[KotoSub] 初始化完成。feeds={state.Feeds.Count}, groups={state.GroupSubs.Count}, " +
            $"friends={state.FriendSubs.Count}, interval={_config.DefaultIntervalSeconds}s");

        if (!_config.Enabled)
            BotLog.Warning("[KotoSub] 插件未启用，请在 config.toml 中将 enabled 设为 true。");

        return Task.CompletedTask;
    }

    protected override async Task OnUnloadAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            if (_app is not null)
            {
                if (!await _app.Poll.StopAsync())
                {
                    BotLog.Warning("[KotoSub] 有检查任务未结束，本次卸载跳过最终保存和资源释放，避免写入过期状态。");
                    return;
                }

                try
                {
                    _app.Persister.PersistSync();
                }
                catch (Exception ex)
                {
                    BotLog.Error($"[KotoSub] 卸载时保存状态失败: {ex}");
                }

                _app.Dispose();
                _app = null;
            }

            _http?.Dispose();
            _http = null;
            _feedHttp?.Dispose();
            _feedHttp = null;
            BotLog.Info("[KotoSub] 已卸载。");
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static bool IsCommand(MessageEvent message) => SubCommandRouter.IsCommand(message.GetPlainText());

    private async Task ReloadAsync()
    {
        if (_app is null || _store is null) return;

        await _reloadLock.WaitAsync();
        try
        {
            if (!await _app.Poll.StopAsync())
                throw new TimeoutException("检查任务仍在运行，稍后再执行 reload。");

            try
            {
                var config = Context.Config.Load<KotoSubConfig>();
                _app.Reload(_store.Load());
                _config = config;
            }
            finally
            {
                _app.Poll.Start();
            }
        }
        finally
        {
            _reloadLock.Release();
        }

        BotLog.Info("[KotoSub] reload 完成，订阅和运行状态已加载并重新建立基线。");
    }

    private void SaveConfig(KotoSubConfig updated)
    {
        _config = updated;
        Context.Config.Save(_config);
    }
}
