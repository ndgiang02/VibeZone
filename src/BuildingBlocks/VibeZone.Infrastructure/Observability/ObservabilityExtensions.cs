using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace VibeZone.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Serilog to stdout. JSON (compact, with trace/span ids) by default; set
    /// <c>Serilog:ConsoleFormat=text</c> for human-readable local output.
    /// Never log request headers/bodies here: they carry cookies, tokens and PII.
    /// </summary>
    public static IHostApplicationBuilder AddVibeZoneLogging(this IHostApplicationBuilder builder)
    {
        var json = !string.Equals(builder.Configuration["Serilog:ConsoleFormat"], "text", StringComparison.OrdinalIgnoreCase);

        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service", builder.Environment.ApplicationName);

            if (json)
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
            else
            {
                logger.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
            }
        });
        return builder;
    }

    /// <summary>
    /// OpenTelemetry traces and metrics. Exported over OTLP only when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is configured.
    /// </summary>
    public static IHostApplicationBuilder AddVibeZoneTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        var otlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal))
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();
                if (otlp)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddNpgsqlInstrumentation();
                if (otlp)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return builder;
    }
}
