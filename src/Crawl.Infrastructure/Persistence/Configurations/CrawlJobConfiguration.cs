using Crawl.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crawl.Infrastructure.Persistence.Configurations;

public sealed class CrawlJobConfiguration : IEntityTypeConfiguration<CrawlJob>
{
    public void Configure(EntityTypeBuilder<CrawlJob> builder)
    {
        builder.ToTable("crawl_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url).HasMaxLength(CrawlJob.UrlMaxLength).IsRequired();
        builder.Property(x => x.StartHost).HasMaxLength(255).IsRequired();
        builder.Property(x => x.MaxDepth).IsRequired();
        builder.Property(x => x.MaxPages).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(CrawlJob.FailureReasonMaxLength);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.Pages)
            .WithOne()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
