FROM mono:latest

# 安裝輕量級 ASP.NET 4.x 伺服器 mono-xsp4 與憑證工具
RUN apt-get update && \
    apt-get install -y --no-install-recommends mono-xsp4 ca-certificates && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

# 複製專案檔案與編譯產物
COPY . /app

# 確保啟動腳本具備執行權限與 Unix 換行格式
RUN chmod +x /app/entrypoint.sh && \
    sed -i 's/\r$//' /app/entrypoint.sh

# 預設對外通訊埠 (Railway 會透過環境變數 $PORT 動態指定)
EXPOSE 8080

ENTRYPOINT ["/app/entrypoint.sh"]
