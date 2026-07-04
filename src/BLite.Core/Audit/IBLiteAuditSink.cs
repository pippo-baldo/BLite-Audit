namespace BLite.Core.Audit;

/// <summary>
/// Interfaccia implementabile dall'utente per ricevere eventi di audit.
/// Tutti i metodi vengono chiamati in modo sincrono nel thread dell'operazione.
/// Implementazioni lente (es. scrittura su file) devono accodarsi internamente.
/// </summary>
public interface IBLiteAuditSink
{
    void OnInsert(InsertAuditEvent e)  { }  // default: no-op (interfaccia con default impl)
    void OnQuery(QueryAuditEvent e)    { }
    void OnCommit(CommitAuditEvent e)  { }
    void OnSlowOperation(SlowOperationEvent e) { }  // Fase 2 — può essere ignorato in Fase 1
}
