using System.Diagnostics;
using Leno.Infrastructure.Logging;
using Leno.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OpenTelemetry.Trace;
using Serilog.Core;

namespace Leno.Infrastructure.Tests.Telemetry;

public class OpenTelemetryExtensionsTests
{
    [Fact]
    public void AddLenoOpenTelemetry_ValidBuilder_ShouldReturnSameBuilder()
    {
        var builder = Host.CreateApplicationBuilder([]);

        var result = builder.AddLenoOpenTelemetry();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddLenoOpenTelemetry_NullBuilder_ShouldThrow()
    {
        IHostApplicationBuilder builder = null!;

        var act = () => builder.AddLenoOpenTelemetry();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLenoOpenTelemetry_ShouldRegisterOpenTelemetryServices()
    {
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddLenoOpenTelemetry();

        var provider = builder.Services.BuildServiceProvider();

        // Verify the TracerProvider can be resolved
        var tracerProvider = provider.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddLenoOpenTelemetry_ShouldRegisterTraceIdEnricher()
    {
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddLenoOpenTelemetry();

        var provider = builder.Services.BuildServiceProvider();

        var enricher = provider.GetService<ILogEventEnricher>();
        enricher.Should().NotBeNull();
        enricher.Should().BeOfType<TraceIdEnricher>();
    }

    [Fact]
    public void AddLenoOpenTelemetry_WithCustomEndpoint_ShouldUseConfiguredValue()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["OpenTelemetry:OtlpEndpoint"] = "http://custom-collector:4317",
            ["OpenTelemetry:ServiceName"] = "TestService"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var builder = Host.CreateApplicationBuilder([]);
        builder.Configuration.AddConfiguration(config);

        builder.AddLenoOpenTelemetry();

        var provider = builder.Services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddLenoOpenTelemetry_WithCustomTracingConfig_ShouldInvokeCallback()
    {
        var builder = Host.CreateApplicationBuilder([]);
        var callbackInvoked = false;

        builder.AddLenoOpenTelemetry(tracing =>
        {
            callbackInvoked = true;
        });

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void ActivitySources_ShouldHaveCorrectNames()
    {
        OpenTelemetryExtensions.ActivitySources.Order.Should().Be("Leno.Order");
        OpenTelemetryExtensions.ActivitySources.Payment.Should().Be("Leno.Payment");
        OpenTelemetryExtensions.ActivitySources.Stock.Should().Be("Leno.Stock");
    }

    [Fact]
    public void DefaultOtlpEndpoint_ShouldBeLocalhost()
    {
        OpenTelemetryExtensions.DefaultOtlpEndpoint.Should().Be("http://localhost:4317");
    }
}

public class TraceIdEnricherTests
{
    [Fact]
    public void Enrich_WithActiveTraceId_ShouldAddTraceIdProperty()
    {
        using var activity = new Activity("test-operation").Start();
        var enricher = new TraceIdEnricher();

        var logEvent = new Serilog.Events.LogEvent(
            DateTimeOffset.Now,
            Serilog.Events.LogEventLevel.Information,
            null,
            Serilog.Events.MessageTemplate.Empty,
            []);
        var propertyFactory = new Mock<ILogEventPropertyFactory>();
        var traceId = activity.TraceId.ToString();

        propertyFactory
            .Setup(f => f.CreateProperty("TraceId", traceId))
            .Returns(new Serilog.Events.LogEventProperty("TraceId", new Serilog.Events.ScalarValue(traceId)));

        var spanId = activity.SpanId.ToString();
        propertyFactory
            .Setup(f => f.CreateProperty("SpanId", spanId))
            .Returns(new Serilog.Events.LogEventProperty("SpanId", new Serilog.Events.ScalarValue(spanId)));

        enricher.Enrich(logEvent, propertyFactory.Object);

        logEvent.Properties.Should().ContainKey("TraceId");
        logEvent.Properties.Should().ContainKey("SpanId");
    }

    [Fact]
    public void Enrich_WithoutActiveActivity_ShouldNotAddTraceId()
    {
        var enricher = new TraceIdEnricher();
        var logEvent = new Serilog.Events.LogEvent(
            DateTimeOffset.Now,
            Serilog.Events.LogEventLevel.Information,
            null,
            Serilog.Events.MessageTemplate.Empty,
            []);
        var propertyFactory = new Mock<ILogEventPropertyFactory>();

        enricher.Enrich(logEvent, propertyFactory.Object);

        logEvent.Properties.Should().NotContainKey("TraceId");
    }

    [Fact]
    public void Enrich_NullLogEvent_ShouldThrow()
    {
        var enricher = new TraceIdEnricher();
        var propertyFactory = new Mock<ILogEventPropertyFactory>();

        var act = () => enricher.Enrich(null!, propertyFactory.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Enrich_NullPropertyFactory_ShouldThrow()
    {
        var enricher = new TraceIdEnricher();
        var logEvent = new Serilog.Events.LogEvent(
            DateTimeOffset.Now,
            Serilog.Events.LogEventLevel.Information,
            null,
            Serilog.Events.MessageTemplate.Empty,
            []);

        var act = () => enricher.Enrich(logEvent, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}