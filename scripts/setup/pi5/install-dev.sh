#!/usr/bin/env bash
set -euo pipefail

echo "[1/6] apt 基礎更新"
sudo apt update
sudo apt install -y \
  ca-certificates curl gnupg lsb-release software-properties-common \
  git unzip zip xz-utils make pkg-config

echo "[2/6] 安裝前端（Node.js 20）"
if ! command -v node >/dev/null 2>&1 || ! node -v | grep -qE '^v20\.'; then
  curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
  sudo apt install -y nodejs
fi
node -v
npm -v

echo "[3/6] 安裝後端（.NET 8 SDK + dotnet-ef）"
if ! command -v dotnet >/dev/null 2>&1; then
  curl -fsSL https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb
  sudo dpkg -i /tmp/packages-microsoft-prod.deb
  sudo apt update
fi
sudo apt install -y dotnet-sdk-8.0
dotnet --version
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef --version || true

echo "[4/6] 安裝韌體編譯工具（Pico SDK toolchain）"
sudo apt install -y \
  cmake ninja-build build-essential python3 \
  gcc-arm-none-eabi binutils-arm-none-eabi libnewlib-arm-none-eabi \
  openocd libusb-1.0-0-dev

echo "[5/6] 安裝 Docker（開發環境建議）"
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker "$USER"
fi
docker --version || true
docker compose version || true

echo "[6/6] 下載 Pico SDK（若尚未存在）"
mkdir -p "$HOME/pico"
if [ ! -d "$HOME/pico/pico-sdk/.git" ]; then
  git clone --depth=1 https://github.com/raspberrypi/pico-sdk.git "$HOME/pico/pico-sdk"
fi
if ! grep -q "PICO_SDK_PATH" "$HOME/.bashrc"; then
  echo 'export PICO_SDK_PATH="$HOME/pico/pico-sdk"' >> "$HOME/.bashrc"
fi
export PICO_SDK_PATH="$HOME/pico/pico-sdk"
echo "PICO_SDK_PATH=$PICO_SDK_PATH"

echo "完成：請重新登入 shell（或執行 source ~/.bashrc）再開始建置。"
