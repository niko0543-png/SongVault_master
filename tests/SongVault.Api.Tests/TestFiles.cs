using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace SongVault.Api.Tests;

internal static class TestFiles
{
    /// <summary>Octets aléatoires précédés de "ID3" : un MP3 du point de vue de la signature.</summary>
    public static byte[] Mp3(int size)
    {
        var bytes = RandomNumberGenerator.GetBytes(size);
        "ID3"u8.CopyTo(bytes);
        return bytes;
    }

    /// <summary>Un vrai WAV PCM 16 bits mono 22,05 kHz : un la (440 Hz) de la durée demandée (#15).</summary>
    public static byte[] Wav(double seconds)
    {
        const int rate = 22050;
        var samples = (int)(rate * seconds);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + samples * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(samples * 2);
        for (var i = 0; i < samples; i++)
            w.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 12000));
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// ADR 0049 : Wav(seconds) réencodé par ffmpeg dans le format d'un enregistrement du navigateur (.webm, .ogg, .m4a).
    /// Le .m4a garde l'index (moov) à la fin, comme bien des mémos vocaux : illisible depuis une entrée standard.
    /// </summary>
    public static byte[] Encoded(double seconds, string extension)
    {
        var input = Path.Combine(Path.GetTempPath(), $"songvault-test-{Guid.NewGuid():N}.wav");
        var output = Path.ChangeExtension(input, extension);
        try
        {
            File.WriteAllBytes(input, Wav(seconds));
            var codec = extension == ".m4a" ? "aac" : "libopus";
            using var ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg",
                ["-hide_banner", "-loglevel", "error", "-y", "-i", input, "-c:a", codec, output])
                { RedirectStandardError = true })!;
            var errors = ffmpeg.StandardError.ReadToEnd();
            ffmpeg.WaitForExit();
            if (ffmpeg.ExitCode != 0) throw new InvalidOperationException($"ffmpeg : {errors}");
            return File.ReadAllBytes(output);
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
        }
    }

    /// <summary>Octets aléatoires précédés de "fLaC" : accepté à l'envoi, illisible pour ffmpeg (#15).</summary>
    public static byte[] BrokenFlac(int size)
    {
        var bytes = RandomNumberGenerator.GetBytes(size);
        "fLaC"u8.CopyTo(bytes);
        return bytes;
    }

    /// <summary>
    /// #17 : ce que TestMalwareScanner prend pour un virus. Pas la chaîne EICAR : l'antivirus du PC de développement
    /// effacerait le fichier du dossier temporaire des tests avant que l'API ne le lise.
    /// </summary>
    public static ReadOnlySpan<byte> VirusMarker => "SONGVAULT-TEST-VIRUS"u8;

    /// <summary>Un .txt qui contient VirusMarker.</summary>
    public static byte[] InfectedTxt() => [.. "Paroles du refrain\n"u8, .. VirusMarker];


    public static MultipartFormDataContent Form(byte[] bytes, string fileName)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { part, "file", fileName } };
    }
}