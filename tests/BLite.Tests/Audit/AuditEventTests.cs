using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using BLite.Core.Audit;
using BLite.Shared;
using Xunit;

namespace BLite.Tests;

/// <summary>Sink di test: raccoglie tutti gli eventi ricevuti (thread-safe).</summary>
internal sealed class CollectingSink : IBLiteAuditSink
{
    private readonly object _lock = new();
    public readonly List<InsertAuditEvent> Inserts = new();
    public readonly List<QueryAuditEvent> Queries = new();
    public readonly List<CommitAuditEvent> Commits = new();
    public readonly List<SlowOperationEvent> Slow = new();

    public void OnInsert(InsertAuditEvent e) { lock (_lock) Inserts.Add(e); }
    public void OnQuery(QueryAuditEvent e) { lock (_lock) Queries.Add(e); }
    public void OnCommit(CommitAuditEvent e) { lock (_lock) Commits.Add(e); }
    public void OnSlowOperation(SlowOperationEvent e) { lock (_lock) Slow.Add(e); }
}

public class AuditEventTests : IDisposable
{
    // Nota: la collection registrata si chiama "testdocuments" (il source generator
    // normalizza il nome in minuscolo). Il campo Category e' gia' indicizzato nel
    // modello con l'indice 'idx_Category'; il campo Name NON e' indicizzato.
    private const string CollName = "testdocuments";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"blite_audit_{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private TestDbContext NewDb(CollectingSink? sink, bool metrics = true,
                                TimeSpan? slowThreshold = null) =>
        new(_path, new BLiteAuditOptions
        {
            Sink = sink,
            EnableMetrics = metrics,
            SlowQueryThreshold = slowThreshold
        });

    // ── 1. Eventi ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Insert_Emits_InsertAuditEvent_With_Correct_Data()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();

        var e = Assert.Single(sink.Inserts);
        Assert.Equal(CollName, e.CollectionName);
        Assert.True(e.DocumentSizeBytes > 0, "la dimensione del documento deve essere > 0");
        Assert.True(e.Elapsed >= TimeSpan.Zero);
        Assert.True(e.TransactionId > 0, "l'insert deve avere un transaction id valido");
    }

    [Fact]
    public async Task Commit_Emits_CommitAuditEvent()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();

        Assert.NotEmpty(sink.Commits);
        var e = sink.Commits[0];
        Assert.True(e.TransactionId > 0);
        Assert.True(e.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Query_Emits_QueryAuditEvent_With_ResultCount()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 2, Name = "Y" });
        await db.TestDocuments.InsertAsync(new TestDocument { Category = "B", Amount = 3, Name = "Z" });
        await db.SaveChangesAsync();

        var res = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();

        Assert.Equal(2, res.Count);
        var e = Assert.Single(sink.Queries);
        Assert.Equal(CollName, e.CollectionName);
        Assert.Equal(2, e.ResultCount);
    }

    // ── 2. Strategia di query ────────────────────────────────────────────────

    [Fact]
    public async Task Query_On_NonIndexed_Field_Is_Not_IndexScan()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();

        // Name NON e' indicizzato -> il motore non puo' usare un indice
        _ = db.TestDocuments.AsQueryable().Where(x => x.Name == "X").ToList();

        var e = Assert.Single(sink.Queries);
        Assert.NotEqual(QueryStrategy.IndexScan, e.Strategy);
        Assert.Null(e.IndexName);
    }

    [Fact]
    public async Task Query_On_Indexed_Field_Reports_IndexScan_And_IndexName()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.TestDocuments.InsertAsync(new TestDocument { Category = "B", Amount = 2, Name = "Y" });
        await db.SaveChangesAsync();

        // Category e' indicizzato nel modello -> deve risultare IndexScan
        _ = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();

        var e = Assert.Single(sink.Queries);
        Assert.Equal(QueryStrategy.IndexScan, e.Strategy);
        Assert.Equal("idx_Category", e.IndexName);
    }

    // ── 3. Metriche ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Metrics_Count_Sequential_Operations()
    {
        using var db = NewDb(sink: null);

        for (int i = 0; i < 5; i++)
            await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = i, Name = $"N{i}" });
        await db.SaveChangesAsync();

        _ = db.TestDocuments.AsQueryable().Where(x => x.Amount >= 0).ToList();

        var m = db.AuditMetrics!;
        Assert.Equal(5, m.TotalInserts);
        Assert.True(m.TotalCommits >= 1);
        Assert.Equal(1, m.TotalQueries);
        Assert.True(m.AvgInsertMs >= 0);
    }

    [Fact]
    public async Task Metrics_Are_Accurate_Under_Concurrency()
    {
        using var db = NewDb(sink: null);

        // 4 task paralleli x 25 insert = 100. Verifica che Interlocked non perda incrementi.
        const int tasks = 4, perTask = 25;
        await Task.WhenAll(Enumerable.Range(0, tasks).Select(async t =>
        {
            for (int i = 0; i < perTask; i++)
                await db.TestDocuments.InsertAsync(
                    new TestDocument { Category = "C", Amount = i, Name = $"T{t}-{i}" });
        }));
        await db.SaveChangesAsync();

        Assert.Equal(tasks * perTask, db.AuditMetrics!.TotalInserts);
    }

    [Fact]
    public async Task Metrics_Reset_Clears_Counters()
    {
        using var db = NewDb(sink: null);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();
        Assert.True(db.AuditMetrics!.TotalInserts > 0);

        db.AuditMetrics.Reset();

        Assert.Equal(0, db.AuditMetrics.TotalInserts);
        Assert.Equal(0, db.AuditMetrics.TotalCommits);
        Assert.Equal(0, db.AuditMetrics.AvgInsertMs);
    }

    // ── 4. Configurazione ────────────────────────────────────────────────────

    [Fact]
    public async Task Metrics_Disabled_Leaves_AuditMetrics_Null()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink, metrics: false);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();

        Assert.Null(db.AuditMetrics);      // metriche spente: nessuna allocazione
        Assert.NotEmpty(sink.Inserts);     // ma il sink riceve comunque
    }

    [Fact]
    public async Task Sink_Null_Does_Not_Throw()
    {
        using var db = NewDb(sink: null);   // solo metriche, nessun sink

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();
        _ = db.TestDocuments.AsQueryable().ToList();

        Assert.True(db.AuditMetrics!.TotalInserts > 0);
    }
}
