using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arisoul.T212Ext.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSignInAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignInAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignInAttempts_At",
                table: "SignInAttempts",
                column: "At");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignInAttempts");
        }
    }
}
