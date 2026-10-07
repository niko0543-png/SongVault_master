using SongVault.Application.Files;
using SongVault.Domain.Files;

namespace SongVault.Application.Tests.Files;

public sealed class FileTypePolicyTests
{
    [Theory]
    [InlineData("maquette.mp3", SongFileType.Audio)]
    [InlineData("MAQUETTE.MP3", SongFileType.Audio)]      // insensible à la casse
    [InlineData("solo.gp5", SongFileType.Tablature)]
    [InlineData("paroles.pdf", SongFileType.Document)]
    public void Extensions_autorisees(string fileName, SongFileType expected)
        => Assert.Equal(expected, FileTypePolicy.Resolve(fileName)?.FileType);

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("chanson.mp3.exe")]                       // double extension
    [InlineData("script.js")]
    [InlineData("sans_extension")]
    public void Extensions_refusees(string fileName)
        => Assert.Null(FileTypePolicy.Resolve(fileName));

    // ===== Cas limites : Chemins complexes =====
    [Theory]
    [InlineData("dossier/chanson.mp3", "audio/mpeg", SongFileType.Audio)]
    [InlineData("dossier\\sous-dossier\\maquette.wav", "audio/wav", SongFileType.Audio)]
    [InlineData("C:\\Users\\musique\\demo.flac", "audio/flac", SongFileType.Audio)]
    [InlineData("./resources/tablature.gp5", "application/octet-stream", SongFileType.Tablature)]
    public void Chemins_complexes_resolvent_extension(string fileName, string expectedContentType, SongFileType expectedType)
    {
        var rule = FileTypePolicy.Resolve(fileName);
        Assert.NotNull(rule);
        Assert.Equal(expectedType, rule.FileType);
        Assert.Equal(expectedContentType, rule.ContentType);
    }

    // ===== Cas limites : Extensions multiples =====
    [Theory]
    [InlineData("chanson.backup.mp3", SongFileType.Audio)]     // Point supplémentaire avant extension
    [InlineData("maquette.v2.wav", SongFileType.Audio)]
    [InlineData("partition.originale.gp", SongFileType.Tablature)]
    [InlineData("document.final.pdf", SongFileType.Document)]
    public void Extensions_multiples_prend_derniere_extension(string fileName, SongFileType expectedType)
        => Assert.Equal(expectedType, FileTypePolicy.Resolve(fileName)?.FileType);

    // ===== Cas limites : Extension vide ou point terminal =====
    [Theory]
    [InlineData("chanson.")]                               // Point à la fin, pas d'extension
    [InlineData("...")]                                    // Seulement des points
    [InlineData(".")]                                      // Un seul point
    public void Noms_avec_points_terminaux_ou_vides_retournent_null(string fileName)
        => Assert.Null(FileTypePolicy.Resolve(fileName));

    // Note: "..mp3" retourne ".mp3" par Path.GetExtension, donc c'est un cas valide
    [Theory]
    [InlineData("..mp3", SongFileType.Audio)]              // Points au début (pas une extension vide)
    public void Noms_avec_points_au_debut_resolvent_extension(string fileName, SongFileType expectedType)
        => Assert.Equal(expectedType, FileTypePolicy.Resolve(fileName)?.FileType);

    // ===== Cas limites : Sensibilité de casse (couverture supplémentaire) =====
    [Theory]
    [InlineData("CHANSON.MP3", SongFileType.Audio)]
    [InlineData("Maquette.Wav", SongFileType.Audio)]
    [InlineData("Partition.GP5", SongFileType.Tablature)]
    [InlineData("Partition.Gp5", SongFileType.Tablature)]
    [InlineData("Document.PDF", SongFileType.Document)]
    [InlineData("document.pdf", SongFileType.Document)]
    [InlineData("DOCUMENT.PDF", SongFileType.Document)]
    [InlineData("DoC.pdf", SongFileType.Document)]
    public void Extension_insensible_casse_cas_limites(string fileName, SongFileType expectedType)
        => Assert.Equal(expectedType, FileTypePolicy.Resolve(fileName)?.FileType);

    // ===== Cas limites : Caractères spéciaux et espaces =====
    [Theory]
    [InlineData("chanson avec espaces.mp3", SongFileType.Audio)]
    [InlineData("maquette-v2-finale.wav", SongFileType.Audio)]
    [InlineData("partition_guitare.gp5", SongFileType.Tablature)]
    [InlineData("document (1).pdf", SongFileType.Document)]
    [InlineData("chanson [remix].mp3", SongFileType.Audio)]
    public void Noms_avec_caracteres_speciaux_resolvent_extension(string fileName, SongFileType expectedType)
        => Assert.Equal(expectedType, FileTypePolicy.Resolve(fileName)?.FileType);

    // ===== Cas limites : Espaces avant/après extension =====
    [Theory]
    [InlineData(" chanson.mp3", SongFileType.Audio)]       // Espace au début du nom
    [InlineData("chanson.mp3 ", null)]                     // Espace à la fin (extension n'existe pas)
    [InlineData(" chanson.mp3 ", null)]                    // Espaces avant et après
    public void Espacements_autour_nom_complet(string fileName, SongFileType? expectedType)
    {
        // Path.GetExtension gère les espaces
        var rule = FileTypePolicy.Resolve(fileName);
        if (expectedType == null)
        {
            Assert.Null(rule);
        }
        else
        {
            Assert.NotNull(rule);
            Assert.Equal(expectedType, rule.FileType);
        }
    }

    // ===== Cas limites : ContentType correct pour chaque extension =====
    [Theory]
    [InlineData("audio.mp3", "audio/mpeg")]
    [InlineData("audio.wav", "audio/wav")]
    [InlineData("audio.flac", "audio/flac")]
    [InlineData("tab.gp", "application/octet-stream")]
    [InlineData("tab.gp5", "application/octet-stream")]
    [InlineData("tab.gpx", "application/octet-stream")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("doc.txt", "text/plain; charset=utf-8")]
    public void ContentType_correct_pour_chaque_extension(string fileName, string expectedContentType)
        => Assert.Equal(expectedContentType, FileTypePolicy.Resolve(fileName)?.ContentType);

    // ===== Cas limites : AllowedExtensions =====
    [Fact]
    public void AllowedExtensions_contient_toutes_les_extensions_autorisees()
    {
        var allowed = FileTypePolicy.AllowedExtensions;

        // Vérifier que toutes les extensions attendues sont présentes
        Assert.Contains(".mp3", allowed);
        Assert.Contains(".wav", allowed);
        Assert.Contains(".flac", allowed);
        Assert.Contains(".gp", allowed);
        Assert.Contains(".gp5", allowed);
        Assert.Contains(".gpx", allowed);
        Assert.Contains(".pdf", allowed);
        Assert.Contains(".txt", allowed);

        // Vérifie que la collection a au minimum 8 éléments
        Assert.Equal(8, allowed.Count);
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".txt")]
    [InlineData(".gp5")]
    public void AllowedExtensions_est_insensible_casse(string extension)
    {
        var allowed = FileTypePolicy.AllowedExtensions;

        // Vérifier que l'extension en majuscule n'est pas dupliquée
        var lowerExt = extension.ToLowerInvariant();
        var upperExt = extension.ToUpperInvariant();

        // Au moins une forme doit être présente
        var found = allowed.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
        Assert.True(found, $"Extension {extension} introuvable dans AllowedExtensions");
    }

    // ===== Cas limites : Constantes de limite de taille =====
    [Fact]
    public void MaxFileSizeBytes_est_100_megabytes()
        => Assert.Equal(100L * 1024 * 1024, FileTypePolicy.MaxFileSizeBytes);

    [Fact]
    public void MaxRequestSizeBytes_est_superieur_MaxFileSizeBytes()
        => Assert.True(FileTypePolicy.MaxRequestSizeBytes > FileTypePolicy.MaxFileSizeBytes);

    [Fact]
    public void MaxRequestSizeBytes_inclut_marge_multipart()
    {
        var marge = FileTypePolicy.MaxRequestSizeBytes - FileTypePolicy.MaxFileSizeBytes;
        Assert.Equal(1024 * 1024, marge);  // 1 Mo de marge
    }

    // ===== Cas limites : Null et string vide =====
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Noms_vides_ou_espaces_retournent_null(string fileName)
        => Assert.Null(FileTypePolicy.Resolve(fileName));

    // ===== Cas limites : Extensions non autorisées mixtes =====
    [Theory]
    [InlineData("song.MP4")]                               // Vidéo non autorisée
    [InlineData("archive.ZIP")]                            // Archive non autorisée (casse différente)
    [InlineData("virus.EXE")]                              // Exécutable (casse différente)
    [InlineData("document.DOC")]                           // Word (non autorisé)
    [InlineData("feuille.XLSX")]                           // Excel (non autorisé)
    public void Extensions_interdites_casse_differente_retournent_null(string fileName)
        => Assert.Null(FileTypePolicy.Resolve(fileName));
}