using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Runtime;

var builder = Host.CreateApplicationBuilder(args);

// Ensure Kestrel binds to container network interface for health/dashboard if ASPNETCORE_URLS is not honored elsewhere.
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:8080";
builder.WebHost.UseUrls(urls);

builder.AddKeyedRedisClient("orleans-redis");
builder.Logging.AddFilter("Orleans.Runtime.Placement.Rebalancing", LogLevel.Trace);
#pragma warning disable ORLEANSEXP002
builder.UseOrleans(builder => builder
    .Configure<GrainCollectionOptions>(o =>
    {
        o.CollectionQuantum = TimeSpan.FromSeconds(15);
    })
    .Configure<ResourceOptimizedPlacementOptions>(o =>
    {
        o.LocalSiloPreferenceMargin = 0;
    })
    .Configure<ActivationRebalancerOptions>(o =>
    {
        o.RebalancerDueTime = TimeSpan.FromSeconds(5);
        o.SessionCyclePeriod = TimeSpan.FromSeconds(5);
        // uncomment these below, if you want higher migration rate
        //o.CycleNumberWeight = 1;
        //o.SiloNumberWeight = 0; 
    })
    .AddActivationRebalancer());
#pragma warning restore ORLEANSEXP002

// Register health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Service is running"))
    .AddCheck<OrleansHealthCheck>("orleans_silo");

builder.Services.AddHostedService<LoadDriverBackgroundService>();
var app = builder.Build();

// Map health check endpoints
// /health/live - Liveness probe (minimal check for Kubernetes)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Name == "self"
});

// /health/ready - Readiness probe (includes Orleans silo status)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true
});

await app.RunAsync();

// Orleans health check implementation
internal class OrleansHealthCheck : IHealthCheck
{
    private readonly ILocalSiloDetails _siloDetails;
    private readonly IClusterMembershipService _membershipService;

    public OrleansHealthCheck(ILocalSiloDetails siloDetails, IClusterMembershipService membershipService)
    {
        _siloDetails = siloDetails;
        _membershipService = membershipService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await _membershipService.GetCurrentSnapshotAsync();
            var siloAddress = _siloDetails.SiloAddress;
            
            if (snapshot.Members.TryGetValue(siloAddress, out var siloStatus))
            {
                if (siloStatus.Status == SiloStatus.Active)
                {
                    return HealthCheckResult.Healthy($"Silo {siloAddress} is active in the cluster");
                }
                else
                {
                    return HealthCheckResult.Unhealthy($"Silo {siloAddress} status is {siloStatus.Status}");
                }
            }
            
            return HealthCheckResult.Unhealthy($"Silo {siloAddress} not found in cluster membership");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check Orleans silo status", ex);
        }
    }
}

internal class LoadDriverBackgroundService(IGrainFactory client) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            for (var i = 0; i < 5 * Random.Shared.Next(1, 1000); i++)
            {
                await client.GetGrain<IRebalancingTestGrain>(Guid.NewGuid()).Ping();
            }

            await Task.Delay(Random.Shared.Next(500, 1_000), stoppingToken);
        }
    }
}

public interface IRebalancingTestGrain : IGrainWithGuidKey
{
    Task Ping();
}

[CollectionAgeLimit(Minutes = 0.5)]
public class RebalancingTestGrain : Grain, IRebalancingTestGrain
{
    public Task Ping() => Task.CompletedTask;
}
