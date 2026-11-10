using Crawl.Domain.Jobs;
using Crawl.Domain.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crawl.Infrastructure.Persistence.Configurations;

public sealed class CrawlPageConfiguration : IEntityTypeConfiguration<CrawlPage>
{
    public void Configure(EntityTypeBuilder<CrawlPage> builder)
    {
        builder.ToTable("crawl_pages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url).HasMaxLength(CrawlJob.UrlMaxLength).IsRequired();
        builder.Property(x => x.Depth).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(CrawlPage.ErrorMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        // Idempotency: same normalized URL within a job must not create duplicate page rows.
        builder.HasIndex(x => new { x.JobId, x.Url }).IsUnique();
        builder.HasIndex(x => new { x.JobId, x.Status });
        builder.HasIndex(x => x.ParentPageId);

        builder.HasMany(x => x.Edges)
            .WithOne()
            .HasForeignKey(x => x.SourcePageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
