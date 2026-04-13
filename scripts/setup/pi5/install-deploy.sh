#!/usr/bin/env bash
set -euo pipefail

echo "[1/5] 系統基礎套件"
sudo apt update
sudo apt install -y ca-certificates curl gnupg lsb-release jq htop

echo "[2/5] 安裝 Docker Engine + Compose Plugin"
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker "$USER"
fi

echo "[3/5] 驗證 Docker"
docker --version
docker compose version

echo "[4/5] 啟用開機自動啟動 Docker"
sudo systemctl enable docker
sudo systemctl restart docker

echo "[5/5] 建立部署常用目錄（若不存在）"
mkdir -p logs data conf/nginx/certs conf/mqtt_broker/certs

echo "完成：請重新登入 shell（使 docker 群組生效），再執行 docker compose。"
