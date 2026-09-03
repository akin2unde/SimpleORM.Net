using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Metadata;
using SimpleORM.Net.SystemModels;

namespace SimpleORM.Net.Services;

internal sealed class StaleDataCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDBMetadataProvider _metadata;
    private readonly ModelAssemblyRegistry _registry;
    private readonly SimpleOrmOptions _options;
    private readonly ILogger<StaleDataCleanupHostedService> _logger;

    public StaleDataCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IDBMetadataProvider metadata,
        ModelAssemblyRegistry registry,
        SimpleOrmOptions options,
        ILogger<StaleDataCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _metadata = metadata;
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        foreach (var modelType in ModelDiscovery.Discover(
                     _registry.Assemblies))
        {
            _metadata.RegisterModel(modelType);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var policies = BuildPolicies();

            if (policies.Count == 0)
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(5),
                    stoppingToken);
                continue;
            }

            var now = DateTime.UtcNow;
            var scheduled = policies
                .Select(policy => new
                {
                    Policy = policy,
                    Next = policy.Expression.GetNextOccurrence(
                        now,
                        TimeZoneInfo.Utc)
                })
                .Where(item => item.Next.HasValue)
                .ToArray();

            if (scheduled.Length == 0)
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(5),
                    stoppingToken);
                continue;
            }

            var nextRun = scheduled.Min(item => item.Next!.Value);
            var delay = nextRun - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                var wait = delay > TimeSpan.FromDays(1)
                    ? TimeSpan.FromDays(1)
                    : delay;

                await Task.Delay(
                    wait,
                    stoppingToken);

                if (wait < delay)
                {
                    continue;
                }
            }

            var dueAt = DateTime.UtcNow.AddSeconds(1);

            foreach (var item in scheduled.Where(
                         item => item.Next!.Value <= dueAt))
            {
                await ExecutePolicy(
                    item.Policy,
                    stoppingToken);
            }
        }
    }

    private IReadOnlyList<StaleDataCleanupPolicy> BuildPolicies()
    {
        var policies = new List<StaleDataCleanupPolicy>();

        foreach (var model in _metadata.GetRegisteredModels())
        {
            if (model.AutoDeleteAfterDays is not > 0
                || string.IsNullOrWhiteSpace(model.AutoDeleteCron))
            {
                continue;
            }

            policies.Add(
                CreatePolicy(
                    model,
                    model.AutoDeleteAfterDays.Value,
                    model.AutoDeleteCron));
        }

        if (_options.ErrorLog.Enabled
            && _options.ErrorLog.AutoDeleteEnabled)
        {
            var errorMetadata = _metadata.GetMetadata<DBErrorLog>();

            if (!policies.Any(policy =>
                    policy.Metadata.ModelType == typeof(DBErrorLog)))
            {
                policies.Add(
                    CreatePolicy(
                        errorMetadata,
                        _options.ErrorLog.RetentionDays,
                        _options.ErrorLog.CleanupCron));
            }
        }

        return policies;
    }

    private static StaleDataCleanupPolicy CreatePolicy(
        DBModelMetadata metadata,
        int retentionDays,
        string cron)
    {
        if (retentionDays <= 0)
        {
            throw new InvalidOperationException(
                $"Retention days for '{metadata.ModelName}' must be greater than zero.");
        }

        var expression = CronExpression.Parse(
            cron,
            CronFormat.Standard);

        return new StaleDataCleanupPolicy(
            metadata,
            retentionDays,
            expression);
    }

    private async Task ExecutePolicy(
        StaleDataCleanupPolicy policy,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider
                .GetRequiredService<IDatabaseProvider>();
            var cutoff = DateTime.UtcNow.AddDays(
                -policy.RetentionDays);

            var deleted = await provider.DeleteStale(
                policy.Metadata,
                cutoff,
                cancellationToken);

            _logger.LogInformation(
                "SimpleORM stale-data cleanup deleted {DeletedCount} {ModelName} records older than {CutoffUtc}.",
                deleted,
                policy.Metadata.ModelName,
                cutoff);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "SimpleORM stale-data cleanup failed for {ModelName}.",
                policy.Metadata.ModelName);
        }
    }

}
