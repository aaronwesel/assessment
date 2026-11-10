using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crawl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
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
