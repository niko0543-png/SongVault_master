using System.IO.Compression;
using System.Text;

using SongVault.Application.Common.Exceptions;
using SongVault.Application.Releases.Exports;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Files;

namespace SongVault.Application.Tests.Releases;

public sealed class ReleaseArchiveTests
{
    private readonly ReleaseExportTestData _data = new();

    private async Task<ReleaseArchiveSource> SourceAsync()
        => (await _data.Exports.ReadSourceAsync(ReleaseExportTestData.BandId, _data.Release.Id, CancellationToken.None))!;

    [Fact]
    public async Task Le_plan_range_chaque_piste_dans_son_dossier_numerote()
    {
        var paths = ReleaseArchive.Plan(await SourceAsync()).Select(e => e.Path).ToList();

        Assert.Equal(
        [
            "Nuits blanches/01 - Nuit _ blanche/master.wav",
            "Nuits blanches/01 - Nuit _ blanche/Pistes séparées/1 - Voix.wav",
            "Nuits blanches/01 - Nuit _ blanche/Pistes séparées/2 - Batterie.wav",
            "Nuits blanches/01 - Nuit _ blanche/paroles.txt",
            "Nuits blanches/02 - Aube/aube.mp3",
            "Nuits blanches/credits.csv",
            "Nuits blanches/pochette.png",
        ], paths);
    }

    [Fact]
    public async Task Les_paroles_sont_en_UTF8_avec_BOM_et_fins_de_ligne_CRLF()
    {
        var lyrics = ReleaseArchive.Plan(await SourceAsync()).Single(e => e.Path.EndsWith("paroles.txt")).Content!;

        Assert.Equal([0xEF, 0xBB, 0xBF], lyrics[..3]);
        Assert.Equal("Couplet 1\r\nRefrain", Encoding.UTF8.GetString(lyrics[3..]));
    }


    [Fact]
    public async Task Des_paroles_avec_accords_donnent_paroles_txt_sans_accords_et_accords_txt()
    {
        _data.First.Update("Mix final", SongVault.Domain.Songs.SongVersionStatus.Final, null,
            "{c: Couplet 1}\n[Am]Je mar[G]che seul\n[F] [E7]", ReleaseExportTestData.Now);

        var entries = ReleaseArchive.Plan(await SourceAsync()).Where(e => e.Path.StartsWith("Nuits blanches/01 - ")).ToList();
        string Text(string name) => Encoding.UTF8.GetString(entries.Single(e => e.Path.EndsWith(name)).Content![3..]);

        Assert.Equal("Couplet 1\r\nJe marche seul", Text("/paroles.txt"));
        Assert.Equal("{c: Couplet 1}\r\n[Am]Je mar[G]che seul\r\n[F] [E7]", Text("/accords.txt"));
    }

    [Fact]
    public async Task Des_paroles_sans_accords_ne_donnent_pas_d_accords_txt()

        => Assert.DoesNotContain(ReleaseArchive.Plan(await SourceAsync()), e => e.Path.EndsWith("accords.txt"));
    [Fact]
    public async Task Le_csv_des_credits_est_celui_de_la_sortie()
    {
        var csv = Encoding.UTF8.GetString(ReleaseArchive.Plan(await SourceAsync()).Single(e => e.Path.EndsWith("credits.csv")).Content!);

        Assert.Contains("1;Nuit / blanche;v1 Mix final;;;Alice Martin;Auteur;100", csv);
    }

    [Fact]
    public async Task Deux_fichiers_du_meme_nom_dans_un_dossier_sont_numerotes()
    {
        _data.AddAudio(_data.Second, "Aube.MP3", [1]);

        var paths = ReleaseArchive.Plan(await SourceAsync()).Select(e => e.Path).Where(p => p.Contains("02 - Aube")).ToList();

        Assert.Equal(["Nuits blanches/02 - Aube/aube.mp3", "Nuits blanches/02 - Aube/Aube (2).mp3"], paths);
    }

    [Theory]
    [InlineData("AC/DC: Live?", "AC_DC_ Live_")]
    [InlineData("Fin... ", "Fin")]
    [InlineData("   ", "_")]
    [InlineData("<>", "__")]
    public void Un_nom_est_lisible_sur_tous_les_systemes(string name, string expected)
        => Assert.Equal(expected, ReleaseArchive.CleanName(name));

    [Fact]
    public void Un_nom_trop_long_est_coupe_en_gardant_son_extension()
    {
        var clean = ReleaseArchive.CleanName(new string('a', 120) + ".wav");

        Assert.Equal(80, clean.Length);
        Assert.EndsWith("a.wav", clean);
    }

    [Fact]
    public async Task Une_sortie_sans_piste_est_refusee()
    {
        _data.Release.SetTracks([], ReleaseExportTestData.Now);

        var source = await SourceAsync();

        var error = Assert.Throws<ConflictException>(() => ReleaseArchive.EnsureReady(source));
        Assert.Contains("aucune piste", error.Message);
    }

    [Fact]
    public async Task Une_piste_sans_fichier_audio_est_nommee_dans_le_refus()
    {
        var empty = _data.Songs.Songs[1].AddVersion("v2", Domain.Songs.SongVersionStatus.Demo, null, null, ReleaseExportTestData.Now);
        _data.Release.SetTracks([
            new(_data.Songs.Songs[0].Id, _data.First.Id), new(_data.Songs.Songs[1].Id, empty.Id)], ReleaseExportTestData.Now);

        var source = await SourceAsync();

        var error = Assert.Throws<ConflictException>(() => ReleaseArchive.EnsureReady(source));
        Assert.Contains("piste 2 « Aube »", error.Message);
    }

    [Fact]
    public async Task Un_fichier_pas_encore_analyse_ou_bloque_est_nomme_dans_le_refus()
    {
        _data.AddAudio(_data.Second, "prise.wav", [1], FileScanStatus.Pending);
        _data.AddAudio(_data.First, "virus.wav", [1], FileScanStatus.Infected);

        var source = await SourceAsync();

        var error = Assert.Throws<ConflictException>(() => ReleaseArchive.EnsureReady(source));
        Assert.Contains("« virus.wav » (piste 1)", error.Message);
        Assert.Contains("« prise.wav » (piste 2)", error.Message);
    }

    [Fact]
    public async Task L_archive_s_ecrit_dans_un_flux_sans_retour_en_arriere()
    {
        var source = await SourceAsync();
        byte[]? zipBytes = null;
        await using (var output = new ForwardOnlyWriteStream(bytes => zipBytes = bytes))
            await ReleaseArchiveWriter.WriteAsync(output, ReleaseArchive.Plan(source), ReleaseExportTestData.Now,
                _data.Storage.OpenReadAsync, CancellationToken.None);

        using var zip = new ZipArchive(new MemoryStream(zipBytes!));
        Assert.Equal(7, zip.Entries.Count);
        var master = zip.GetEntry("Nuits blanches/01 - Nuit _ blanche/master.wav")!;
        Assert.Equal(master.Length, master.CompressedLength);                // audio stocké tel quel
        using var content = new MemoryStream();
        await using (var read = await master.OpenAsync())
            await read.CopyToAsync(content);
        Assert.Equal([1, 2, 3], content.ToArray());
        Assert.NotNull(zip.GetEntry("Nuits blanches/pochette.png"));
    }
}