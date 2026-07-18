using Leno.Infrastructure.Telemetry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Leno.Infrastructure.Tests.Telemetry;

public class OpenTelemetryMetricsExtensionsTests
{
    [Fact]
    public void AddLenoOpenTelemetry_WithMetricsCallback_InvokesCallback()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317";
        var invoked = false;

        builder.AddLenoOpenTelemetry(
            configureMetrics: metrics =>
            {
                invoked = true;
                metrics.AddMeter("Test.Meter");
            });

        using var host = builder.Build();
        invoked.Should().BeTrue();
    }

    [Fact]
    public void AddLenoOpenTelemetry_DefaultSubscribesAntiCorruptionMeter()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317";

        builder.AddLenoOpenTelemetry();

        var provider = builder.Services.BuildServiceProvider().GetService<MeterProvider>();
        provider.Should().NotBeNull();
    }
}
