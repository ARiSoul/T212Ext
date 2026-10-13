using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeRatesInstrumentSettingsAndNetPerShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetAmountPerShare",
                table: "Dividends",
                type: "decimal(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "CashTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PerEuro = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => new { x.Currency, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "InstrumentSettings",
                columns: table => new
                {
                    Isin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceCountry = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentSettings", x => x.Isin);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "InstrumentSettings");

            migrationBuilder.DropColumn(
                name: "NetAmountPerShare",
                table: "Dividends");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "CashTransactions");
        }
    }
}
