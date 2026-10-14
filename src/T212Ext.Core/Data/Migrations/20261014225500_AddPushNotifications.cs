using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LastDividendId = table.Column<long>(type: "bigint", nullable: true),
                    LastSummaryMonth = table.Column<DateOnly>(type: "date", nullable: true)
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
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    EndpointHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Culture = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushDevices_EndpointHash",
                table: "PushDevices",
                column: "EndpointHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationStates");

            migrationBuilder.DropTable(
                name: "PushDevices");
        }
    }
}
