#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 /absolute/backup/dir [retention_days]"
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

BACKUP_DIR="$1"
RETENTION_DAYS="${2:-14}"

if [[ "${BACKUP_DIR}" != /* ]]; then
  echo "[ERROR] Backup dir must be an absolute path."
  usage
  exit 1
fi

if ! [[ "${RETENTION_DAYS}" =~ ^[0-9]+$ ]]; then
  echo "[ERROR] retention_days must be an integer."
  usage
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${PROJECT_ROOT}"

if [[ -f ".env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source ".env"
  set +a
fi

: "${POSTGRES_USER:?POSTGRES_USER is required in .env}"
: "${POSTGRES_DB:?POSTGRES_DB is required in .env}"

mkdir -p "${BACKUP_DIR}"

timestamp="$(date +%Y%m%d_%H%M%S)"
base_name="db_${POSTGRES_DB}_${timestamp}"
tmp_file="${BACKUP_DIR}/${base_name}.dump.tmp"
dump_file="${BACKUP_DIR}/${base_name}.dump"
sha_file="${BACKUP_DIR}/${base_name}.sha256"

echo "[INFO] Backup start: ${dump_file}"

docker compose exec -T postgresql \
  pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -Fc > "${tmp_file}"

mv "${tmp_file}" "${dump_file}"
sha256sum "${dump_file}" > "${sha_file}"

echo "[INFO] Backup completed: ${dump_file}"
echo "[INFO] Checksum generated: ${sha_file}"

find "${BACKUP_DIR}" -type f -name "db_${POSTGRES_DB}_*.dump" -mtime +"${RETENTION_DAYS}" -delete
find "${BACKUP_DIR}" -type f -name "db_${POSTGRES_DB}_*.sha256" -mtime +"${RETENTION_DAYS}" -delete

echo "[INFO] Retention cleanup finished (${RETENTION_DAYS} days)."
