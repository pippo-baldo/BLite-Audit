namespace BLite.Core.Audit;

/// <summary>Emesso al completamento di ogni CommitTransaction.</summary>
public readonly record struct CommitAuditEvent(
    ulong TransactionId,
    string CollectionName,   // "" se commit coinvolge più collection o è cross-collection
    int PagesWritten,
    int WalSizeBytes,
    TimeSpan Elapsed);

/// <summary>Emesso al completamento di ogni InsertDataCore.</summary>
public readonly record struct InsertAuditEvent(
    ulong TransactionId,
    string CollectionName,
    int DocumentSizeBytes,
    TimeSpan Elapsed);

/// <summary>Emesso al completamento di ogni Execute in BTreeQueryProvider.</summary>
public readonly record struct QueryAuditEvent(
    string CollectionName,
    QueryStrategy Strategy,
    string? IndexName,           // null se Strategy != IndexScan
    int ResultCount,
    TimeSpan Elapsed);

/// <summary>Emesso quando un'operazione supera la soglia configurata (Fase 2).</summary>
public readonly record struct SlowOperationEvent(
    SlowOperationType OperationType,
    string CollectionName,
    TimeSpan Elapsed,
    string? Detail);             // es. espressione LINQ o nome indice

public enum QueryStrategy : byte
{
    Unknown = 0,
    IndexScan = 1,
    BsonScan = 2,
    FullScan = 3
}

public enum SlowOperationType : byte
{
    Insert = 1,
    Query = 2,
    Commit = 3
}
