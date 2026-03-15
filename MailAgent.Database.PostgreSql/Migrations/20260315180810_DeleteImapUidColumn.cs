using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailAgent.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DeleteImapUidColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapUid",
                table: "Mails");

            migrationBuilder.CreateIndex(
                name: "IX_Mails_MessageId",
                table: "Mails",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mails_MessageId",
                table: "Mails");

            migrationBuilder.AddColumn<int>(
                name: "ImapUid",
                table: "Mails",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
