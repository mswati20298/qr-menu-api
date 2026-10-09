using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewayLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentGatewayLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GatewayOrderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentGatewayLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentGatewayLogs_CreatedAt",
                table: "PaymentGatewayLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentGatewayLogs_GatewayOrderId_CreatedAt",
                table: "PaymentGatewayLogs",
                columns: new[] { "GatewayOrderId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentGatewayLogs");
        }
    }
}
