using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Crawl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crawl_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    StartHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MaxDepth = table.Column<int>(type: "integer", nullable: false),
                    MaxPages = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crawl_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crawl_pages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentPageId = table.Column<long>(type: "bigint", nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LinkCount = table.Column<int>(type: "integer", nullable: true),
                    InternalLinkCount = table.Column<int>(type: "integer", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crawl_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crawl_pages_crawl_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "crawl_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crawl_edges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourcePageId = table.Column<long>(type: "bigint", nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crawl_edges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crawl_edges_crawl_pages_SourcePageId",
                        column: x => x.SourcePageId,
                        principalTable: "crawl_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crawl_edges_SourcePageId_TargetUrl",
                table: "crawl_edges",
                columns: new[] { "SourcePageId", "TargetUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_CreatedAt",
                table: "crawl_jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_Status",
                table: "crawl_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_pages_JobId_Status",
                table: "crawl_pages",
                columns: new[] { "JobId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_crawl_pages_JobId_Url",
                table: "crawl_pages",
                columns: new[] { "JobId", "Url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crawl_pages_ParentPageId",
                table: "crawl_pages",
                column: "ParentPageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crawl_edges");

            migrationBuilder.DropTable(
                name: "crawl_pages");

            migrationBuilder.DropTable(
                name: "crawl_jobs");
        }
    }
}
