using Deepfake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Deepfake.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Veritabanındaki tablomuz
    public DbSet<AnalysisResult> AnalysisResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AnalysisResults Tablosu Yapılandırması
        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            // Tablo adı ve Primary Key
            entity.ToTable("AnalysisResults");
            entity.HasKey(e => e.Id);

            // Rapordaki performans indeksleri
            entity.HasIndex(e => e.CreatedAt).IsDescending();
            entity.HasIndex(e => e.Status);

            // Alan kısıtlamaları ve Veri Tipleri
            entity.Property(e => e.OriginalImagePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.GradcamImagePath).HasMaxLength(500);
            entity.Property(e => e.ElaImagePath).HasMaxLength(500);
            entity.Property(e => e.FftImagePath).HasMaxLength(500);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(500);
            
            // Enum değerini veritabanına metin (Processing, Completed vb.) olarak kaydetmek için
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            
            // Hassasiyet gerektiren ondalıklı alanlar
            entity.Property(e => e.CnnConfidence).HasPrecision(5, 4);
            entity.Property(e => e.ElaScore).HasPrecision(5, 4);
            entity.Property(e => e.FftAnomalyScore).HasPrecision(5, 4);
            entity.Property(e => e.ProcessingTimeSeconds).HasPrecision(10, 2);
        });
    }
}