using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BLite.Core.Audit;
using BLite.Shared;
using Xunit;

namespace BLite.Tests;

public class AuditSlowQueryTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"blite_slow_{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private TestDbContext NewDb(CollectingSink sink, TimeSpan? threshold) =>
        new(_path, new BLiteAuditOptions
        {
            Sink = sink,
            EnableMetrics = true,
            SlowQueryThreshold = threshold
        });

    /// <summary>
    /// Soglia a zero: QUALSIASI operazione la supera. Serve a provare che il
    /// rilevamento scatta, senza dipendere da rallentamenti artificiali (niente
    /// Sleep, quindi niente test "flaky").
    /// </summary>
    private static readonly TimeSpan AlwaysSlow = TimeSpan.Zero;

    /// <summary>Soglia altissima: nessuna operazione la supera mai.</summary>
    private static readonly TimeSpan NeverSlow = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task SlowOperation_Emitted_When_Threshold_Exceeded()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink, AlwaysSlow);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();
        _ = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();

        Assert.NotEmpty(sink.Slow);
    }

    [Fact]
    public async Task SlowOperation_Not_Emitted_When_Below_Threshold()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink, NeverSlow);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();
        _ = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();

        // Gli eventi normali arrivano...
        Assert.NotEmpty(sink.Inserts);
        Assert.NotEmpty(sink.Queries);
        // ...ma nessuno e' "slow".
        Assert.Empty(sink.Slow);
    }

    [Fact]
    public async Task SlowOperation_Not_Emitted_When_Threshold_Null()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink, threshold: null);   // rilevamento disabilitato

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();
        _ = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();

        Assert.NotEmpty(sink.Inserts);
        Assert.Empty(sink.Slow);
    }

    [Fact]
    public async Task SlowOperation_Covers_Insert_Query_And_Commit()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink, AlwaysSlow);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();
        _ = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();

        var types = sink.Slow.Select(s => s.OperationType).Distinct().ToList();
        Assert.Contains(SlowOperationType.Insert, types);
        Assert.Contains(SlowOperationType.Query, types);
        Assert.Contains(SlowOperationType.Commit, types);
    }

    [Fact]
    public async Task SlowOperation_Carries_Collection_And_Elapsed()
    {
        var sink = new CollectingSink();
        using var db = NewDb(sink, AlwaysSlow);

        await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
        await db.SaveChangesAsync();

        var insertSlow = sink.Slow.First(s => s.OperationType == SlowOperationType.Insert);
        Assert.Equal("testdocuments", insertSlow.CollectionName);
        Assert.True(insertSlow.Elapsed >= TimeSpan.Zero);
    }
}
