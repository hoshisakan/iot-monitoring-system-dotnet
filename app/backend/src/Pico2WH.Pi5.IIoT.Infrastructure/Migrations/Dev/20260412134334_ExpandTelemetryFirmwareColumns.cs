using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pico2WH.Pi5.IIoT.Infrastructure.Migrations.Dev
{
    /// <inheritdoc />
    public partial class ExpandTelemetryFirmwareColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "accel_x",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "accel_y",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "accel_z",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "gas_resistance_ohm",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "gyro_x",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "gyro_y",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "gyro_z",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pressure_hpa",
                schema: "dev",
                table: "telemetry_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rssi",
                schema: "dev",
                table: "telemetry_records",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accel_x",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "accel_y",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "accel_z",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "gas_resistance_ohm",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "gyro_x",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "gyro_y",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "gyro_z",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "pressure_hpa",
                schema: "dev",
                table: "telemetry_records");

            migrationBuilder.DropColumn(
                name: "rssi",
                schema: "dev",
                table: "telemetry_records");
        }
    }
}
