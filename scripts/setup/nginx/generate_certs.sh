#!/bin/bash
#
# Nginx HTTPS 憑證產生（與 MQTT 分離，專用 CN／SAN 為 nginx／HTTPS）
# ---------------------------------------------------------------------------
# 建議：與 conf/mqtt_broker/certs/* 分開管理，避免 MQTT 與 Nginx 共用同一 server 金鑰。
#
# 用法：
#   ./scripts/setup/nginx/generate_certs.sh local    # 測試／本機對應 -> conf/nginx/certs/local
#   ./scripts/setup/nginx/generate_certs.sh server   # 生產／部署主機對應 -> conf/nginx/certs/server
#
# 可選環境變數（覆寫 SAN 中的主要 IP，與 docker-compose 之 NGINX_HOST_IP 對齊時使用）：
#   NGINX_SAN_IP   例如 NGINX_SAN_IP=172.95.25.6 ./scripts/setup/nginx/generate_certs.sh local
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

MODE="${1:-}"
case "$MODE" in
  local)
    CERT_DIR="$PROJECT_ROOT/conf/nginx/certs/local"
    # 與 scripts/setup/mqtt/generate_certs.sh local 類似：固定橋接 IP 可透過 NGINX_SAN_IP 覆寫
    DEFAULT_IP="${NGINX_SAN_IP:-172.95.25.6}"
    ;;
  server)
    CERT_DIR="$PROJECT_ROOT/conf/nginx/certs/server"
    DETECTED_IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
    DEFAULT_IP="${NGINX_SAN_IP:-${DETECTED_IP:-127.0.0.1}}"
    ;;
  *)
    echo "用法: $0 {local|server}" >&2
    echo "  local  - 輸出至 conf/nginx/certs/local（預設 SAN IP: 172.20.10.3，可設 NGINX_SAN_IP）" >&2
    echo "  server - 輸出至 conf/nginx/certs/server（預設 SAN IP: 本機第一個 IPv4，可設 NGINX_SAN_IP）" >&2
    exit 1
    ;;
esac

DOCKER_SERVICE_NAME="${NGINX_SERVICE_NAME:-nginx}"
NGINX_IP="$DEFAULT_IP"

echo "🔐 Nginx TLS: mode=$MODE | CERT_DIR=$CERT_DIR"
echo "🌐 SAN IP: $NGINX_IP | DNS: localhost, ${DOCKER_SERVICE_NAME}"

sudo mkdir -p "$CERT_DIR"
sudo chown -R "$USER:$USER" "$PROJECT_ROOT/conf/nginx" 2>/dev/null || true
rm -f "$CERT_DIR"/*

# --- CA（私有根，僅內網／開發信任；對外正式環境請改公有 CA 或企業 PKI）---
cat > "$CERT_DIR/ca.conf" <<EOF
[req]
distinguished_name = ca_dn
x509_extensions = v3_ca
prompt = no
[ca_dn]
CN = Ulfius-Nginx-Root-CA-${MODE}
[v3_ca]
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid:always,issuer
basicConstraints = critical, CA:true
keyUsage = critical, digitalSignature, cRLSign, keyCertSign
EOF

openssl req -new -x509 -days 3650 -config "$CERT_DIR/ca.conf" \
  -keyout "$CERT_DIR/ca.key" -out "$CERT_DIR/ca.crt" -nodes -sha256

# --- Server：CN 為 nginx 服務名，SAN 含 HTTPS 常用主機／IP ---
openssl genrsa -out "$CERT_DIR/server.key" 2048

cat > "$CERT_DIR/server.conf" <<EOF
[req]
distinguished_name = server_dn
req_extensions = v3_req
prompt = no
[server_dn]
CN = ${DOCKER_SERVICE_NAME}
[v3_req]
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = IP:${NGINX_IP},IP:127.0.0.1,DNS:localhost,DNS:${DOCKER_SERVICE_NAME}
EOF

openssl req -new -key "$CERT_DIR/server.key" -out "$CERT_DIR/server.csr" \
  -config "$CERT_DIR/server.conf" -sha256

openssl x509 -req -in "$CERT_DIR/server.csr" \
  -CA "$CERT_DIR/ca.crt" -CAkey "$CERT_DIR/ca.key" \
  -CAcreateserial -out "$CERT_DIR/server.crt" -days 3650 -sha256 \
  -extfile "$CERT_DIR/server.conf" -extensions v3_req

rm -f "$CERT_DIR"/*.conf "$CERT_DIR"/*.csr "$CERT_DIR"/*.srl 2>/dev/null || true

sudo chmod 755 "$CERT_DIR"
sudo chmod 644 "$CERT_DIR/ca.crt" "$CERT_DIR/server.crt"
sudo chmod 600 "$CERT_DIR/ca.key" "$CERT_DIR/server.key"
sudo chown -R root:root "$CERT_DIR"

echo "✅ Nginx TLS 憑證已寫入: $CERT_DIR"
echo "   ssl_certificate     $CERT_DIR/server.crt"
echo "   ssl_certificate_key $CERT_DIR/server.key"
echo "   （瀏覽器信任請匯入 ca.crt 或使用 -k / 內網政策）"
