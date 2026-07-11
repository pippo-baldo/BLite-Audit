using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BLite.Core.Audit;
using BLite.Shared;
using Xunit;

namespace BLite.Tests;

public class ConsoleAuditSinkTests
{
    [Fact]
    public async Task ConsoleSink_Writes_All_Event_Types()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blite_console_{Guid.NewGuid():N}.db");
        var original = Console.Out;
        var buffer = new StringWriter();
        Console.SetOut(buffer);

        try
        {
            using (var db = new TestDbContext(path, new BLiteAuditOptions
            {
                Sink = new ConsoleAuditSink(useColor: false),
                SlowQueryThreshold = TimeSpan.Zero      // forza anche gli eventi SLOW
            }))
            {
                await db.TestDocuments.InsertAsync(new TestDocument { Category = "A", Amount = 1, Name = "X" });
                await db.SaveChangesAsync();
                _ = db.TestDocuments.AsQueryable().Where(x => x.Category == "A").ToList();
            }
        }
        finally
        {
            Console.SetOut(original);
            if (File.Exists(path)) File.Delete(path);
        }

        var output = buffer.ToString();
        Assert.Contains("INSERT", output);
        Assert.Contains("QUERY", output);
        Assert.Contains("COMMIT", output);
        Assert.Contains("SLOW", output);
        Assert.Contains("strategy=IndexScan", output);
    }
}
