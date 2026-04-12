#!/bin/sh

set -e

# 1. 🛡️ 檢查環境變數：確保 Ulfius Node 認證資訊存在
if [ -z "${MOSQUITTO_USERNAME}" ] || [ -z "${MOSQUITTO_PASSWORD}" ]; then
  echo "❌ Error: MOSQUITTO_USERNAME or MOSQUITTO_PASSWORD not defined"
  exit 1
fi

# 2. 🔑 建立與修正密碼檔 (確保服務具備讀取權限)
cd /mosquitto
touch passwordfile
# 使用 -b (batch mode) 自動寫入加密密碼
mosquitto_passwd -b /mosquitto/passwordfile "${MOSQUITTO_USERNAME}" "${MOSQUITTO_PASSWORD}"

# 💡 關鍵修正：將擁有權交給 mosquitto 使用者 (UID 1883)
# 雖然 2.0.x 版本會噴出 Warning，但這能確保服務「讀得到」檔案並成功啟動
chown mosquitto:mosquitto /mosquitto/passwordfile
chmod 600 /mosquitto/passwordfile

# 3. 📝 日誌與證書目錄權限修復
if [ -n "$MOSQUITTO_LOGFILENAME" ]; then
  # 確保日誌目錄存在
  mkdir -p "$(dirname "$MOSQUITTO_LOGFILENAME")"
  touch "$MOSQUITTO_LOGFILENAME"
  
  # 將日誌擁有權交給 mosquitto 用戶以便寫入
  chown mosquitto:mosquitto "$MOSQUITTO_LOGFILENAME"
  chmod 640 "$MOSQUITTO_LOGFILENAME"
  
  # 🛡️ 自動修復證書權限 (解決 OpenSSL Permission denied)
  # 這是讓 8883 (TLS) 埠成功開啟的核心步驟
  if [ -d "/mosquitto/config/certs" ]; then
    chown -R mosquitto:mosquitto /mosquitto/config/certs
    # 私鑰需嚴格權限 (600)，憑證可公開讀取 (644)
    find /mosquitto/config/certs -name "*.key" -exec chmod 600 {} + 2>/dev/null || true
    find /mosquitto/config/certs -name "*.crt" -exec chmod 644 {} + 2>/dev/null || true
    find /mosquitto/config/certs -name "*.pem" -exec chmod 644 {} + 2>/dev/null || true
  fi

  echo "✅ Project Ulfius: Broker initialized with functional permissions."
fi

# 4. 🚀 啟動：執行 CMD 傳入的 mosquitto 指令
exec "$@"
