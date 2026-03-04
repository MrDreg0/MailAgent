using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailAgent.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnsForBody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Body",
                table: "Mails",
                newName: "RawBody");

            migrationBuilder.AddColumn<string>(
                name: "MarkdownBody",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkdownBody",
                table: "Mails");

            migrationBuilder.RenameColumn(
                name: "RawBody",
                table: "Mails",
                newName: "Body");
        }
    }
}
