using Crawl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crawl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// Designer was missing so EF never discovered this migration on startup;
    /// attribute on the Up class restores MigrateAsync application.
    [DbContext(typeof(CrawlDbContext))]
    [Migration("20261110190000_AddJobAlertEmail")]
    public partial class AddJobAlertEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlertEmail",
                table: "crawl_jobs",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StatusAlertSent",
                table: "crawl_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertEmail",
                table: "crawl_jobs");

            migrationBuilder.DropColumn(
                name: "StatusAlertSent",
                table: "crawl_jobs");
        }
    }
}
