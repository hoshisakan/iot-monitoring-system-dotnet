# Cloudflare Tunnel 與本專案

## 概要

- **Nginx** 已掛載 `conf/nginx/cloudflare-real-ip.conf`，在連線來自 Cloudflare 邊緣時以 **`CF-Connecting-IP`** 還原客戶端 IP（請定期對齊 [Cloudflare IP 清單](https://www.cloudflare.com/ips/) 更新該檔）。
- **後端** 已啟用 **`ForwardedHeaders`**，信任 RFC1918 私有網段之前置代理，還原 **`X-Forwarded-Proto`／`X-Forwarded-For`**（與 Nginx 反代一致）。

## 方式 A：Docker Compose + Token（建議）

1. Cloudflare **Zero Trust** → **Networks** → **Tunnels** → 建立 Tunnel → **Install connector** 複製 **token**。
2. 專案根 `.env` 設定：
   - `TUNNEL_TOKEN=<貼上 token>`
   - `CLOUDFLARED_HOST_IP` 須落在 `MONITOR_SYSTEM_SUBNET` 內且**不重複**其他服務（範例：`172.95.26.7`）。
3. 在 Cloudflare 該 Tunnel 的 **Public Hostname**（Zero Trust → 該 Tunnel → **Public Hostname**）：
   - **cloudflared 與 Nginx 同在 Docker Compose 網路時**，**Service** 請填可解析的服務名，例如 **`https://nginx:443`**（勿填 `localhost`，容器內 localhost 不是 Nginx）。
   - 來源為**自簽憑證**時，Cloudflare 邊緣對來源 SSL 請選 **Full**（非 **Full (strict)**），否則可能無法連線。
   - 若 **cloudflared 安裝在宿主機**、僅 Nginx 用 Docker 對外綁 `127.0.0.1:443`，則可試 **`https://127.0.0.1:443`**（依實際埠映射調整）。
4. 啟動：

   ```bash
   docker compose --profile cloudflare up -d
   ```

5. **自簽憑證**：若回源使用 HTTPS 連到 Nginx，Cloudflare 儀表板中該服務的 **SSL** 可設為 **Full (strict)** 僅在來源為合法憑證時成立；自簽憑證請用 **Full** 或於 **cloudflared** 使用 `originRequest.noTLSVerify`（方式 B）。

> **注意**：`docker-compose` 內 `cloudflared` 服務**僅**實作 **token** 模式；**Public Hostname 回源** 在 Cloudflare 網頁設定，**不**在本 repo 的 `config.yml`。

## 方式 B：具名 Tunnel + 本機 config.yml（進階）

1. 安裝 `cloudflared` 於宿主或另起容器。
2. 複製 `config.yml.example` 為 `config.yml`（已列入 `.gitignore`），填入 `tunnel` UUID、`credentials-file`。
3. `ingress` 將 `hostname` 指向你的網域，**service** 建議使用 **`https://nginx:443`**（與 Compose 同網路時），並在 `originRequest` 使用 **`noTLSVerify: true`**（對應自簽 Nginx 憑證）。
4. 執行範例：

   ```bash
   cloudflared tunnel --config /path/to/config.yml run
   ```

## MQTT

- **MQTT（1883/8883）** 不經標準 HTTP Tunnel；裝置連線仍須 **區網／埠轉發／VPN** 等另行規劃。
