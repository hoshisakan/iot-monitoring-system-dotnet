using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pico2WH.Pi5.IIoT.Infrastructure.Migrations.Dev
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dev");

            migrationBuilder.CreateTable(
                name: "app_logs",
                schema: "dev",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    source_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    device_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_control_audits",
                schema: "dev",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    command = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value_percent = table.Column<int>(type: "integer", nullable: false),
                    value_16bit = table.Column<int>(type: "integer", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_control_audits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_ui_events",
                schema: "dev",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    device_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    event_value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    site_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    ingested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_ui_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "dev",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_records",
                schema: "dev",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    site_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    device_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    server_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_sync_back = table.Column<bool>(type: "boolean", nullable: false),
                    temperature_c = table.Column<double>(type: "double precision", nullable: true),
                    humidity_pct = table.Column<double>(type: "double precision", nullable: true),
                    lux = table.Column<double>(type: "double precision", nullable: true),
                    co2_ppm = table.Column<double>(type: "double precision", nullable: true),
                    temperature_c_scd41 = table.Column<double>(type: "double precision", nullable: true),
                    humidity_pct_scd41 = table.Column<double>(type: "double precision", nullable: true),
                    pir_active = table.Column<bool>(type: "boolean", nullable: true),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemetry_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "dev",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "dev",
                columns: table => new
                {
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.token_id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "dev",
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_logs_channel_created_at_utc",
                schema: "dev",
                table: "app_logs",
                columns: new[] { "channel", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_device_control_audits_request_id",
                schema: "dev",
                table: "device_control_audits",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_ui_events_device_id_device_time_utc",
                schema: "dev",
                table: "device_ui_events",
                columns: new[] { "device_id", "device_time_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_device_ui_events_site_id_device_id_device_time_utc",
                schema: "dev",
                table: "device_ui_events",
                columns: new[] { "site_id", "device_id", "device_time_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_devices_device_id",
                schema: "dev",
                table: "devices",
                column: "device_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "dev",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                schema: "dev",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_telemetry_records_device_id_device_time",
                schema: "dev",
                table: "telemetry_records",
                columns: new[] { "device_id", "device_time" });

            migrationBuilder.CreateIndex(
                name: "ix_telemetry_records_device_id_device_time_is_sync_back",
                schema: "dev",
                table: "telemetry_records",
                columns: new[] { "device_id", "device_time", "is_sync_back" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                schema: "dev",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_logs",
                schema: "dev");

            migrationBuilder.DropTable(
                name: "device_control_audits",
                schema: "dev");

            migrationBuilder.DropTable(
                name: "device_ui_events",
                schema: "dev");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "dev");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "dev");

            migrationBuilder.DropTable(
                name: "telemetry_records",
                schema: "dev");

            migrationBuilder.DropTable(
                name: "users",
                schema: "dev");
        }
    }
}
