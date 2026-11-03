using System.Text.Json;

namespace Shirobot.Plugin.KotoSub.Infrastructure.Storage;

internal static class AtomicJsonFile
{
    private const int ReplaceRetryCount = 7;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static T Read<T>(string path)
    {
        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException($"{Path.GetFileName(path)} 为空。");
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidDataException($"{Path.GetFileName(path)} 无法解析。");
    }

    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = CreateTempPath(path);
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, WriteOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            await ReplaceFileWithRetryAsync(tempPath, path, cancellationToken);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = CreateTempPath(path);
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       16 * 1024, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, WriteOptions);
                stream.Flush(flushToDisk: true);
            }

            ReplaceFileWithRetry(tempPath, path);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private static string CreateTempPath(string path) =>
        $"{path}.{Guid.NewGuid():N}.tmp";

    private static async Task ReplaceFileWithRetryAsync(
        string tempPath, string destinationPath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ReplaceFile(tempPath, destinationPath);
                return;
            }
            catch (Exception ex) when (IsFileBusy(ex) && attempt < ReplaceRetryCount - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << attempt)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static void ReplaceFileWithRetry(string tempPath, string destinationPath)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                ReplaceFile(tempPath, destinationPath);
                return;
            }
            catch (Exception ex) when (IsFileBusy(ex) && attempt < ReplaceRetryCount - 1)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100 * (1 << attempt)));
            }
        }
    }

    private static void ReplaceFile(string tempPath, string destinationPath)
    {
        // File.Replace can fail on Windows while removing the destination even after retries
        // (for example, when another process briefly opens state.json). Move with overwrite
        // uses the platform's same-volume replace operation and handles this case more reliably.
        File.Move(tempPath, destinationPath, overwrite: true);
    }

    public static bool IsFileBusy(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (IsFileBusy(ex))
        {
            BotLog.Warning($"[KotoSub] 清理临时状态文件失败: file={Path.GetFileName(path)} error={ex.GetType().Name}");
        }
    }
}
