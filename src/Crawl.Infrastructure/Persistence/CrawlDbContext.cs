using Crawl.Domain.Jobs;
using Crawl.Domain.Pages;
using Microsoft.EntityFrameworkCore;

namespace Crawl.Infrastructure.Persistence;

public sealed class CrawlDbContext(DbContextOptions<CrawlDbContext> options) : DbContext(options)
{
    public DbSet<CrawlJob> Jobs => Set<CrawlJob>();
    public DbSet<CrawlPage> Pages => Set<CrawlPage>();
    public DbSet<PageEdge> Edges => Set<PageEdge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrawlDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
