#!/bin/bash
# 確保腳本在出錯時停止
set -e

echo "🧹 Cleaning previous build artifacts..."
rm -rf build/

echo "🚀 Starting Automated Build and Flash for Pico 2 WH..."

# 1. 編譯 (強制指定 pico2_w 並進行多線程編譯)
cmake -B build -DPICO_BOARD=pico2_w .
make -C build -j$(nproc)

echo "⚡ Flashing via Debug Probe (CMSIS-DAP)..."

# 2. 使用 OpenOCD 進行燒錄
# 改用 init -> program -> shutdown 流程，避免 libusb 資源釋放衝突
openocd -f interface/cmsis-dap.cfg \
        -f target/rp2350.cfg \
        -c "adapter speed 5000" \
        -c "init" \
        -c "program build/iiot_firmware.elf verify reset" \
        -c "shutdown"

echo "✅ Success! Pico 2 WH firmware deployed and verified."
