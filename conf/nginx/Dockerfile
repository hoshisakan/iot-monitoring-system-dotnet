# syntax=docker/dockerfile:1
# 多階段：建置前端 → 以 nginx 提供靜態檔與 /api 反代
FROM node:20-alpine AS frontend-builder
WORKDIR /build

# 依賴層獨立，利於快取（原始碼變更時不必重跑 npm ci）
COPY app/frontend/package.json app/frontend/package-lock.json ./
# Ensure devDependencies are present for `tsc -b && vite build`.
ENV NODE_ENV=development
RUN npm ci --include=dev

COPY app/frontend/ ./
RUN npx tsc -v && npm ls typescript vite @types/node --depth=0
RUN npm run build

FROM nginx:stable-alpine

COPY --from=frontend-builder /build/dist /usr/share/nginx/html
