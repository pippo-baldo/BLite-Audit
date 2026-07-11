using BLite.Bson;
using BLite.Core;
using BLite.Core.Collections;
using BLite.Core.Metadata;

namespace BLite.AuditDashboard;

/// <summary>Documento di esempio per la demo.</summary>
public class Item
{
    public ObjectId Id { get; set; }
    public string Category { get; set; } = "";   // INDICIZZATO (vedi OnModelCreating)
    public string Name { get; set; } = "";       // NON indicizzato
    public int Price { get; set; }
}

public partial class ShopDbContext : DocumentDbContext
{
    public DocumentCollection<ObjectId, Item> Items { get; set; } = null!;

    public ShopDbContext(string path, BLite.Core.Audit.BLiteAuditOptions audit)
        : base(path, audit) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Indice su Category: le query su questo campo useranno IndexScan.
        // Il campo Name resta senza indice -> le query su Name faranno una scansione.
        modelBuilder.Entity<Item>().HasIndex(x => x.Category);
    }
}
