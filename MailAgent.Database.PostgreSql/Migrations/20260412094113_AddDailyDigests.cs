using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MailAgent.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyDigests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyDigests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Folder = table.Column<string>(type: "text", nullable: false),
                    DigestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalFetched = table.Column<int>(type: "integer", nullable: false),
                    Selected = table.Column<int>(type: "integer", nullable: false),
                    DigestMarkdown = table.Column<string>(type: "text", nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyDigests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyDigests_Folder_DigestDate",
                table: "DailyDigests",
                columns: new[] { "Folder", "DigestDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyDigests");
        }
    }
}
