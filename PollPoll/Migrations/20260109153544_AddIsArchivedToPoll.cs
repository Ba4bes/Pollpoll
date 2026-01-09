using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PollPoll.Migrations
{
    /// <inheritdoc />
    public partial class AddIsArchivedToPoll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Polls",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Polls");
        }
    }
}
