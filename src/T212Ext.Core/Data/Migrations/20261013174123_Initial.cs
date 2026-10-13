using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Ticker = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ShortName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AddedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Ticker);
                });

            migrationBuilder.CreateTable(
                name: "CashTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sources = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashTransactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dividends",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sources = table.Column<int>(type: "int", nullable: false),
                    Ticker = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InstrumentName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaidOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    GrossAmountPerShare = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    InstrumentCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AmountInEuro = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    WithholdingTax = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    WithholdingTaxCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dividends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dividends_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    FillId = table.Column<long>(type: "bigint", nullable: true),
                    Ticker = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InstrumentName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    InstrumentCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Side = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Strategy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InitiatedFrom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TimeInForce = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Value = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    FilledQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    FilledValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    LimitPrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    StopPrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    FilledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FillPrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    FillQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    FillType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TradingMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WalletCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FxRate = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    NetValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    RealisedProfitLoss = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TakenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    CashAvailable = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    CashInPies = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    CashReserved = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    InvestmentsValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    UnrealizedProfitLoss = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    RealizedProfitLoss = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Snapshots_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    Job = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ItemsAdded = table.Column<int>(type: "int", nullable: false),
                    ItemsUpdated = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncRuns_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Job = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HistoryComplete = table.Column<bool>(type: "bit", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => new { x.AccountId, x.Job });
                    table.ForeignKey(
                        name: "FK_SyncStates_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderTaxes",
                columns: table => new
                {
                    HistoricalOrderId = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ChargedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTaxes", x => new { x.HistoricalOrderId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrderTaxes_Orders_HistoricalOrderId",
                        column: x => x.HistoricalOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PositionSnapshots",
                columns: table => new
                {
                    PortfolioSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ticker = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    InstrumentCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    QuantityInPies = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    AveragePricePaid = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    WalletCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    UnrealizedProfitLoss = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    FxImpact = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionSnapshots", x => new { x.PortfolioSnapshotId, x.Id });
                    table.ForeignKey(
                        name: "FK_PositionSnapshots_Snapshots_PortfolioSnapshotId",
                        column: x => x.PortfolioSnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Key",
                table: "Accounts",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_AccountId_DateTime",
                table: "CashTransactions",
                columns: new[] { "AccountId", "DateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_AccountId_Reference",
                table: "CashTransactions",
                columns: new[] { "AccountId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dividends_AccountId_Isin",
                table: "Dividends",
                columns: new[] { "AccountId", "Isin" });

            migrationBuilder.CreateIndex(
                name: "IX_Dividends_AccountId_PaidOn",
                table: "Dividends",
                columns: new[] { "AccountId", "PaidOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Dividends_AccountId_Reference",
                table: "Dividends",
                columns: new[] { "AccountId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Isin",
                table: "Instruments",
                column: "Isin");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AccountId_OrderId_FillId",
                table: "Orders",
                columns: new[] { "AccountId", "OrderId", "FillId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AccountId_Ticker",
                table: "Orders",
                columns: new[] { "AccountId", "Ticker" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_AccountId_Date",
                table: "Snapshots",
                columns: new[] { "AccountId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_AccountId",
                table: "SyncRuns",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_StartedAt",
                table: "SyncRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashTransactions");

            migrationBuilder.DropTable(
                name: "Dividends");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "OrderTaxes");

            migrationBuilder.DropTable(
                name: "PositionSnapshots");

            migrationBuilder.DropTable(
                name: "SyncRuns");

            migrationBuilder.DropTable(
                name: "SyncStates");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Snapshots");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
