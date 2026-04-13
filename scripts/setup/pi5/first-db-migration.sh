#!/usr/bin/env bash
set -euo pipefail

# 首次在 Pi 5 / WSL 移植後執行 EF Core migration
# 用法：
#   ./scripts/setup/pi5/first-db-migration.sh
#   DB_SCHEMA=prod ./scripts/setup/pi5/first-db-migration.sh
#   CONNECTION_STRING="Host=127.0.0.1;Port=5432;Database=xxx;Username=xxx;Password=xxx" ./scripts/setup/pi5/first-db-migration.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../" && pwd)"
BACKEND_SRC="$REPO_ROOT/app/backend/src"
INFRA_CSPROJ="Pico2WH.Pi5.IIoT.Infrastructure/Pico2WH.Pi5.IIoT.Infrastructure.csproj"
API_CSPROJ="Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj"

DB_SCHEMA="${DB_SCHEMA:-dev}"
AUTO_MIGRATE="${AUTO_MIGRATE:-false}"

echo "[INFO] Repo root: $REPO_ROOT"
echo "[INFO] Backend src: $BACKEND_SRC"
echo "[INFO] Target schema: $DB_SCHEMA"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[ERROR] dotnet 未安裝。請先執行 scripts/setup/pi5/install-dev.sh"
  exit 1
fi

if [ ! -d "$BACKEND_SRC" ]; then
  echo "[ERROR] 找不到 backend src 目錄：$BACKEND_SRC"
  exit 1
fi

if [ -n "${CONNECTION_STRING:-}" ]; then
  export ConnectionStrings__Default="$CONNECTION_STRING"
  echo "[INFO] 已使用 CONNECTION_STRING 覆寫 ConnectionStrings__Default"
fi

# 設定執行期與設計期 schema / migration 行為
export Database__DefaultSchema="$DB_SCHEMA"
export Database__AutoMigrate="$AUTO_MIGRATE"

if ! command -v dotnet-ef >/dev/null 2>&1; then
  echo "[INFO] dotnet-ef 尚未安裝，嘗試安裝..."
  dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
  export PATH="$PATH:$HOME/.dotnet/tools"
fi

cd "$BACKEND_SRC"

echo "[STEP] 檢查可用 migration..."
dotnet ef migrations list \
  --project "$INFRA_CSPROJ" \
  --startup-project "$API_CSPROJ"

echo "[STEP] 套用 migration（database update）..."
dotnet ef database update \
  --project "$INFRA_CSPROJ" \
  --startup-project "$API_CSPROJ"

echo "[DONE] Migration 已套用完成。"
echo "[TIP] 若要驗證，請查看資料庫中的 __EFMigrationsHistory 與目標 schema 資料表。"
