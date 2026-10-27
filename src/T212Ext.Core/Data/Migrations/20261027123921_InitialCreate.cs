using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PerEuro = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => new { x.Currency, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MonthlyTarget = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    MonthlyExpenses = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CoverShare = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IncludeInterest = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentLogos",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentLogos", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Ticker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AddedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Ticker);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentSettings",
                columns: table => new
                {
                    Isin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentSettings", x => x.Isin);
                });

            migrationBuilder.CreateTable(
                name: "NotificationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    LastDividendId = table.Column<long>(type: "bigint", nullable: true),
                    LastSummaryMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    LastWeekNotice = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushDevices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    EndpointHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Culture = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotifyDividends = table.Column<bool>(type: "boolean", nullable: false),
                    NotifySummary = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyWeek = table.Column<bool>(type: "boolean", nullable: false),
                    QuietFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    QuietTo = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignInAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Username = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockSplits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Isin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Ratio = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockSplits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    ReportId = table.Column<long>(type: "bigint", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DividendsAdded = table.Column<int>(type: "integer", nullable: false),
                    DividendsMatched = table.Column<int>(type: "integer", nullable: false),
                    TransactionsAdded = table.Column<int>(type: "integer", nullable: false),
                    TransactionsMatched = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountReports_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sources = table.Column<int>(type: "integer", nullable: false),
                    CsvReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sources = table.Column<int>(type: "integer", nullable: false),
                    CsvReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ticker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InstrumentName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PaidOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    GrossAmountPerShare = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    NetAmountPerShare = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    InstrumentCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AmountInEuro = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    WithholdingTax = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    WithholdingTaxCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    FillId = table.Column<long>(type: "bigint", nullable: true),
                    Ticker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InstrumentName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    InstrumentCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Side = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Strategy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InitiatedFrom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TimeInForce = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FilledQuantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FilledValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    LimitPrice = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StopPrice = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FillPrice = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FillQuantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FillType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TradingMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WalletCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FxRate = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    NetValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    RealisedProfitLoss = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TakenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TotalValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CashAvailable = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CashInPies = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CashReserved = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    InvestmentsValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TotalCost = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    UnrealizedProfitLoss = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    RealizedProfitLoss = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true)
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: true),
                    Job = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ItemsAdded = table.Column<int>(type: "integer", nullable: false),
                    ItemsUpdated = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
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
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Job = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HistoryComplete = table.Column<bool>(type: "boolean", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ChargedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Isin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    InstrumentCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    QuantityInPies = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    AveragePricePaid = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    WalletCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TotalCost = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    UnrealizedProfitLoss = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    FxImpact = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true)
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
                name: "IX_AccountReports_AccountId_ReportId",
                table: "AccountReports",
                columns: new[] { "AccountId", "ReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Key",
                table: "Accounts",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransactions_AccountId_CsvReference",
                table: "CashTransactions",
                columns: new[] { "AccountId", "CsvReference" });

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
                name: "IX_Dividends_AccountId_CsvReference",
                table: "Dividends",
                columns: new[] { "AccountId", "CsvReference" });

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
                name: "IX_PushDevices_EndpointHash",
                table: "PushDevices",
                column: "EndpointHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignInAttempts_At",
                table: "SignInAttempts",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_AccountId_Date",
                table: "Snapshots",
                columns: new[] { "AccountId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockSplits_Isin_Date",
                table: "StockSplits",
                columns: new[] { "Isin", "Date" },
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
                name: "AccountReports");

            migrationBuilder.DropTable(
                name: "CashTransactions");

            migrationBuilder.DropTable(
                name: "Dividends");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "InstrumentLogos");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "InstrumentSettings");

            migrationBuilder.DropTable(
                name: "NotificationStates");

            migrationBuilder.DropTable(
                name: "OrderTaxes");

            migrationBuilder.DropTable(
                name: "PositionSnapshots");

            migrationBuilder.DropTable(
                name: "PushDevices");

            migrationBuilder.DropTable(
                name: "SignInAttempts");

            migrationBuilder.DropTable(
                name: "StockSplits");

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
