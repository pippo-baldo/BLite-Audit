namespace BLite.Core.Audit;

public sealed class BLiteAuditOptions
{
    /// <summary>Sink custom dell'utente. Se null, nessun callback viene invocato.</summary>
    public IBLiteAuditSink? Sink { get; set; }

    /// <summary>
    /// Se true, popola il singleton BLiteMetrics accessibile via BLiteEngine.Metrics
    /// e DocumentDbContext.Metrics.
    /// </summary>
    public bool EnableMetrics { get; set; } = false;

    // ── Fase 2 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Soglia oltre la quale viene emesso un SlowOperationEvent sul sink.
    /// null = soglia disabilitata.
    /// </summary>
    public TimeSpan? SlowQueryThreshold { get; set; }

    /// <summary>
    /// Se true, emette System.Diagnostics.Activity tramite BLiteDiagnostics.ActivitySource.
    /// Richiede un listener OpenTelemetry o DiagnosticListener attivo per avere overhead.
    /// </summary>
    public bool EnableDiagnosticSource { get; set; } = false;
}
