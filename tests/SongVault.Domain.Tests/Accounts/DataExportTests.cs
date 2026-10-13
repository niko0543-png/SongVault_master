using SongVault.Domain.Accounts;
using SongVault.Domain.Common;

namespace SongVault.Domain.Tests.Accounts;

public sealed class DataExportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Prete_elle_se_telecharge_pendant_7_jours()
    {
        var export = DataExport.Request("u1", Now);

        export.MarkReady("export-x.zip", 1234, Now);

        Assert.Equal(DataExportStatus.Ready, export.Status);
        Assert.True(export.IsDownloadableAt(Now.AddDays(7).AddTicks(-1)));
        Assert.False(export.IsDownloadableAt(Now.AddDays(7)));
    }

    [Fact]
    public void En_preparation_elle_ne_se_telecharge_pas()
        => Assert.False(DataExport.Request("u1", Now).IsDownloadableAt(Now));

    [Fact]
    public void Un_echec_ne_change_pas_une_archive_deja_prete()
    {
        var export = DataExport.Request("u1", Now);
        export.MarkReady("export-x.zip", 1234, Now);

        export.MarkFailed(Now);

        Assert.Equal(DataExportStatus.Ready, export.Status);
        Assert.Throws<DomainException>(() => export.MarkReady("export-y.zip", 1, Now));
    }
}