using Microsoft.EntityFrameworkCore;

namespace USCISFormTracker.Data;

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
            entity.HasIndex(e => e.FileName).IsUnique();
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.FullLink).IsRequired();
            entity.Property(e => e.FormName).IsRequired();
            entity.Property(e => e.Hash).IsRequired();
            entity.Property(e => e.ExtractedText).IsRequired();
            entity.Property(e => e.LastChecked).IsRequired();
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.DeletedAt).IsRequired(false);
            entity.HasIndex(e => e.IsActive); // For filtering active/deleted forms
        });

        modelBuilder.Entity<PdfFormChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.FullLink).IsRequired();
            entity.Property(e => e.FormName).IsRequired();
            entity.Property(e => e.OldHash).IsRequired();
            entity.Property(e => e.NewHash).IsRequired();
            entity.Property(e => e.DiffLinesSerialized).IsRequired();
            entity.Property(e => e.DetectedChangeTime).IsRequired();
        });
    }
}
