using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zhadina.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written: a credit card's balance becomes the amount owed. Until now either sign meant "owed"
            // (ADR-009), but transactions add to and subtract from the stored number, so the sign has to mean
            // something: a $40 purchase on a card stored as -500 would otherwise lower the debt.
            // Down leaves this as is; the old sign carried no meaning.
            migrationBuilder.Sql("""
                UPDATE "Accounts" SET "Balance" = -"Balance" WHERE "Type" = 'CreditCard' AND "Balance" < 0;
                """);

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transactions_AmountPositive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_Transactions_NoCategoryOnTransfers", "\"Kind\" <> 'Transfer' OR \"Category\" IS NULL");
                    table.CheckConstraint("CK_Transactions_ToAccountOnlyOnTransfers", "(\"Kind\" = 'Transfer') = (\"ToAccountId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Transactions_TransferBetweenTwoAccounts", "\"ToAccountId\" <> \"AccountId\"");
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_ToAccountId",
                        column: x => x.ToAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ToAccountId",
                table: "Transactions",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_Date_CreatedAt",
                table: "Transactions",
                columns: new[] { "UserId", "Date", "CreatedAt" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
