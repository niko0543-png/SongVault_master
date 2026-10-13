using System.Collections.Concurrent;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SongVault.Application.Abstractions;
using SongVault.Infrastructure.Jobs;
using SongVault.Infrastructure.Persistence;

namespace SongVault.Api.Tests.Jobs;

[Collection(ApiCollection.Name)]
public sealed class BackgroundJobsTests(SongVaultApiFactory factory)
{
    private readonly TestClock _clock = new(DateTimeOffset.UtcNow);
    private readonly RecordingHandler _handler = new();

    private WebApplicationFactory<Program> Host()
        => factory.WithClock(_clock, s => s.AddSingleton<IBackgroundJobHandler>(_handler));

    private static string NewPayload(string prefix = "") => $"{prefix}{Guid.NewGuid():N}";

    private static async Task EnqueueAsync(IServiceProvider services, string payload, DateTimeOffset runAfter)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IBackgroundJobs>().Enqueue(RecordingHandler.JobType, payload, runAfter);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private static async Task<BackgroundJob> JobAsync(IServiceProvider services, string payload)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SongVaultDbContext>()
            .BackgroundJobs.AsNoTracking().SingleAsync(j => j.Payload == payload);
    }

    [Fact]
    public async Task Une_tache_due_s_execute_une_fois()
    {
        await using var host = Host();
        var payload = NewPayload();
        await EnqueueAsync(host.Services, payload, _clock.GetUtcNow());

        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);

        Assert.Single(_handler.Runs, r => r.Payload == payload);
        var job = await JobAsync(host.Services, payload);
        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public async Task Une_tache_programmee_attend_son_heure()
    {
        await using var host = Host();
        var payload = NewPayload();
        await EnqueueAsync(host.Services, payload, _clock.GetUtcNow().AddHours(1));

        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        Assert.DoesNotContain(_handler.Runs, r => r.Payload == payload);

        _clock.Advance(TimeSpan.FromHours(1));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        Assert.Single(_handler.Runs, r => r.Payload == payload);
    }

    [Fact]
    public async Task Une_tache_en_echec_est_reessayee_puis_marquee_Failed()
    {
        await using var host = Host();
        var payload = NewPayload("fail:");
        await EnqueueAsync(host.Services, payload, _clock.GetUtcNow());

        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        var job = await JobAsync(host.Services, payload);
        Assert.Equal(BackgroundJobStatus.Pending, job.Status);
        Assert.Equal(_clock.GetUtcNow().AddMinutes(1), job.RunAfter);          // 1er nouvel essai dans 1 minute

        _clock.Advance(TimeSpan.FromMinutes(1));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        job = await JobAsync(host.Services, payload);
        Assert.Equal(_clock.GetUtcNow().AddMinutes(5), job.RunAfter);          // 2e dans 5 minutes

        _clock.Advance(TimeSpan.FromMinutes(5));
        await SongVaultApiFactory.RunDueJobsAsync(host.Services);
        job = await JobAsync(host.Services, payload);
        Assert.Equal(BackgroundJobStatus.Failed, job.Status);
        Assert.Equal(3, job.Attempts);
        Assert.Contains("Échec voulu par le test.", job.Error);
        Assert.Equal(new[] { false, false, true },
            _handler.Runs.Where(r => r.Payload == payload).Select(r => r.IsLastAttempt));
    }

    [Fact]
    public async Task Plusieurs_instances_ne_prennent_jamais_la_meme_tache()
    {
        await using var host = Host();
        var prefix = $"para-{Guid.NewGuid():N}-";
        var payloads = Enumerable.Range(0, 20).Select(i => $"{prefix}{i}").ToList();
        foreach (var payload in payloads)
            await EnqueueAsync(host.Services, payload, _clock.GetUtcNow());

        // Quatre « instances de l'API » qui dépilent en même temps
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            Task.Run(() => SongVaultApiFactory.RunDueJobsAsync(host.Services))));

        foreach (var payload in payloads)
            Assert.Single(_handler.Runs, r => r.Payload == payload);
    }

    private sealed class RecordingHandler : IBackgroundJobHandler
    {
        public const string JobType = "test.recording";

        public ConcurrentQueue<BackgroundJobContext> Runs { get; } = new();

        public string Type => JobType;

        public Task RunAsync(BackgroundJobContext job, CancellationToken ct)
        {
            Runs.Enqueue(job);
            return job.Payload.StartsWith("fail:", StringComparison.Ordinal)
                ? throw new InvalidOperationException("Échec voulu par le test.")
                : Task.CompletedTask;
        }
    }
}