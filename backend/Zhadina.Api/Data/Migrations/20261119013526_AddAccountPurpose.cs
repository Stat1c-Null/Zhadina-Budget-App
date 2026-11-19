using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zhadina.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF scaffolded an empty-string default, which isn't a purpose. Existing accounts become Spending,
            // the "neither box ticked" value. The column keeps this default; the API always sets it anyway.
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Spending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Accounts");
        }
    }
}
