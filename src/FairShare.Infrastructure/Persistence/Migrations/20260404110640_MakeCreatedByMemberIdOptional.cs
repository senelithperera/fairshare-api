using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairShare.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeCreatedByMemberIdOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_expenses_group_members_CreatedByMemberId",
                table: "expenses");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMemberId",
                table: "expenses",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_expenses_group_members_CreatedByMemberId",
                table: "expenses",
                column: "CreatedByMemberId",
                principalTable: "group_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_expenses_group_members_CreatedByMemberId",
                table: "expenses");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByMemberId",
                table: "expenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_expenses_group_members_CreatedByMemberId",
                table: "expenses",
                column: "CreatedByMemberId",
                principalTable: "group_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
