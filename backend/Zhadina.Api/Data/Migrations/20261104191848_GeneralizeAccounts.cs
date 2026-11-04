using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zhadina.Api.Data.Migrations
{
    // Hand-written: EF scaffolds this rename as DropTable + CreateTable, which would delete every account.
    /// <inheritdoc />
    public partial class GeneralizeAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_AspNetUsers_UserId",
                table: "BankAccounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BankAccounts",
                table: "BankAccounts");

            migrationBuilder.RenameTable(
                name: "BankAccounts",
                newName: "Accounts");

            migrationBuilder.RenameIndex(
                name: "IX_BankAccounts_UserId",
                table: "Accounts",
                newName: "IX_Accounts_UserId");

            // Everything stored so far was a bank account. The default only backfills those rows.
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Bank");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Bank");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Accounts",
                table: "Accounts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AspNetUsers_UserId",
                table: "Accounts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AspNetUsers_UserId",
                table: "Accounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Accounts",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Accounts");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_UserId",
                table: "Accounts",
                newName: "IX_BankAccounts_UserId");

            migrationBuilder.RenameTable(
                name: "Accounts",
                newName: "BankAccounts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BankAccounts",
                table: "BankAccounts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_AspNetUsers_UserId",
                table: "BankAccounts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
