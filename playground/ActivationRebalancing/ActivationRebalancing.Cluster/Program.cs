using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Orleans.Configuration;
using Orleans.Runtime;

var builder = Host.CreateApplicationBuilder(args);

// Ensure Kestrel binds to container network interface for health/dashboard if ASPNETCORE_URLS is not honored elsewhere.
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:8080";
builder.WebHost.UseUrls(urls);

// Configure OpenTelemetry
var assemblyName = Assembly.GetExecutingAssembly().GetName();
var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? assemblyName.Name ?? "Orleans.Silo";
var serviceVersion = assemblyName.Version?.ToString() ?? "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddSource("Orleans.Grains") // Custom ActivitySource for grain operations
            .AddConsoleExporter(); // Always enable console exporter

        // Add OTLP exporter if endpoint is configured
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                
                // Optional: Add headers if configured
                var otlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    foreach (var header in otlpHeaders.Split(','))
                    {
                        var parts = header.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            options.Headers += $"{parts[0].Trim()}={parts[1].Trim()},";
                        }
                    }
                }
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddConsoleExporter(); // Always enable console exporter

        // Add OTLP exporter if endpoint is configured
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
        }
    });

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
    // ActivitySource for custom tracing in grain operations
    private static readonly ActivitySource ActivitySource = new("Orleans.Grains");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Example: Create a custom Activity to trace grain operations
            using var activity = ActivitySource.StartActivity("LoadDriver.GenerateLoad", ActivityKind.Internal);
            
            var grainCount = 5 * Random.Shared.Next(1, 1000);
            activity?.SetTag("grain.count", grainCount);
            
            try
            {
                for (var i = 0; i < grainCount; i++)
                {
                    await client.GetGrain<IRebalancingTestGrain>(Guid.NewGuid()).Ping();
                }
                
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw;
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
    // ActivitySource for tracing grain method calls
    private static readonly ActivitySource ActivitySource = new("Orleans.Grains");

    public Task Ping()
    {
        // Example: Create a span for grain method execution
        using var activity = ActivitySource.StartActivity("RebalancingTestGrain.Ping", ActivityKind.Server);
        activity?.SetTag("grain.type", nameof(RebalancingTestGrain));
        activity?.SetTag("grain.key", this.GetPrimaryKey().ToString());
        
        // Simulate some work
        return Task.CompletedTask;
    }
}
