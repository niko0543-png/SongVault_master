using System.Text;

using SongVault.Application.Files;

namespace SongVault.Application.Tests.Files;

[Trait("Category", "Security")]
public sealed class FileSignatureValidatorTests
{
    private static byte[] Bytes(string ascii) => Encoding.Latin1.GetBytes(ascii);

    public static TheoryData<string, byte[]> Valides => new()
{
    { ".mp3",  Bytes("ID3\u0004\u0000") },
    { ".mp3",  [0xFF, 0xFB, 0x90, 0x64] },                         // trame MPEG sans balises
    { ".wav",  Bytes("RIFF\u0024\u0008\u0000\u0000WAVEfmt ") },
    { ".flac", Bytes("fLaC\u0000\u0000\u0000\u0022") },
    { ".pdf",  Bytes("%PDF-1.7\n") },
    { ".gp",   [0x50, 0x4B, 0x03, 0x04, 0x14] },                   // PK ETX EOT DC4, comme votre fichier
    { ".gpx",  Bytes("BCFZ\u0000\u0001") },
    { ".gp5",  Bytes("\u0018FICHIER GUITAR PRO v5.00") },
    { ".txt",  Bytes("Couplet 1\nRefrain") },
    { ".MP3",  Bytes("ID3\u0003") },
};

    public static TheoryData<string, byte[]> Invalides => new()
{
    { ".mp3",  Bytes("MZ\u0090\u0000") },                           // exécutable Windows renommé
    { ".mp3",  Bytes("<html><script>") },
    { ".pdf",  Bytes("ID3\u0004") },                                // vrai MP3, mauvaise extension
    { ".wav",  Bytes("RIFF\u0024\u0008\u0000\u0000AVI ") },         // RIFF mais pas WAVE
    { ".txt",  [0x4D, 0x5A, 0x00, 0x00] },                          // octets nuls : binaire
    { ".mp3",  [] },
    { ".exe",  Bytes("MZ") },
};

    [Theory, MemberData(nameof(Valides))]
    public void Accepte_les_signatures_valides(string extension, byte[] header)
        => Assert.True(FileSignatureValidator.Matches(extension, header));

    [Theory, MemberData(nameof(Invalides))]
    public void Refuse_les_signatures_invalides(string extension, byte[] header)
        => Assert.False(FileSignatureValidator.Matches(extension, header));

    [Theory]
    [InlineData("../../x.mp3", "x.mp3")]
    [InlineData(@"..\..\x.mp3", "x.mp3")]
    [InlineData("C:\\Users\\moi\\maquette.mp3", "maquette.mp3")]
    [InlineData("ma\"quette\u0007.mp3", "maquette.mp3")]
    [InlineData("   ", "fichier")]
    public void Sanitize_nettoie_le_nom(string input, string expected)
        => Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
}