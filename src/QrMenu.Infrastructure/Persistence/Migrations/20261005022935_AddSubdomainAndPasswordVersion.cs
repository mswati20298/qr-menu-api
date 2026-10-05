using Microsoft.EntityFrameworkCore.Migrations;
using QrMenu.Application.Restaurants;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubdomainAndPasswordVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PasswordVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Subdomain",
                table: "Restaurants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_Subdomain",
                table: "Restaurants",
                column: "Subdomain",
                unique: true,
                filter: "[Subdomain] IS NOT NULL");

            // Existing restaurants get their slug as their own address when it is a valid, non-reserved name.
            // Slugs are already unique, so the new unique index cannot clash.
            var reserved = string.Join(", ", SubdomainRules.ReservedNames.Select(n => $"N'{n}'"));
            migrationBuilder.Sql($@"
UPDATE [Restaurants] SET [Subdomain] = [Slug]
WHERE LEN([Slug]) BETWEEN {SubdomainRules.MinLength} AND {SubdomainRules.MaxLength}
  AND [Slug] NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_BIN
  AND [Slug] NOT LIKE '-%' AND [Slug] NOT LIKE '%-' AND [Slug] NOT LIKE '%--%'
  AND [Slug] NOT IN ({reserved});");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Restaurants_Subdomain",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PasswordVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Subdomain",
                table: "Restaurants");
        }
    }
}
