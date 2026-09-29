using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Storage;

internal sealed class LocalFileStorageService : IFileStorageService
{
    private const int BufferSize = 81920;
    private readonly string _root;

    public LocalFileStorageService(IOptions<FileStorageOptions> options, IHostEnvironment environment)
    {
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.RootPath));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct)
    {
        // CreateNew : échoue si le fichier existe déjà (jamais d'écrasement silencieux)
        await using var target = new FileStream(ResolvePath(storageKey), FileMode.CreateNew,
            FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await content.CopyToAsync(target, ct);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
        => Task.FromResult<Stream>(new FileStream(ResolvePath(storageKey), FileMode.Open,
            FileAccess.Read, FileShare.Read, BufferSize, useAsync: true));

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    // Défense en profondeur : même si une clé était corrompue ("../../web.config"), on ne sort pas de la racine
    private string ResolvePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Clé de stockage invalide.");
        return fullPath;
    }
}