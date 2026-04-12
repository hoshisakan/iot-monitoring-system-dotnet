# Pico2WH.Pi5.IIoT.Api — 多階段建置（.NET 8）
# build context: app/backend（與 docker-compose 之 context: ./app/backend 一致）
#
# 執行時請透過環境變數覆寫 ConnectionStrings、Mqtt、Jwt 等（見 docker-compose.yml）。
# 日誌目錄：/app/logs（對應 Serilog File sink）

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

# 先複製專案檔以利 restore 層快取
COPY src/Pico2WH.Pi5.IIoT.Domain/Pico2WH.Pi5.IIoT.Domain.csproj Pico2WH.Pi5.IIoT.Domain/
COPY src/Pico2WH.Pi5.IIoT.Application/Pico2WH.Pi5.IIoT.Application.csproj Pico2WH.Pi5.IIoT.Application/
COPY src/Pico2WH.Pi5.IIoT.Infrastructure/Pico2WH.Pi5.IIoT.Infrastructure.csproj Pico2WH.Pi5.IIoT.Infrastructure/
COPY src/Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj Pico2WH.Pi5.IIoT.Api/

RUN dotnet restore Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj

COPY src/ ./

RUN dotnet publish Pico2WH.Pi5.IIoT.Api/Pico2WH.Pi5.IIoT.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Serilog 檔案 sink、執行身分（aspnet 映像之 app 使用者）可寫入
RUN mkdir -p /app/logs \
    && chown -R app:app /app/logs

ENV ASPNETCORE_URLS=http://+:5163

USER app

ENTRYPOINT ["dotnet", "Pico2WH.Pi5.IIoT.Api.dll"]
