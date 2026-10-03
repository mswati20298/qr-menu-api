using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantThemeColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThemeColor",
                table: "Restaurants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "masala");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThemeColor",
                table: "Restaurants");
        }
    }
}
