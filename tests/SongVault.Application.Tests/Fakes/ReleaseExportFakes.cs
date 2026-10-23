using SongVault.Application.Abstractions;
using SongVault.Application.Releases.Exports;
using SongVault.Domain.Files;
using SongVault.Domain.Releases;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Archives en mémoire ; la source se lit dans les faux dépôts des sorties, des morceaux et des analyses.</summary>
internal sealed class FakeReleaseExportRepository(
    FakeReleaseRepository releases, FakeSongRepository songs, FakeFileScanRepository scans) : IReleaseExportRepository
{
    public List<ReleaseExport> Exports { get; } = [];

    public void Add(ReleaseExport export) => Exports.Add(export);

    public void Remove(ReleaseExport export) => Exports.Remove(export);

    public Task<ReleaseExport?> GetAsync(Guid exportId, CancellationToken ct)
        => Task.FromResult(Exports.SingleOrDefault(e => e.Id == exportId));

    public Task<ReleaseExport?> GetLatestAsync(Guid bandId, Guid releaseId, CancellationToken ct)
        => Task.FromResult(Exports.Where(e => e.BandId == bandId && e.ReleaseId == releaseId)
            .OrderByDescending(e => e.RequestedAt).FirstOrDefault());

    public Task<string?> GetReleaseTitleAsync(Guid bandId, Guid releaseId, CancellationToken ct)
        => Task.FromResult(releases.Releases.SingleOrDefault(r => r.Id == releaseId && r.BandId == bandId)?.Title);

    public async Task<ReleaseArchiveSource?> ReadSourceAsync(Guid bandId, Guid releaseId, CancellationToken ct)
    {
        var release = releases.Releases.SingleOrDefault(r => r.Id == releaseId && r.BandId == bandId);
        if (release is null) return null;
        var detail = (await releases.GetDetailAsync(releaseId, ct))!;

        var versionIds = release.Tracks.Select(t => t.VersionId).ToHashSet();
        var files = songs.Songs.SelectMany(s => s.Versions).Where(v => versionIds.Contains(v.Id))
            .SelectMany(v => v.Files)
            .Where(f => f.FileType == SongFileType.Audio && !f.IsDeleted)
            .Select(f => new ReleaseArchiveFile(f.SongVersionId, f.OriginalFileName, f.StorageKey, f.StemName, f.StemPosition,
                scans.Scans.SingleOrDefault(s => s.SongFileId == f.Id)?.Status))
            .ToList();
        var songIds = release.Tracks.Select(t => t.SongId).ToHashSet();
        return new ReleaseArchiveSource(detail, release.CoverStorageKey, release.CoverContentType,
            [.. songs.Songs.Where(s => songIds.Contains(s.Id))], files);
    }
}

/// <summary>Flux d'écriture sans Seek ni Position, comme celui du stockage objet ; rend les octets à la fermeture.</summary>
internal sealed class ForwardOnlyWriteStream(Action<byte[]> onClose) : Stream
{
    private readonly MemoryStream _buffer = new();
    private bool _closed;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => _buffer.Write(buffer, offset, count);
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_closed)
        {
            _closed = true;
            onClose(_buffer.ToArray());
        }
        base.Dispose(disposing);
    }
}