using SongVault.Application.Abstractions;
using SongVault.Domain.Files;

namespace SongVault.Application.Tests.Fakes;

/// <summary>Analyses en mémoire ; Targets : original de chaque fichier ; Notices : de quoi prévenir l'auteur.</summary>
internal sealed class FakeFileScanRepository : IFileScanRepository
{
    public List<FileScan> Scans { get; } = [];
    public Dictionary<Guid, FileScanTarget> Targets { get; } = [];
    public Dictionary<Guid, InfectedFileNotice> Notices { get; } = [];

    public void Add(FileScan scan) => Scans.Add(scan);

    public Task<FileScan?> GetAsync(Guid songFileId, CancellationToken ct)
        => Task.FromResult(Scans.SingleOrDefault(s => s.SongFileId == songFileId));

    public Task<IReadOnlyList<FileScan>> ListNotScannedAsync(int take, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<FileScan>>(
            [.. Scans.Where(s => s.Status == FileScanStatus.NotScanned).OrderBy(s => s.RequestedAt).Take(take)]);

    public Task<FileScanTarget?> GetTargetAsync(Guid songFileId, CancellationToken ct)
        => Task.FromResult(Targets.GetValueOrDefault(songFileId));

    public Task<InfectedFileNotice?> GetInfectedNoticeAsync(Guid songFileId, CancellationToken ct)
        => Task.FromResult(Notices.GetValueOrDefault(songFileId));
}

/// <summary>Antivirus factice : lit tout le flux, puis rend Threat (null : sain) ou lève Failure.</summary>
internal sealed class FakeMalwareScanner : IMalwareScanner
{
    public bool IsEnabled { get; set; } = true;
    public string? Threat { get; set; }
    public Exception? Failure { get; set; }
    public int Calls { get; private set; }

    public async Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        Calls++;
        await content.CopyToAsync(Stream.Null, ct);
        if (Failure is not null) throw Failure;
        return new MalwareScanResult(Threat);
    }
}