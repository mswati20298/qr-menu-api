using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingPlansAndOnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "SubscriptionEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "Restaurants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PricingPlanId",
                table: "Restaurants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PricingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GatewayOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanPayments_PricingPlans_PricingPlanId",
                        column: x => x.PricingPlanId,
                        principalTable: "PricingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanPayments_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_PricingPlanId",
                table: "Restaurants",
                column: "PricingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanPayments_GatewayOrderId",
                table: "PlanPayments",
                column: "GatewayOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanPayments_PricingPlanId",
                table: "PlanPayments",
                column: "PricingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanPayments_RestaurantId_CreatedAt",
                table: "PlanPayments",
                columns: new[] { "RestaurantId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Restaurants_PricingPlans_PricingPlanId",
                table: "Restaurants",
                column: "PricingPlanId",
                principalTable: "PricingPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Two starter plans, inactive: owners cannot buy anything until the super admin
            // has checked the price and switched a plan on.
            migrationBuilder.Sql(@"
INSERT INTO PricingPlans (Id, Name, Description, DurationMonths, Price, IsActive, SortOrder)
VALUES ('6f1c2a8e-0b1d-4c55-9a51-3f2b8c7d1a01', 'Monthly', 'All features, renewed every month.', 1, 499, 0, 1),
       ('6f1c2a8e-0b1d-4c55-9a51-3f2b8c7d1a12', 'Yearly', 'All features, renewed once a year.', 12, 4999, 0, 2);");

            // Old plan values Free/Monthly/Yearly become Trial/Free/Paid + a display name.
            migrationBuilder.Sql(@"
UPDATE Restaurants SET [Plan] = 'Paid', PlanName = 'Monthly', PricingPlanId = '6f1c2a8e-0b1d-4c55-9a51-3f2b8c7d1a01' WHERE [Plan] = 'Monthly';
UPDATE Restaurants SET [Plan] = 'Paid', PlanName = 'Yearly', PricingPlanId = '6f1c2a8e-0b1d-4c55-9a51-3f2b8c7d1a12' WHERE [Plan] = 'Yearly';

-- Still on the sign-up trial (nothing changed since registering).
UPDATE r SET [Plan] = 'Trial', PlanName = 'Trial'
FROM Restaurants r
WHERE r.[Plan] = 'Free' AND r.PlanExpiresAt IS NOT NULL
  AND EXISTS (SELECT 1 FROM SubscriptionEvents e WHERE e.RestaurantId = r.Id AND e.Action = 'TrialStarted')
  AND NOT EXISTS (SELECT 1 FROM SubscriptionEvents e WHERE e.RestaurantId = r.Id AND e.Action <> 'TrialStarted');

UPDATE Restaurants SET PlanName = 'Free' WHERE [Plan] = 'Free' AND PlanName IS NULL;

UPDATE SubscriptionEvents SET PlanName = [Plan], [Plan] = 'Paid' WHERE [Plan] IN ('Monthly', 'Yearly');
UPDATE SubscriptionEvents SET [Plan] = 'Trial', PlanName = 'Trial' WHERE Action = 'TrialStarted';
UPDATE SubscriptionEvents SET PlanName = 'Free' WHERE [Plan] = 'Free' AND PlanName IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the old Free/Monthly/Yearly values (any paid plan that is not 12 months becomes Monthly).
            migrationBuilder.Sql(@"
UPDATE r SET [Plan] = CASE WHEN p.DurationMonths >= 12 THEN 'Yearly' ELSE 'Monthly' END
FROM Restaurants r LEFT JOIN PricingPlans p ON p.Id = r.PricingPlanId
WHERE r.[Plan] = 'Paid';
UPDATE Restaurants SET [Plan] = 'Free' WHERE [Plan] = 'Trial';
UPDATE SubscriptionEvents SET [Plan] = CASE WHEN PlanName = 'Yearly' THEN 'Yearly' ELSE 'Monthly' END WHERE [Plan] = 'Paid';
UPDATE SubscriptionEvents SET [Plan] = 'Free' WHERE [Plan] = 'Trial';");

            migrationBuilder.DropForeignKey(
                name: "FK_Restaurants_PricingPlans_PricingPlanId",
                table: "Restaurants");

            migrationBuilder.DropTable(
                name: "PlanPayments");

            migrationBuilder.DropTable(
                name: "PricingPlans");

            migrationBuilder.DropIndex(
                name: "IX_Restaurants_PricingPlanId",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "SubscriptionEvents");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PricingPlanId",
                table: "Restaurants");
        }
    }
}
