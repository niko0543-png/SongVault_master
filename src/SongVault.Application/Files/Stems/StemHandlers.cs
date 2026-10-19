using SongVault.Application.Abstractions;
using SongVault.Application.Common.Exceptions;

namespace SongVault.Application.Files.Stems;

// Lire = tout membre ; écrire = Member ou Owner (ADR 0008, appliqué par [BandScoped] sur le contrôleur).

public sealed class GetStemsHandler(ISongRepository songs)
{
    public async Task<IReadOnlyList<StemDto>> HandleAsync(Guid songId, Guid versionId, CancellationToken ct)
        => await songs.ListStemsAsync(songId, versionId, ct) ?? throw new NotFoundException("SongVersion", versionId);
}

public sealed class SetStemsHandler(ISongRepository songs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>Remplace les pistes ; les règles (8 au plus, audio, en place, nommées) sont dans SongVersion.SetStems.</summary>
    public async Task<IReadOnlyList<StemDto>> HandleAsync(SetStemsCommand command, CancellationToken ct)
    {
        var song = await songs.GetWithVersionAsync(command.SongId, command.VersionId, ct)
                   ?? throw new NotFoundException("Song", command.SongId);
        var version = song.FindVersion(command.VersionId)
                      ?? throw new NotFoundException("SongVersion", command.VersionId);

        version.SetStems(command.Stems, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        return await songs.ListStemsAsync(command.SongId, command.VersionId, ct)
               ?? throw new NotFoundException("SongVersion", command.VersionId);
    }
}