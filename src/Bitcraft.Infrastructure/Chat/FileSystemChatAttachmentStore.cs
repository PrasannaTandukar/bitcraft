using System.Globalization;
using Bitcraft.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Bitcraft.Infrastructure.Chat;

public sealed class FileSystemChatAttachmentStore : IChatAttachmentStore
{
    private readonly string root;

    public FileSystemChatAttachmentStore(IConfiguration configuration)
    {
        // The default is outside the repository/web root and requires no machine-specific path.
        root = Path.GetFullPath(configuration["ChatAttachments:RootPath"]
            ?? Path.Combine(Path.GetTempPath(), "bitcraft-chat-attachments"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(root);
        else Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public async Task SaveAsync(string storageKey, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var destination = ResolvePath(storageKey);
        var temporary = destination + ".tmp";
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            BufferSize = 81920, Options = FileOptions.Asynchronous
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            await using (var output = new FileStream(temporary, options))
                await output.WriteAsync(data, cancellationToken);
            File.Move(temporary, destination);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult<Stream?>(new FileStream(ResolvePath(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true));
        }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(ResolvePath(storageKey));
        return Task.CompletedTask;
    }

    public Task CleanupExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var nowSeconds = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
        foreach (var file in Directory.EnumerateFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            if (name.EndsWith(".tmp", StringComparison.Ordinal)) name = name[..^4];
            if (TryReadKey(name, out var expires) && expires <= nowSeconds) File.Delete(file);
        }
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        if (!TryReadKey(key, out _)) throw new ArgumentException("Invalid attachment storage key.", nameof(key));
        return Path.Combine(root, key);
    }

    private static bool TryReadKey(string key, out long expires)
    {
        expires = 0;
        var parts = key.Split('.');
        return parts.Length == 2 && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out expires)
            && Guid.TryParseExact(parts[1], "N", out _);
    }
}
