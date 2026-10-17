using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;

namespace SongVault.Infrastructure.Antivirus;

/// <summary>
/// Antivirus ClamAV (ADR 0024) : le fichier est envoyé à clamd par la commande INSTREAM, en morceaux,
/// sans passer par le disque de l'API. Une connexion par analyse.
/// </summary>
public sealed class ClamAvScanner(IOptions<ClamAvOptions> options) : IMalwareScanner
{
    private const int ChunkSize = 256 * 1024;
    private const int MaxReplyLength = 4096;

    public bool IsEnabled => true;

    public async Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(settings.Timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(settings.Host, settings.Port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            await SendAsync(content, stream, timeout.Token);
            return ParseReply(await ReadReplyAsync(stream, timeout.Token));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MalwareScannerException($"ClamAV n'a pas répondu en {settings.Timeout}.");
        }
        catch (SocketException ex)
        {
            throw new MalwareScannerException($"ClamAV injoignable ({settings.Host}:{settings.Port}).", ex);
        }
        catch (IOException ex)
        {
            throw new MalwareScannerException("Connexion à ClamAV interrompue.", ex);
        }
    }

    /// <summary>
    /// Traduit la réponse de clamd : « stream: OK », « stream: Win.Test.EICAR_HDB-1 FOUND »,
    /// ou une erreur comme « INSTREAM size limit exceeded. ERROR ».
    /// </summary>
    /// <exception cref="MalwareScannerException">Pas de verdict.</exception>
    public static MalwareScanResult ParseReply(string reply)
    {
        var text = reply.TrimEnd('\0').Trim();
        if (text.EndsWith(" FOUND", StringComparison.Ordinal))
        {
            var colon = text.IndexOf(": ", StringComparison.Ordinal);
            var threat = text[(colon < 0 ? 0 : colon + 2)..^" FOUND".Length].Trim();
            return new MalwareScanResult(threat.Length == 0 ? "Menace inconnue" : threat);
        }
        if (text.EndsWith(": OK", StringComparison.Ordinal)) return MalwareScanResult.Clean;
        throw new MalwareScannerException($"Réponse de ClamAV sans verdict : {text}");
    }

    // Chaque morceau : sa longueur sur 4 octets (ordre réseau), puis ses octets ; un morceau vide termine le flux
    private static async Task SendAsync(Stream content, NetworkStream stream, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize + 4);
        try
        {
            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(4, ChunkSize), ct)) > 0)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)read);
                await stream.WriteAsync(buffer.AsMemory(0, read + 4), ct);
            }
            BinaryPrimitives.WriteUInt32BigEndian(buffer, 0);
            await stream.WriteAsync(buffer.AsMemory(0, 4), ct);
        }
        catch (IOException)
        {
            // clamd coupe l'envoi quand il refuse le flux (trop long) : sa réponse, lue ensuite, dit pourquoi
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // La réponse se termine par \0 (préfixe « z » de la commande)
    private static async Task<string> ReadReplyAsync(NetworkStream stream, CancellationToken ct)
    {
        var reply = new MemoryStream();
        var buffer = new byte[256];
        while (reply.Length < MaxReplyLength)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            var end = Array.IndexOf(buffer, (byte)0, 0, read);
            reply.Write(buffer, 0, end < 0 ? read : end);
            if (end >= 0) break;
        }
        return Encoding.UTF8.GetString(reply.GetBuffer(), 0, (int)reply.Length);
    }
}