using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pico2WH.Pi5.IIoT.Infrastructure.Migrations.Dev
{
    /// <summary>
    /// 將 <c>raw_payload</c> 移回資料表最後一欄（先前擴充欄位時 <c>ADD COLUMN</c> 插在 <c>raw_payload</c> 之後）。
    /// </summary>
    public partial class RawPayloadColumnLast : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "dev"."telemetry_records" ADD COLUMN raw_payload__reorder jsonb;
                UPDATE "dev"."telemetry_records" SET raw_payload__reorder = raw_payload;
                ALTER TABLE "dev"."telemetry_records" DROP COLUMN raw_payload;
                ALTER TABLE "dev"."telemetry_records" RENAME COLUMN raw_payload__reorder TO raw_payload;
                """);
        }

        /// <remarks>僅調整欄位顯示順序，不實作 Down。</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
