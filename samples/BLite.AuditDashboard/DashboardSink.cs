using System.Collections.Concurrent;
using BLite.Core.Audit;

namespace BLite.AuditDashboard;

/// <summary>Riga di evento mostrata nella dashboard.</summary>
public record EventRow(string Time, string Type, string Detail);

/// <summary>
/// Sink che accumula gli eventi in memoria per mostrarli nella dashboard web.
/// Usa una coda concorrente: i metodi del sink sono chiamati dal thread
/// dell'operazione del database, mentre la pagina web legge da un altro thread.
/// Tiene solo gli ultimi N eventi per non consumare memoria all'infinito.
/// </summary>
public sealed class DashboardSink : IBLiteAuditSink
{
    private const int MaxEvents = 50;
    private readonly ConcurrentQueue<EventRow> _events = new();

    public IReadOnlyList<EventRow> Recent => _events.Reverse().ToList();

    public void OnInsert(InsertAuditEvent e) => Add("INSERT",
        $"{e.CollectionName} · {e.DocumentSizeBytes} B · {Ms(e.Elapsed)}");

    public void OnQuery(QueryAuditEvent e) => Add("QUERY",
        $"{e.CollectionName} · <b>{e.Strategy}</b>" +
        (e.IndexName is null ? "" : $" ({e.IndexName})") +
        $" · {e.ResultCount} risultati · {Ms(e.Elapsed)}");

    public void OnCommit(CommitAuditEvent e) => Add("COMMIT",
        $"tx {e.TransactionId} · {e.PagesWritten} pagine · {Ms(e.Elapsed)}");

    public void OnSlowOperation(SlowOperationEvent e) => Add("SLOW",
        $"{e.OperationType} · {e.CollectionName} · {Ms(e.Elapsed)}");

    private static string Ms(TimeSpan t) => $"{t.TotalMilliseconds:F2} ms";

    private void Add(string type, string detail)
    {
        _events.Enqueue(new EventRow(DateTime.Now.ToString("HH:mm:ss.fff"), type, detail));
        while (_events.Count > MaxEvents) _events.TryDequeue(out _);
    }
}
