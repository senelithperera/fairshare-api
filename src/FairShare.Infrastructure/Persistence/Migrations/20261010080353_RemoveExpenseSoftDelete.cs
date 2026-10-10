using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExpenseSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Purge soft-deleted expenses first, otherwise they'd reappear once the column is
            // gone. Their expense_payers / expense_splits rows go with them via ON DELETE CASCADE.
            migrationBuilder.Sql("""DELETE FROM expenses WHERE "IsDeleted" = true;""");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "expenses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "expenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
