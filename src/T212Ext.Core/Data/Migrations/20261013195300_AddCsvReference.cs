using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCsvReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CsvReference",
                table: "Dividends",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CsvReference",
                table: "CashTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dividends_AccountId_CsvReference",
                table: "Dividends",
                columns: new[] { "AccountId", "CsvReference" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_AccountId_CsvReference",
                table: "CashTransactions",
                columns: new[] { "AccountId", "CsvReference" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dividends_AccountId_CsvReference",
                table: "Dividends");

            migrationBuilder.DropIndex(
                name: "IX_CashTransactions_AccountId_CsvReference",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "CsvReference",
                table: "Dividends");

            migrationBuilder.DropColumn(
                name: "CsvReference",
                table: "CashTransactions");
        }
    }
}
