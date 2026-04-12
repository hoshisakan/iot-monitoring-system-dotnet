#!/bin/bash
#
# MQTT Broker（Mosquitto）TLS 憑證產生腳本（合併原 local／server 兩版）
# ---------------------------------------------------------------------------
# 用法：
#   ./scripts/setup/mqtt/generate_certs.sh local    # -> conf/mqtt_broker/certs/local
#   ./scripts/setup/mqtt/generate_certs.sh server   # -> conf/mqtt_broker/certs/server
#
# 可選環境變數（覆寫 SAN 中的 broker IP，與 MOSQUITTO_HOST_IP／實際連線一致時使用）：
#   MQTT_SAN_IP  例如 MQTT_SAN_IP=172.95.25.4 ./scripts/setup/mqtt/generate_certs.sh server
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

MODE="${1:-}"
case "$MODE" in
  local)
    CERT_DIR="$PROJECT_ROOT/conf/mqtt_broker/certs/local"
    # 與舊版 local 腳本一致：預設固定橋接 IP；可透過 MQTT_SAN_IP 覆寫
    BROKER_IP="${MQTT_SAN_IP:-172.20.10.3}"
    ;;
  server)
    CERT_DIR="$PROJECT_ROOT/conf/mqtt_broker/certs/server"
    DETECTED_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
    BROKER_IP="${MQTT_SAN_IP:-${DETECTED_IP:-127.0.0.1}}"
    ;;
  *)
    echo "用法: $0 {local|server}" >&2
    echo "  local  - 輸出至 conf/mqtt_broker/certs/local（預設 SAN IP: 172.20.10.3，可設 MQTT_SAN_IP）" >&2
    echo "  server - 輸出至 conf/mqtt_broker/certs/server（預設 SAN IP: 本機第一個 IPv4，可設 MQTT_SAN_IP）" >&2
    exit 1
    ;;
esac

DOCKER_SERVICE_NAME="${MQTT_SERVICE_NAME:-mqtt_broker}"

echo "🛡️ MQTT TLS: mode=$MODE | CERT_DIR=$CERT_DIR"
echo "🌐 Target IP: $BROKER_IP | Internal DNS: $DOCKER_SERVICE_NAME"

sudo mkdir -p "$CERT_DIR"
sudo chown -R "$USER:$USER" "$PROJECT_ROOT/conf/mqtt_broker" 2>/dev/null || true
rm -f "$CERT_DIR"/*

# --- CA ---
cat > "$CERT_DIR/ca.conf" <<EOF
[req]
distinguished_name = ca_dn
x509_extensions = v3_ca
prompt = no
[ca_dn]
CN = Ulfius-Root-CA
[v3_ca]
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid:always,issuer
basicConstraints = critical, CA:true
keyUsage = critical, digitalSignature, cRLSign, keyCertSign
EOF

openssl req -new -x509 -days 3650 -config "$CERT_DIR/ca.conf" \
  -keyout "$CERT_DIR/ca.key" -out "$CERT_DIR/ca.crt" -nodes -sha256

# --- Server ---
openssl genrsa -out "$CERT_DIR/server.key" 2048

cat > "$CERT_DIR/server.conf" <<EOF
[req]
distinguished_name = server_dn
req_extensions = v3_req
prompt = no
[server_dn]
CN = $DOCKER_SERVICE_NAME
[v3_req]
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = IP:$BROKER_IP,IP:127.0.0.1,DNS:localhost,DNS:$DOCKER_SERVICE_NAME,DNS:ulfius-broker
EOF

openssl req -new -key "$CERT_DIR/server.key" -out "$CERT_DIR/server.csr" \
  -config "$CERT_DIR/server.conf" -sha256

openssl x509 -req -in "$CERT_DIR/server.csr" \
  -CA "$CERT_DIR/ca.crt" -CAkey "$CERT_DIR/ca.key" \
  -CAcreateserial -out "$CERT_DIR/server.crt" -days 3650 -sha256 \
  -extfile "$CERT_DIR/server.conf" -extensions v3_req

echo "🔐 Project Ulfius: Applying permissions (mosquitto 1883)..."
rm -f "$CERT_DIR"/*.conf "$CERT_DIR"/*.csr "$CERT_DIR"/*.srl 2>/dev/null || true

sudo chown -R 1883:1883 "$CERT_DIR"
sudo chmod 755 "$CERT_DIR"
sudo chmod 644 "$CERT_DIR/ca.crt" "$CERT_DIR/server.crt"
sudo chmod 600 "$CERT_DIR/server.key"

echo "✅ Success! MQTT certificates are live in: $CERT_DIR"
