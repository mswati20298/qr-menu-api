using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundsAndUniqueNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before the unique indexes: rename names that already repeat (same restaurant / category, ignoring case),
            // so the indexes can be created on existing data. The first one keeps its name; the others get " (2)", " (3)"...
            migrationBuilder.Sql(@"
WITH d AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY RestaurantId, Name ORDER BY SortOrder, Id) AS n FROM Categories)
UPDATE c SET Name = LEFT(c.Name, 140) + ' (' + CAST(d.n AS nvarchar(5)) + ')' FROM Categories c JOIN d ON d.Id = c.Id WHERE d.n > 1;

WITH d AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY CategoryId, Name ORDER BY SortOrder, Id) AS n FROM MenuItems)
UPDATE i SET Name = LEFT(i.Name, 190) + ' (' + CAST(d.n AS nvarchar(5)) + ')' FROM MenuItems i JOIN d ON d.Id = i.Id WHERE d.n > 1;

WITH d AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY RestaurantId, Number ORDER BY Id) AS n FROM Tables)
UPDATE t SET Number = LEFT(t.Number, 14) + ' (' + CAST(d.n AS nvarchar(3)) + ')' FROM Tables t JOIN d ON d.Id = t.Id WHERE d.n > 1;
");

            migrationBuilder.AddColumn<int>(
                name: "PasswordVersion",
                table: "SuperAdmins",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlanRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GatewayRefundId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanRefunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanRefunds_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tables_RestaurantId_Number",
                table: "Tables",
                columns: new[] { "RestaurantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CategoryId_Name",
                table: "MenuItems",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_RestaurantId_Name",
                table: "Categories",
                columns: new[] { "RestaurantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanRefunds_GatewayRefundId",
                table: "PlanRefunds",
                column: "GatewayRefundId",
                unique: true,
                filter: "[GatewayRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlanRefunds_PaymentEventId",
                table: "PlanRefunds",
                column: "PaymentEventId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanRefunds_PlanPaymentId",
                table: "PlanRefunds",
                column: "PlanPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanRefunds_RestaurantId",
                table: "PlanRefunds",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanRefunds_Status_RequestedAt",
                table: "PlanRefunds",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanRefunds");

            migrationBuilder.DropIndex(
                name: "IX_Tables_RestaurantId_Number",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_CategoryId_Name",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_Categories_RestaurantId_Name",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "PasswordVersion",
                table: "SuperAdmins");
        }
    }
}
