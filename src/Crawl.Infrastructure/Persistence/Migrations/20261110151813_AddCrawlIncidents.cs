using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crawl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrawlIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crawl_incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FaultCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PageId = table.Column<long>(type: "bigint", nullable: true),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    RemediationKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RemediationStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RemediationDetail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UsedModel = table.Column<bool>(type: "boolean", nullable: false),
                    AiAdvice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AlertSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crawl_incidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crawl_incidents_JobId",
                table: "crawl_incidents",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_incidents_JobId_CreatedAt",
                table: "crawl_incidents",
                columns: new[] { "JobId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crawl_incidents");
        }
    }
}
