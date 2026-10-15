using Microsoft.Extensions.Options;

using SongVault.Application.Common;
using SongVault.Application.Common.Exceptions;
using SongVault.Application.Files;
using SongVault.Application.Invitations;
using SongVault.Application.Sharing;
using SongVault.Application.Tests.Fakes;
using SongVault.Domain.Common;
using SongVault.Domain.Sharing;

namespace SongVault.Application.Tests.Sharing;

public sealed class ShareLinkHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid BandId = Guid.CreateVersion7();
    private static readonly Guid VersionId = Guid.CreateVersion7();
    private static readonly ShareVisitor Visitor = new("203.0.113.7", "Mozilla/5.0 (Macintosh)");

    private readonly FakeShareLinkRepository _links = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeShareUnlockTokens _unlocks = new();
    private readonly FakeSharePasswordHasher _passwords = new();
    private readonly FakeFileStorageService _storage = new();
    private FixedTimeProvider _clock = new(Now);

    public ShareLinkHandlersTests() => _links.Targets.Add((BandId, VersionId));

    private CreateShareLinkHandler CreateHandler() => new(_links, _uow, new FakeBandContext(BandId), new FakeCurrentUser(),
        _passwords, Options.Create(new AppOptions { PublicUrl = "https://songvault.example/" }), _clock);

    private ShareLinkGate Gate() => new(_links, _unlocks, _clock);

    private Task<ShareLinkCreatedDto> CreateAsync(string? password = null, int days = 30, Guid? versionId = null)
        => CreateHandler().HandleAsync(new CreateShareLinkCommand(
            ShareTargetType.Version, versionId ?? VersionId, "Studio Delta", days, password), CancellationToken.None);

    private static string TokenOf(ShareLinkCreatedDto created) => created.Url[(created.Url.LastIndexOf('/') + 1)..];

    [Fact]
    public async Task Create_renvoie_le_lien_une_fois_et_ne_garde_que_l_empreinte()
    {
        var created = await CreateAsync();

        var token = TokenOf(created);
        Assert.StartsWith("https://songvault.example/s/", created.Url);
        Assert.Equal(43, token.Length);
        var link = Assert.Single(_links.Links);
        Assert.Equal(InvitationToken.Hash(token), link.TokenHash);
        Assert.Equal("owner-1", link.CreatedById);
        Assert.Equal(Now.AddDays(30), created.Link.ExpiresAt);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Create_refuse_une_cible_d_un_autre_groupe()
        => await Assert.ThrowsAsync<NotFoundException>(() => CreateAsync(versionId: Guid.CreateVersion7()));

    [Fact]
    public async Task Create_hache_le_mot_de_passe_et_refuse_un_trop_court()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateAsync(password: "12345"));

        var created = await CreateAsync(password: "écoute-v3");

        Assert.True(created.Link.HasPassword);
        Assert.Equal("hash:écoute-v3", Assert.Single(_links.Links).PasswordHash);
    }

    [Fact]
    public async Task Un_lien_revoque_ou_expire_est_introuvable()
    {
        var revoked = TokenOf(await CreateAsync());
        await new RevokeShareLinkHandler(_links, _uow, new FakeBandContext(BandId), _clock)
            .HandleAsync(_links.Links[0].Id, CancellationToken.None);
        var expiring = TokenOf(await CreateAsync(days: 1));

        await Assert.ThrowsAsync<NotFoundException>(() => Gate().OpenAsync(revoked, null, CancellationToken.None));
        Assert.NotNull(await Gate().OpenAsync(expiring, null, CancellationToken.None));
        _clock = new FixedTimeProvider(Now.AddDays(1));
        await Assert.ThrowsAsync<NotFoundException>(() => Gate().OpenAsync(expiring, null, CancellationToken.None));
    }

    [Fact]
    public async Task Un_jeton_inconnu_est_introuvable()
        => await Assert.ThrowsAsync<NotFoundException>(() => Gate().OpenAsync("pas-un-jeton", null, CancellationToken.None));

    [Fact]
    public async Task Un_lien_protege_exige_le_deverrouillage_de_ce_lien()
    {
        var token = TokenOf(await CreateAsync(password: "écoute-v3"));
        var other = await CreateAsync(password: "autre-lien");
        var link = _links.Links[0];

        await Assert.ThrowsAsync<ForbiddenException>(() => Gate().OpenAsync(token, null, CancellationToken.None));
        // Le cookie d'un autre lien, ou un cookie arrivé à sa fin, ne suffit pas
        var otherUnlock = _unlocks.Create(other.Link.Id, Now.AddHours(1));
        await Assert.ThrowsAsync<ForbiddenException>(() => Gate().OpenAsync(token, otherUnlock, CancellationToken.None));
        var stale = _unlocks.Create(link.Id, Now);
        await Assert.ThrowsAsync<ForbiddenException>(() => Gate().OpenAsync(token, stale, CancellationToken.None));

        Assert.Same(link, await Gate().OpenAsync(token, _unlocks.Create(link.Id, Now.AddHours(1)), CancellationToken.None));
    }

    [Fact]
    public async Task Unlock_journalise_un_mauvais_mot_de_passe_puis_refuse()
    {
        var token = TokenOf(await CreateAsync(password: "écoute-v3"));
        var handler = new UnlockShareLinkHandler(Gate(), _passwords, _unlocks, new ShareAccessLog(_links), _uow, _clock);

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(token, "mauvais", Visitor, CancellationToken.None));
        Assert.Equal(ShareAccessKind.WrongPassword, Assert.Single(_links.Accesses).Kind);

        var unlock = await handler.HandleAsync(token, "écoute-v3", Visitor, CancellationToken.None);
        Assert.Equal(Now + UnlockShareLinkHandler.UnlockLifetime, unlock.ExpiresAt);
        Assert.True(_unlocks.TryRead(unlock.UnlockToken, out var linkId, out _));
        Assert.Equal(_links.Links[0].Id, linkId);
    }

    [Fact]
    public async Task Unlock_ne_depasse_jamais_l_expiration_du_lien()
    {
        var token = TokenOf(await CreateAsync(password: "écoute-v3", days: 1));
        _clock = new FixedTimeProvider(Now.AddHours(20));
        var handler = new UnlockShareLinkHandler(Gate(), _passwords, _unlocks, new ShareAccessLog(_links), _uow, _clock);

        var unlock = await handler.HandleAsync(token, "écoute-v3", Visitor, CancellationToken.None);

        Assert.Equal(Now.AddDays(1), unlock.ExpiresAt);
    }

    [Fact]
    public async Task Open_journalise_une_empreinte_de_l_ip_jamais_l_ip()
    {
        var token = TokenOf(await CreateAsync());
        var handler = new OpenSharedContentHandler(Gate(), _links, new ShareAccessLog(_links), _uow, _clock);

        var content = await handler.HandleAsync(token, null, Visitor, CancellationToken.None);

        Assert.Equal("Aube", content.Title);
        var access = Assert.Single(_links.Accesses);
        Assert.Equal(ShareAccessKind.Opened, access.Kind);
        Assert.Equal(ShareAccessLog.HashIp(_links.Links[0].AccessSalt, "203.0.113.7"), access.IpHash);
        Assert.Equal("Mozilla/5.0 (Macintosh)", access.UserAgent);
    }

    [Fact]
    public void L_empreinte_de_l_ip_depend_du_lien()
    {
        var a = ShareAccessLog.HashIp([1, 2, 3], "203.0.113.7");
        var b = ShareAccessLog.HashIp([4, 5, 6], "203.0.113.7");

        Assert.Equal(32, a!.Length);
        Assert.NotEqual(a, b);
        Assert.Null(ShareAccessLog.HashIp([1, 2, 3], null));
    }

    [Fact]
    public async Task Fichier_seul_le_debut_de_l_ecoute_entre_au_journal_et_rien_hors_de_la_cible()
    {
        var token = TokenOf(await CreateAsync());
        var link = _links.Links[0];
        var fileId = Guid.CreateVersion7();
        _links.Files[(link.Id, fileId)] = new StoredFileInfo("abc.mp3", "audio/mpeg", "aube-mix.mp3");
        _storage.Files["abc.mp3"] = [1, 2, 3];
        var handler = new DownloadSharedFileHandler(Gate(), _links, _storage, new ShareAccessLog(_links), _uow, _clock);

        var start = await handler.HandleAsync(token, null, fileId, countAsPlay: true, Visitor, CancellationToken.None);
        await handler.HandleAsync(token, null, fileId, countAsPlay: false, Visitor, CancellationToken.None);

        Assert.Equal("audio/mpeg", start.ContentType);
        var played = Assert.Single(_links.Accesses);
        Assert.Equal((ShareAccessKind.Played, fileId), (played.Kind, played.FileId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(token, null, Guid.CreateVersion7(), true, Visitor, CancellationToken.None));
    }

    [Fact]
    public async Task Le_journal_d_un_lien_d_un_autre_groupe_est_introuvable()
    {
        await CreateAsync();
        var handler = new ListShareLinkAccessesHandler(_links, new FakeBandContext(Guid.CreateVersion7()));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(_links.Links[0].Id, CancellationToken.None));
    }
}