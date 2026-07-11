using BLite.AuditDashboard;
using BLite.Core.Audit;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ── Setup del database con audit attivo ──────────────────────────────────────
var sink = new DashboardSink();
var metrics = new BLiteMetrics();
var dbPath = Path.Combine(Path.GetTempPath(), $"dashboard_{Guid.NewGuid():N}.db");

var db = new ShopDbContext(dbPath, new BLiteAuditOptions
{
    Sink = sink,
    EnableMetrics = true,
    SlowQueryThreshold = TimeSpan.FromMilliseconds(5)   // soglia bassa: qualcosa scattera'
});

app.UseDefaultFiles();
app.UseStaticFiles();

// ── API usate dalla pagina ───────────────────────────────────────────────────

// Stato corrente: eventi recenti + contatori
app.MapGet("/api/state", () =>
{
    var m = db.AuditMetrics;
    return Results.Json(new
    {
        events = sink.Recent,
        metrics = new
        {
            inserts    = m?.TotalInserts ?? 0,
            commits    = m?.TotalCommits ?? 0,
            queries    = m?.TotalQueries ?? 0,
            indexScan  = m?.TotalQueriesIndexScan ?? 0,
            bsonScan   = m?.TotalQueriesBsonScan ?? 0,
            fullScan   = m?.TotalQueriesFullScan ?? 0,
            avgInsert  = Math.Round(m?.AvgInsertMs ?? 0, 3),
            avgQuery   = Math.Round(m?.AvgQueryMs ?? 0, 3),
        }
    });
});

// Inserisce N documenti
app.MapPost("/api/insert", async () =>
{
    var rnd = Random.Shared;
    string[] cats = { "libri", "musica", "giochi" };
    for (int i = 0; i < 10; i++)
        await db.Items.InsertAsync(new Item
        {
            Category = cats[rnd.Next(cats.Length)],
            Name = $"Articolo-{rnd.Next(1000)}",
            Price = rnd.Next(5, 200)
        });
    await db.SaveChangesAsync();
    return Results.Ok();
});

// Query su campo INDICIZZATO -> IndexScan
app.MapPost("/api/query-indexed", () =>
{
    var n = db.Items.AsQueryable().Where(x => x.Category == "libri").ToList().Count;
    return Results.Ok(new { results = n });
});

// Query su campo NON indicizzato -> scansione
app.MapPost("/api/query-scan", () =>
{
    var n = db.Items.AsQueryable().Where(x => x.Price > 100).ToList().Count;
    return Results.Ok(new { results = n });
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    db.Dispose();
    if (File.Exists(dbPath)) File.Delete(dbPath);
});

app.Run("http://localhost:5080");
