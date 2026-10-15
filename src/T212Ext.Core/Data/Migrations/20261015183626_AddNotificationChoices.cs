using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Devices that had notifications on keep getting everything (and the new weekly notice).
            migrationBuilder.AddColumn<bool>(
                name: "NotifyDividends",
                table: "PushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifySummary",
                table: "PushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyWeek",
                table: "PushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietFrom",
                table: "PushDevices",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietTo",
                table: "PushDevices",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "PushDevices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastWeekNotice",
                table: "NotificationStates",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyDividends",
                table: "PushDevices");

            migrationBuilder.DropColumn(
                name: "NotifySummary",
                table: "PushDevices");

            migrationBuilder.DropColumn(
                name: "NotifyWeek",
                table: "PushDevices");

            migrationBuilder.DropColumn(
                name: "QuietFrom",
                table: "PushDevices");

            migrationBuilder.DropColumn(
                name: "QuietTo",
                table: "PushDevices");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "PushDevices");

            migrationBuilder.DropColumn(
                name: "LastWeekNotice",
                table: "NotificationStates");
        }
    }
}
