using System.Diagnostics;
using System.Reflection;

namespace BLite.Core.Audit;

/// <summary>
/// Sorgente Activity per l'integrazione con OpenTelemetry / DiagnosticListener.
/// Un consumer si registra su "BLite.Core" per ricevere le tracce.
/// Se nessun listener e' attivo, StartActivity() ritorna null: overhead ~zero.
/// </summary>
public static class BLiteDiagnostics
{
    /// <summary>Nome della sorgente, usato dai listener OpenTelemetry.</summary>
    public const string SourceName = "BLite.Core";

    /// <summary>Versione letta dall'assembly (evita costanti hardcoded).</summary>
    public static readonly string Version =
        typeof(BLiteDiagnostics).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static readonly ActivitySource ActivitySource = new(SourceName, Version);
}
