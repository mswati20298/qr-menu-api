using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantBackgrounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackgroundMode",
                table: "Restaurants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RestaurantBackgrounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Slots = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantBackgrounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantBackgrounds_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantBackgrounds_RestaurantId",
                table: "RestaurantBackgrounds",
                column: "RestaurantId");

            // Keep the background photo each restaurant has already uploaded:
            // it becomes that restaurant's default background image.
            migrationBuilder.Sql(
                @"INSERT INTO RestaurantBackgrounds (Id, RestaurantId, ImageUrl, Slots, IsDefault, SortOrder)
                  SELECT NEWID(), Id, CoverImageUrl, 0, 1, 0
                  FROM Restaurants
                  WHERE CoverImageUrl IS NOT NULL AND CoverImageUrl <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestaurantBackgrounds");

            migrationBuilder.DropColumn(
                name: "BackgroundMode",
                table: "Restaurants");
        }
    }
}
