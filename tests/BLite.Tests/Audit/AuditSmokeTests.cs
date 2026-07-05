using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using BLite.Core.Audit;
using BLite.Shared;
using Xunit;

namespace BLite.Tests;

public class AuditSmokeTests
{
    private sealed class CapturingSink : IBLiteAuditSink
    {
        public readonly List<InsertAuditEvent> Inserts = new();
        public readonly List<QueryAuditEvent> Queries = new();
        public readonly List<CommitAuditEvent> Commits = new();
        public void OnInsert(InsertAuditEvent e) => Inserts.Add(e);
        public void OnQuery(QueryAuditEvent e)   => Queries.Add(e);
        public void OnCommit(CommitAuditEvent e) => Commits.Add(e);
    }

    [Fact]
    public async Task Hooks_Fire_For_Insert_Query_Commit()
    {
        var sink = new CapturingSink();
        var opts = new BLiteAuditOptions { Sink = sink, EnableMetrics = true };
        var path = Path.Combine(Path.GetTempPath(), $"blite_audit_smoke_{Guid.NewGuid()}.db");

        try
        {
            using var db = new TestDbContext(path, opts);

            await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 10, Name = "Item1" });
            await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 20, Name = "Item2" });
            await db.TestDocuments.InsertAsync(new TestDocument { Category = "B", Amount = 30, Name = "Item3" });
            await db.SaveChangesAsync();

            var itemsA = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();
            Assert.Equal(2, itemsA.Count);

            // Gli hook hanno emesso gli eventi?
            Assert.True(sink.Inserts.Count >= 3, $"insert events = {sink.Inserts.Count}");
            Assert.True(sink.Commits.Count >= 1, $"commit events = {sink.Commits.Count}");
            Assert.True(sink.Queries.Count >= 1, $"query events = {sink.Queries.Count}");

            // Le metriche si sono incrementate?
            Assert.NotNull(db.AuditMetrics);
            Assert.True(db.AuditMetrics!.TotalInserts >= 3);
            Assert.True(db.AuditMetrics.TotalCommits >= 1);
            Assert.True(db.AuditMetrics.TotalQueries >= 1);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
