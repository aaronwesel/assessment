using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crawl.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CrawlDbContext>
{
    public CrawlDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CrawlDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=crawl;Username=app;Password=changeme-local-only")
            .Options;
        return new CrawlDbContext(options);
    }
}
