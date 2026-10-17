using System.Security.Cryptography;
using System.Text;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Infrastructure.Antivirus;

namespace SongVault.Api.Tests.Files;

/// <summary>Réponses de clamd, sans conteneur.</summary>
public sealed class ClamAvReplyTests
{
    [Theory]
    [InlineData("stream: OK\0", null)]
    [InlineData("stream: Win.Test.EICAR_HDB-1 FOUND\0", "Win.Test.EICAR_HDB-1")]
    [InlineData("stream: Doc.Exploit.CVE_2017_0199-1 FOUND", "Doc.Exploit.CVE_2017_0199-1")]
    public void Lit_le_verdict(string reply, string? threat)
        => Assert.Equal(threat, ClamAvScanner.ParseReply(reply).Threat);

    [Fact]
    public void Une_erreur_n_est_pas_un_verdict()
        => Assert.Throws<MalwareScannerException>(() => ClamAvScanner.ParseReply("INSTREAM size limit exceeded. ERROR\0"));
}

/// <summary>Le vrai ClamAV, en conteneur, une fois pour la classe. Bases de signatures incluses dans l'image.</summary>
public sealed class ClamAvFixture : IAsyncLifetime
{
    private readonly IContainer _clamav = new ContainerBuilder("clamav/clamav:1.4")
        .WithEnvironment("CLAMAV_NO_FRESHCLAMD", "true")         // pas de mise à jour des signatures pendant les tests
        .WithPortBinding(3310, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310))
        .Build();

    public ClamAvScanner Scanner { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _clamav.StartAsync();
        Scanner = new ClamAvScanner(Options.Create(new ClamAvOptions
        {
            Host = _clamav.Hostname,
            Port = _clamav.GetMappedPublicPort(3310),
            Timeout = TimeSpan.FromMinutes(2),
        }));

        // Le port s'ouvre parfois avant la fin du chargement des signatures : on attend un premier verdict
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await Scanner.ScanAsync(new MemoryStream("ping"u8.ToArray()), CancellationToken.None);
                return;
            }
            catch (MalwareScannerException) when (attempt < 60)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    public Task DisposeAsync() => _clamav.DisposeAsync().AsTask();
}

[Trait("Category", "Security")]
public sealed class ClamAvScannerTests(ClamAvFixture clamav) : IClassFixture<ClamAvFixture>
{
    /// <summary>
    /// Fichier de test EICAR, assemblé en mémoire : écrit tel quel dans ce fichier source ou sur le disque,
    /// il serait mis en quarantaine par l'antivirus du PC de développement.
    /// </summary>
    private static byte[] Eicar() => Encoding.ASCII.GetBytes(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$" + "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    [Fact]
    public async Task Reconnait_le_fichier_de_test_EICAR()
    {
        var result = await clamav.Scanner.ScanAsync(new MemoryStream(Eicar()), CancellationToken.None);

        Assert.Contains("EICAR", result.Threat, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Laisse_passer_un_fichier_sain_envoye_en_plusieurs_morceaux()
    {
        var bytes = RandomNumberGenerator.GetBytes(3 * 1024 * 1024);           // 12 morceaux de 256 Kio

        var result = await clamav.Scanner.ScanAsync(new MemoryStream(bytes), CancellationToken.None);

        Assert.Null(result.Threat);
    }

    [Fact]
    public async Task Un_antivirus_injoignable_leve_une_erreur_claire()
    {
        var nowhere = new ClamAvScanner(Options.Create(new ClamAvOptions { Host = "127.0.0.1", Port = 1, Timeout = TimeSpan.FromSeconds(5) }));

        var error = await Assert.ThrowsAsync<MalwareScannerException>(() =>
            nowhere.ScanAsync(new MemoryStream([1, 2, 3]), CancellationToken.None));

        Assert.Contains("injoignable", error.Message);
    }
}