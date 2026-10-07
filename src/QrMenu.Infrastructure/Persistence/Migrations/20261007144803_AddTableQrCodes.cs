using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableQrCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "Tables",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "AllowLinkTakeaway",
                table: "Restaurants",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "QrSessionHours",
                table: "Restaurants",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "RequireTableQr",
                table: "Restaurants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing tables get their own random code (NEWID() is evaluated per row). Old printed QR codes keep
            // working until the owner turns on "only table QR orders" and prints the new cards.
            migrationBuilder.Sql("UPDATE [Tables] SET [QrCode] = UPPER(LEFT(REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''), 12)) WHERE [QrCode] = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "AllowLinkTakeaway",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "QrSessionHours",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "RequireTableQr",
                table: "Restaurants");
        }
    }
}
