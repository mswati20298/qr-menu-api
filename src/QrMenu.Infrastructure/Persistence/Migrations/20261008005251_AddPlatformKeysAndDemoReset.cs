using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QrMenu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformKeysAndDemoReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DemoAutoResetDays",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 30); // existing row: reset every 30 days

            migrationBuilder.AddColumn<string>(
                name: "GeminiApiKeyEncrypted",
                table: "PlatformSettings",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDemoResetAt",
                table: "PlatformSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazorpayKeyId",
                table: "PlatformSettings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazorpayKeySecretEncrypted",
                table: "PlatformSettings",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazorpayWebhookSecretEncrypted",
                table: "PlatformSettings",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DemoAutoResetDays",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "GeminiApiKeyEncrypted",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "LastDemoResetAt",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RazorpayKeyId",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RazorpayKeySecretEncrypted",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "RazorpayWebhookSecretEncrypted",
                table: "PlatformSettings");
        }
    }
}
