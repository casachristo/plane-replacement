using System.Diagnostics.Metrics;

namespace Waypoint.Api.Observability;

/// <summary>
/// Custom metrics exposed at /metrics (via the System.Diagnostics.Metrics integration
/// with prometheus-net or OpenTelemetry's PrometheusExporter). Scrape via
/// the in-cluster Prometheus; NetworkPolicy clause for the monitoring namespace
/// added in Phase 7 follow-up.
/// </summary>
public static class WaypointMetrics
{
    public const string MeterName = "Waypoint.Api";
    private static readonly Meter Meter = new(MeterName, "0.1.0");

    public static readonly Counter<long> WebhookDeliveriesAttempted =
        Meter.CreateCounter<long>("waypoint_webhook_deliveries_attempted_total",
            description: "Webhook deliveries attempted, by status.");

    public static readonly Counter<long> TokensUsed =
        Meter.CreateCounter<long>("waypoint_tokens_used_total",
            description: "Service token authentications, by token name.");

    public static readonly Counter<long> ImporterRowsProcessed =
        Meter.CreateCounter<long>("waypoint_importer_rows_processed_total",
            description: "Rows processed by the Plane→Waypoint importer, by entity type.");

    public static readonly Histogram<double> EndpointDurationMs =
        Meter.CreateHistogram<double>("waypoint_endpoint_duration_ms",
            description: "Endpoint handler duration in milliseconds.");
}
