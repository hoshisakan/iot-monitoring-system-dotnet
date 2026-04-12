# 前端（React）

**Vite** 驅動之 **SPA**，**React** + **TypeScript**，UI 為 **Mantine**。監控相關程式以簡化 **Clean Architecture** 分層：`monitoring/domain`（實體與 **Repository** 介面）、`monitoring/application`（**use cases**）、`monitoring/infrastructure`（**HTTP** 實作）、`presentation`（頁面與版面）。

## 指令

於本目錄：

```bash
npm install
npm run dev    # 開發伺服器
npm run build  # 正式建置
npm run lint
```

## API 連線

實際 **Base URL** 與 **HTTPS**／**Proxy** 依部署而定（例如經儲存庫根目錄 **Nginx** 反向代理至後端）。請與 `src/infrastructure/apiBase.ts` 等設定一致。

---