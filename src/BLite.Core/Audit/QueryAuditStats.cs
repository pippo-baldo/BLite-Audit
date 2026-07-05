namespace BLite.Core.Audit;

/// <summary>
/// Contenitore mutabile per far "risalire" la strategia scelta da
/// DocumentCollection.FetchAsync fino a BTreeQueryProvider, che poi emette
/// il QueryAuditEvent. Interno: non fa parte dell'API pubblica.
/// </summary>
internal sealed class QueryAuditStats
{
    public QueryStrategy Strategy;
    public string? IndexName;
}
