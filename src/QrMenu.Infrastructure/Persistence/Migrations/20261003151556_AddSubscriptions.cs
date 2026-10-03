using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Restaurants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                // Existing restaurants keep working: free plan with no end date.
                defaultValue: "Free");

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanCancelledAt",
                table: "Restaurants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanExpiresAt",
                table: "Restaurants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionEvents_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_PlanExpiresAt",
                table: "Restaurants",
                column: "PlanExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionEvents_RestaurantId_CreatedAt",
                table: "SubscriptionEvents",
                columns: new[] { "RestaurantId", "CreatedAt" });

            // Start every existing restaurant's history with the free plan it was just given.
            migrationBuilder.Sql(@"
INSERT INTO SubscriptionEvents (Id, RestaurantId, Action, [Plan], ExpiresAt, PerformedBy, Note, CreatedAt)
SELECT NEWID(), Id, 'FreeGranted', 'Free', NULL, 'system',
       'Existing restaurant moved to the free plan when subscriptions were introduced.', SYSUTCDATETIME()
FROM Restaurants;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionEvents");

            migrationBuilder.DropIndex(
                name: "IX_Restaurants_PlanExpiresAt",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PlanCancelledAt",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "Restaurants");
        }
    }
}
