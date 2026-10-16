using Microsoft.Extensions.Options;

using SongVault.Application.Abstractions;
using SongVault.Application.Billing;
using SongVault.Application.Common;
using SongVault.Application.Tests.Fakes;

namespace SongVault.Application.Tests.Billing;

public sealed class PaymentFailedJobTests
{
    private static readonly Guid BandId = Guid.CreateVersion7();
    private static readonly IOptions<AppOptions> App = Options.Create(new AppOptions { PublicUrl = "https://songvault.example/" });
    private readonly FakeBillingRepository _billing = new();
    private readonly FakeEmailSender _email = new();

    private static BackgroundJobContext Job(string payload) => new(Guid.NewGuid(), payload, 1, false);

    [Fact]
    public async Task Un_paiement_refuse_ecrit_a_chaque_Owner_avec_le_lien_Facturation()
    {
        _billing.Owners[BandId] = new BandOwners("Les Nocturnes <b>", ["a@test.local", "b@test.local"]);

        await new PaymentFailedJob(_billing, _email, App).RunAsync(Job(BandId.ToString()), CancellationToken.None);

        Assert.Equal(["a@test.local", "b@test.local"], _email.Sent.Select(m => m.To));
        var mail = _email.Sent[0];
        Assert.Equal("Paiement refusé pour « Les Nocturnes <b> » sur SongVault", mail.Subject);
        Assert.Contains($"https://songvault.example/b/{BandId}/billing", mail.TextBody);
        Assert.Contains("Les Nocturnes &lt;b&gt;", mail.HtmlBody);             // nom du groupe échappé
    }

    [Fact]
    public async Task Un_groupe_efface_depuis_ne_recoit_rien()
    {
        await new PaymentFailedJob(_billing, _email, App).RunAsync(Job(BandId.ToString()), CancellationToken.None);

        Assert.Empty(_email.Sent);
    }
}