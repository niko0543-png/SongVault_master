using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Files;

namespace SongVault.Infrastructure.Audio;

/// <summary>
/// Un seul passage de ffmpeg par fichier (ADR 0021) : l'original arrive sur l'entrée standard, au fil de sa lecture
/// dans le stockage (rien de copié sur le disque, même pour un WAV de 2 Go) ; ffmpeg écrit le MP3 d'écoute dans un
/// fichier temporaire, et un son mono à 8 kHz sur sa sortie standard, d'où l'on tire la forme d'onde et la durée.
/// Un fichier déjà sur le disque (FileStream) est lu par son chemin (ADR 0049) : ffmpeg peut alors y aller chercher
/// l'index d'un .m4a, souvent rangé à la fin, ce qu'une entrée standard ne permet pas.
/// </summary>
internal sealed class FfmpegAudioProcessor(IOptions<AudioPreviewOptions> options) : IAudioProcessor
{
    private const string Unreadable = "Ce fichier audio n'a pas pu être lu : il est peut-être abîmé, ou dans un format inattendu.";

    public async Task<AudioAnalysis> ProcessAsync(Stream source, string? mp3OutputPath, CancellationToken ct)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(settings.Timeout);

        var sourcePath = (source as FileStream)?.Name;
        using var process = new Process { StartInfo = StartInfo(settings, sourcePath, mp3OutputPath) };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"ffmpeg introuvable (« {settings.FfmpegPath} ») : installez-le, ou indiquez son chemin dans AudioPreview:FfmpegPath.", ex);
        }

        var waveform = new WaveformBuilder();
        try
        {
            var feed = sourcePath is null ? FeedAsync(source, process.StandardInput.BaseStream, timeout.Token)
                : CloseAsync(process.StandardInput.BaseStream);
            var read = ReadPcmAsync(process.StandardOutput.BaseStream, waveform, timeout.Token);
            var errors = ReadLastErrorAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(feed, read, errors);
            await process.WaitForExitAsync(timeout.Token);

            if (process.ExitCode != 0) throw new AudioProcessingException(Unreadable, await errors);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);      // délai dépassé, ou API arrêtée
        }

        if (waveform.DurationMs == 0) throw new AudioProcessingException("Aucun son dans ce fichier.");
        return new AudioAnalysis(waveform.Build(), waveform.DurationMs);
    }

    private static ProcessStartInfo StartInfo(AudioPreviewOptions settings, string? sourcePath, string? mp3OutputPath)
    {
        var start = new ProcessStartInfo(settings.FfmpegPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        string[] input = ["-hide_banner", "-nostats", "-loglevel", "error", "-y", "-i", sourcePath ?? "pipe:0"];
        // MP3 d'écoute : stéréo au plus (un WAV 5.1 aussi), 44,1 kHz, débit constant
        string[] mp3 = mp3OutputPath is null ? [] :
        [
            "-map", "0:a:0", "-vn", "-ac", "2", "-ar", "44100",
            "-c:a", "libmp3lame", "-b:a", $"{settings.Mp3BitrateKbps.ToString(CultureInfo.InvariantCulture)}k",
            "-f", "mp3", mp3OutputPath,
        ];
        // Forme d'onde : mono, 8 kHz, PCM 16 bits, sur la sortie standard
        string[] pcm =
        [
            "-map", "0:a:0", "-vn", "-ac", "1", "-ar", WaveformBuilder.SampleRate.ToString(CultureInfo.InvariantCulture),
            "-c:a", "pcm_s16le", "-f", "s16le", "pipe:1",
        ];
        foreach (var argument in (string[])[.. input, .. mp3, .. pcm]) start.ArgumentList.Add(argument);
        return start;
    }

    private static async Task FeedAsync(Stream source, Stream stdin, CancellationToken ct)
    {
        try
        {
            await source.CopyToAsync(stdin, 81920, ct);
        }
        catch (IOException)
        {
            // ffmpeg a refermé son entrée (fichier illisible) : son code de sortie dira pourquoi
        }
        finally
        {
            try { await stdin.DisposeAsync(); } catch (IOException) { /* idem */ }
        }
    }

    private static async Task CloseAsync(Stream stdin)
    {
        try { await stdin.DisposeAsync(); } catch (IOException) { /* ffmpeg déjà arrêté */ }
    }

    private static async Task ReadPcmAsync(Stream stdout, WaveformBuilder waveform, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await stdout.ReadAsync(buffer, ct)) > 0) waveform.Add(buffer.AsSpan(0, read));
    }

    /// <summary>Dernière ligne des erreurs de ffmpeg, pour les journaux (jamais montrée aux membres).</summary>
    private static async Task<string?> ReadLastErrorAsync(StreamReader stderr, CancellationToken ct)
    {
        string? last = null;
        while (await stderr.ReadLineAsync(ct) is { } line)
            if (!string.IsNullOrWhiteSpace(line)) last = line.Length > 500 ? line[..500] : line;
        return last;
    }
}