using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    ReportId = table.Column<long>(type: "bigint", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DividendsAdded = table.Column<int>(type: "int", nullable: false),
                    DividendsMatched = table.Column<int>(type: "int", nullable: false),
                    TransactionsAdded = table.Column<int>(type: "int", nullable: false),
                    TransactionsMatched = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_AccountReports_AccountId_ReportId",
                table: "AccountReports",
                columns: new[] { "AccountId", "ReportId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountReports");
        }
    }
}
