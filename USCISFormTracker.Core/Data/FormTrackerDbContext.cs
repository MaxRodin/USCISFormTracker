using Microsoft.EntityFrameworkCore;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core.Data;

public class FormTrackerDbContext : DbContext
{
    public DbSet<PdfFormRecord> FormRecords { get; set; }
    public DbSet<PdfFormChange> FormChanges { get; set; }

    public FormTrackerDbContext(DbContextOptions<FormTrackerDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PdfFormRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Link).IsUnique();
            entity.Property(e => e.FormName).IsRequired();
            entity.Property(e => e.Hash).IsRequired();
            entity.Property(e => e.LastChecked).IsRequired();
        });

        modelBuilder.Entity<PdfFormChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Link).IsRequired();
            entity.Property(e => e.FormName).IsRequired();
            entity.Property(e => e.OldHash).IsRequired();
            entity.Property(e => e.NewHash).IsRequired();
            entity.Property(e => e.DiffLinesSerialized).IsRequired();
            entity.Property(e => e.DetectedChangeTime).IsRequired();
        });
    }
}
