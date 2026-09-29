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
}