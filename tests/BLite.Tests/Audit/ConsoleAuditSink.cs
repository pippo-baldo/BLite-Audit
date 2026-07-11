using System;
using BLite.Core.Audit;

namespace BLite.Tests;

/// <summary>
/// Sink dimostrativo: stampa ogni evento di audit sulla console.
/// Mostra l'uso tipico di IBLiteAuditSink da parte di un'applicazione host.
///
/// Nota: i metodi vengono invocati in modo sincrono sul thread dell'operazione,
/// quindi un sink reale che fa I/O (file, rete) dovrebbe accodare gli eventi
/// invece di scriverli direttamente, per non rallentare il database.
/// </summary>
public sealed class ConsoleAuditSink : IBLiteAuditSink
{
    private readonly bool _useColor;

    public ConsoleAuditSink(bool useColor = true) => _useColor = useColor;

    public void OnInsert(InsertAuditEvent e) =>
        Write(ConsoleColor.Green, "INSERT",
            $"collection={e.CollectionName} size={e.DocumentSizeBytes}B " +
            $"tx={e.TransactionId} elapsed={Ms(e.Elapsed)}");

    public void OnQuery(QueryAuditEvent e) =>
        Write(ConsoleColor.Cyan, "QUERY ",
            $"collection={e.CollectionName} strategy={e.Strategy} " +
            $"index={e.IndexName ?? "-"} results={e.ResultCount} elapsed={Ms(e.Elapsed)}");

    public void OnCommit(CommitAuditEvent e) =>
        Write(ConsoleColor.Yellow, "COMMIT",
            $"tx={e.TransactionId} pages={e.PagesWritten} " +
            $"wal={e.WalSizeBytes}B elapsed={Ms(e.Elapsed)}");

    public void OnSlowOperation(SlowOperationEvent e) =>
        Write(ConsoleColor.Red, "SLOW  ",
            $"op={e.OperationType} collection={e.CollectionName} " +
            $"elapsed={Ms(e.Elapsed)} detail={e.Detail ?? "-"}");

    private static string Ms(TimeSpan t) => $"{t.TotalMilliseconds:F2}ms";

    private void Write(ConsoleColor color, string tag, string message)
    {
        if (_useColor)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write($"[audit] {tag} ");
            Console.ForegroundColor = prev;
            Console.WriteLine(message);
        }
        else
        {
            Console.WriteLine($"[audit] {tag} {message}");
        }
    }
}
