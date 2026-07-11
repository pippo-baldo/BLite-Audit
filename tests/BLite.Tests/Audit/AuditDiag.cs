using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using BLite.Core.Audit;
using BLite.Shared;
using Xunit;
using Xunit.Abstractions;

namespace BLite.Tests;

public class AuditDiag
{
    private readonly ITestOutputHelper _out;
    public AuditDiag(ITestOutputHelper output) => _out = output;

    private sealed class DiagSink : IBLiteAuditSink
    {
        public readonly List<InsertAuditEvent> Inserts = new();
        public readonly List<QueryAuditEvent> Queries = new();
        public readonly List<CommitAuditEvent> Commits = new();
        public void OnInsert(InsertAuditEvent e) { lock (Inserts) Inserts.Add(e); }
        public void OnQuery(QueryAuditEvent e) { lock (Queries) Queries.Add(e); }
        public void OnCommit(CommitAuditEvent e) { lock (Commits) Commits.Add(e); }
    }

    [Fact]
    public async Task Dump_NoIndex()
    {
        var sink = new DiagSink();
        var path = Path.Combine(Path.GetTempPath(), $"diagA_{Guid.NewGuid():N}.db");
        using (var db = new TestDbContext(path, new BLiteAuditOptions { Sink = sink, EnableMetrics = true }))
        {
            await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
            await db.TestDocuments.InsertAsync(new TestDocument { Category = "B", Amount = 2, Name = "Y" });
            await db.SaveChangesAsync();
            var res = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();
            _out.WriteLine($"[SENZA INDICE] risultati={res.Count} inserts={sink.Inserts.Count} commits={sink.Commits.Count} queries={sink.Queries.Count}");
            foreach (var e in sink.Inserts)
                _out.WriteLine($"[SENZA INDICE] insert coll='{e.CollectionName}' size={e.DocumentSizeBytes} tx={e.TransactionId}");
            foreach (var e in sink.Queries)
                _out.WriteLine($"[SENZA INDICE] query coll='{e.CollectionName}' strategy={e.Strategy} index='{e.IndexName ?? "null"}' count={e.ResultCount}");
        }
        Assert.True(false, "diagnostico");
    }

    [Fact]
    public async Task Dump_WithIndex()
    {
        var sink = new DiagSink();
        var path = Path.Combine(Path.GetTempPath(), $"diagB_{Guid.NewGuid():N}.db");
        using (var db = new TestDbContext(path, new BLiteAuditOptions { Sink = sink, EnableMetrics = true }))
        {
            await db.TestDocuments.CreateIndexAsync(x => x.Category, "idx_cat");
            await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
            await db.TestDocuments.InsertAsync(new TestDocument { Category = "B", Amount = 2, Name = "Y" });
            await db.SaveChangesAsync();
            var res = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();
            _out.WriteLine($"[CON INDICE] risultati={res.Count} queries={sink.Queries.Count}");
            foreach (var e in sink.Queries)
                _out.WriteLine($"[CON INDICE] query coll='{e.CollectionName}' strategy={e.Strategy} index='{e.IndexName ?? "null"}' count={e.ResultCount}");
        }
        Assert.True(false, "diagnostico");
    }
}
