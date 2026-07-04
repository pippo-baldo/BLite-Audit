using System.Threading;

namespace BLite.Core.Audit;

/// <summary>
/// Contatori cumulativi thread-safe. Accessibili via BLiteEngine.Metrics / DocumentDbContext.Metrics.
/// Tutti gli aggiornamenti usano Interlocked per ~10-20 ns overhead.
/// </summary>
public sealed class BLiteMetrics
{
    private long _totalInserts;
    private long _totalQueriesIndexScan;
    private long _totalQueriesBsonScan;
    private long _totalQueriesFullScan;
    private long _totalCommits;
    private long _pageCacheHits;       // hit su _walCache o _walIndex
    private long _pageCacheMisses;     // lettura da PageFile
    private long _totalInsertMs;       // per calcolo media mobile
    private long _totalQueryMs;

    // ── Lettura (snapshot istantaneo) ───────────────────────────────────────────
    public long TotalInserts            => Interlocked.Read(ref _totalInserts);
    public long TotalQueriesIndexScan   => Interlocked.Read(ref _totalQueriesIndexScan);
    public long TotalQueriesBsonScan    => Interlocked.Read(ref _totalQueriesBsonScan);
    public long TotalQueriesFullScan    => Interlocked.Read(ref _totalQueriesFullScan);
    public long TotalCommits            => Interlocked.Read(ref _totalCommits);
    public long PageCacheHits           => Interlocked.Read(ref _pageCacheHits);
    public long PageCacheMisses         => Interlocked.Read(ref _pageCacheMisses);

    public double AvgInsertMs =>
        _totalInserts == 0 ? 0 : (double)Interlocked.Read(ref _totalInsertMs) / _totalInserts;

    public double AvgQueryMs =>
        TotalQueries == 0 ? 0 : (double)Interlocked.Read(ref _totalQueryMs) / TotalQueries;

    public long TotalQueries =>
        TotalQueriesIndexScan + TotalQueriesBsonScan + TotalQueriesFullScan;

    public double CacheHitRate
    {
        get
        {
            var total = PageCacheHits + PageCacheMisses;
            return total == 0 ? 0 : (double)PageCacheHits / total;
        }
    }

    // ── Aggiornamento (interno a BLite.Core) ────────────────────────────────────
    internal void RecordInsert(TimeSpan elapsed)
    {
        Interlocked.Increment(ref _totalInserts);
        Interlocked.Add(ref _totalInsertMs, (long)elapsed.TotalMilliseconds);
    }

    internal void RecordQuery(QueryStrategy strategy, TimeSpan elapsed)
    {
        switch (strategy)
        {
            case QueryStrategy.IndexScan: Interlocked.Increment(ref _totalQueriesIndexScan); break;
            case QueryStrategy.BsonScan:  Interlocked.Increment(ref _totalQueriesBsonScan);  break;
            default:                      Interlocked.Increment(ref _totalQueriesFullScan);  break;
        }
        Interlocked.Add(ref _totalQueryMs, (long)elapsed.TotalMilliseconds);
    }

    internal void RecordCommit()   => Interlocked.Increment(ref _totalCommits);
    internal void RecordCacheHit() => Interlocked.Increment(ref _pageCacheHits);
    internal void RecordCacheMiss()=> Interlocked.Increment(ref _pageCacheMisses);

    /// <summary>Azzera tutti i contatori (utile per test o reset periodico).</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalInserts, 0);
        Interlocked.Exchange(ref _totalQueriesIndexScan, 0);
        Interlocked.Exchange(ref _totalQueriesBsonScan, 0);
        Interlocked.Exchange(ref _totalQueriesFullScan, 0);
        Interlocked.Exchange(ref _totalCommits, 0);
        Interlocked.Exchange(ref _pageCacheHits, 0);
        Interlocked.Exchange(ref _pageCacheMisses, 0);
        Interlocked.Exchange(ref _totalInsertMs, 0);
        Interlocked.Exchange(ref _totalQueryMs, 0);
    }
}
