using Crawl.Domain.Jobs;
using Crawl.Domain.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crawl.Infrastructure.Persistence.Configurations;

public sealed class PageEdgeConfiguration : IEntityTypeConfiguration<PageEdge>
{
    public void Configure(EntityTypeBuilder<PageEdge> builder)
    {
        builder.ToTable("crawl_edges");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TargetUrl).HasMaxLength(CrawlJob.UrlMaxLength).IsRequired();
        builder.Property(x => x.IsInternal).IsRequired();

        // Idempotency: same outgoing target from a source page is unique.
        builder.HasIndex(x => new { x.SourcePageId, x.TargetUrl }).IsUnique();
    }
}
