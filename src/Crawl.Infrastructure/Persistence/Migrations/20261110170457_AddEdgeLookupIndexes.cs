using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crawl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEdgeLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_crawl_edges_SourcePageId",
                table: "crawl_edges",
                column: "SourcePageId");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_edges_TargetUrl",
                table: "crawl_edges",
                column: "TargetUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_crawl_edges_SourcePageId",
                table: "crawl_edges");

            migrationBuilder.DropIndex(
                name: "IX_crawl_edges_TargetUrl",
                table: "crawl_edges");
        }
    }
}
