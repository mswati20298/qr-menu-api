using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackAndTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingTwoFactorSecretEncrypted",
                table: "SuperAdmins",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryCodeHashes",
                table: "SuperAdmins",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "SuperAdmins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TwoFactorLastStep",
                table: "SuperAdmins",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecretEncrypted",
                table: "SuperAdmins",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feedback_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_IsPublished_CreatedAt",
                table: "Feedback",
                columns: new[] { "IsPublished", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_OrderId",
                table: "Feedback",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_OwnerPerRestaurant",
                table: "Feedback",
                column: "RestaurantId",
                unique: true,
                filter: "[Kind] = 'Owner'");

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_RestaurantId_Kind_CreatedAt",
                table: "Feedback",
                columns: new[] { "RestaurantId", "Kind", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Feedback");

            migrationBuilder.DropColumn(
                name: "PendingTwoFactorSecretEncrypted",
                table: "SuperAdmins");

            migrationBuilder.DropColumn(
                name: "RecoveryCodeHashes",
                table: "SuperAdmins");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "SuperAdmins");

            migrationBuilder.DropColumn(
                name: "TwoFactorLastStep",
                table: "SuperAdmins");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecretEncrypted",
                table: "SuperAdmins");
        }
    }
}
