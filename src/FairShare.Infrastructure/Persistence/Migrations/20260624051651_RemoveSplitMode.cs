using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSplitMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SplitMode",
                table: "expenses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SplitMode",
                table: "expenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
