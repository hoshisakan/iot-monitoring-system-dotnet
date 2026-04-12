using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pico2WH.Pi5.IIoT.Infrastructure.Migrations.Prod
{
    /// <inheritdoc />
    public partial class MiInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "prod");

            migrationBuilder.RenameTable(
                name: "users",
                schema: "dev",
                newName: "users",
                newSchema: "prod");

            migrationBuilder.RenameTable(
                name: "telemetry_records",
                schema: "dev",
                newName: "telemetry_records",
                newSchema: "prod");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                schema: "dev",
                newName: "refresh_tokens",
                newSchema: "prod");

            migrationBuilder.RenameTable(
                name: "devices",
                schema: "dev",
                newName: "devices",
                newSchema: "prod");

            migrationBuilder.RenameTable(
                name: "device_ui_events",
                schema: "dev",
                newName: "device_ui_events",
                newSchema: "prod");

            migrationBuilder.RenameTable(
                name: "device_control_audits",
                schema: "dev",
                newName: "device_control_audits",
                newSchema: "prod");

            migrationBuilder.RenameTable(
                name: "app_logs",
                schema: "dev",
                newName: "app_logs",
                newSchema: "prod");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dev");

            migrationBuilder.RenameTable(
                name: "users",
                schema: "prod",
                newName: "users",
                newSchema: "dev");

            migrationBuilder.RenameTable(
                name: "telemetry_records",
                schema: "prod",
                newName: "telemetry_records",
                newSchema: "dev");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                schema: "prod",
                newName: "refresh_tokens",
                newSchema: "dev");

            migrationBuilder.RenameTable(
                name: "devices",
                schema: "prod",
                newName: "devices",
                newSchema: "dev");

            migrationBuilder.RenameTable(
                name: "device_ui_events",
                schema: "prod",
                newName: "device_ui_events",
                newSchema: "dev");

            migrationBuilder.RenameTable(
                name: "device_control_audits",
                schema: "prod",
                newName: "device_control_audits",
                newSchema: "dev");

            migrationBuilder.RenameTable(
                name: "app_logs",
                schema: "prod",
                newName: "app_logs",
                newSchema: "dev");
        }
    }
}
