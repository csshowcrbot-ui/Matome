FROM mono:6.12

# 因底層 Debian Buster 已生命週期終止 (EOL)，將 APT 來源指向 archive.debian.org 並關閉過期檢查
RUN sed -i 's/deb.debian.org/archive.debian.org/g' /etc/apt/sources.list && \
    sed -i 's|security.debian.org/debian-security|archive.debian.org/debian-security|g' /etc/apt/sources.list && \
    sed -i '/buster-updates/d' /etc/apt/sources.list && \
    echo 'Acquire::Check-Valid-Until "false";' > /etc/apt/apt.conf.d/99no-check-valid-until && \
    apt-get update && \
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
