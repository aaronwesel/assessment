using Crawl.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crawl.Infrastructure.Persistence.Configurations;

public sealed class CrawlIncidentConfiguration : IEntityTypeConfiguration<CrawlIncident>
{
    public void Configure(EntityTypeBuilder<CrawlIncident> builder)
    {
        builder.ToTable("crawl_incidents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FaultCode).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(CrawlIncident.SummaryMaxLength).IsRequired();
        builder.Property(x => x.Host).HasMaxLength(255);
        builder.Property(x => x.RemediationKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.RemediationStatus).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RemediationDetail).HasMaxLength(CrawlIncident.DetailMaxLength);
        builder.Property(x => x.AiAdvice).HasMaxLength(CrawlIncident.DetailMaxLength);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.JobId);
        builder.HasIndex(x => new { x.JobId, x.CreatedAt });
    }
}
