using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Sync;

/// <summary>The sync queue and its state, shared by the worker and the UI.</summary>
public sealed class SyncCoordinator(TimeProvider time)
{
    private readonly Channel<SyncRequest> _queue = Channel.CreateUnbounded<SyncRequest>(new UnboundedChannelOptions { SingleReader = true });

    public bool IsRunning { get; private set; }
    public string? CurrentStep { get; private set; }
    public DateTimeOffset? LastFinishedAt { get; private set; }

    /// <summary>Raised when the state above changes. Handlers run on the worker's thread.</summary>
    public event Action? StateChanged;

    /// <summary>Queues a sync; it starts when the current one (if any) ends.</summary>
    public void Request(SyncRequest request) => _queue.Writer.TryWrite(request);

    internal ChannelReader<SyncRequest> Reader => _queue.Reader;

    internal void Started()
    {
        IsRunning = true;
        CurrentStep = null;
        StateChanged?.Invoke();
    }

    /// <param name="synced">False when nothing ran (no account with a key): the last sync time stays as it was.</param>
    internal void Finished(bool synced)
    {
        IsRunning = false;
        CurrentStep = null;
        if (synced)
            LastFinishedAt = time.GetUtcNow();
        StateChanged?.Invoke();
    }

    internal void SetStep(string step)
    {
        CurrentStep = step;
        StateChanged?.Invoke();
    }
}

/// <summary>Runs queued syncs one at a time, and queues one every <see cref="SyncOptions.Interval"/>.</summary>
public sealed class SyncWorker(
    SyncCoordinator coordinator,
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    TimeProvider time,
    ILogger<SyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = ScheduleAsync(stoppingToken);

        await foreach (var request in coordinator.Reader.ReadAllAsync(stoppingToken))
        {
            coordinator.Started();
            var synced = false;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<SyncRunner>();
                runner.Progress += coordinator.SetStep;
                synced = await runner.RunAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync failed");
            }
            finally
            {
                coordinator.Finished(synced);
            }
        }

        await schedule;
    }

    private async Task ScheduleAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Scheduled sync is off (Sync:Enabled = false)");
            return;
        }

        if (settings.RunOnStartup)
            coordinator.Request(new SyncRequest(Reason: "startup"));

        using var timer = new PeriodicTimer(settings.Interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                coordinator.Request(new SyncRequest(Reason: "schedule"));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
