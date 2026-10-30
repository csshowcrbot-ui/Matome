#!/bin/sh
PORT="${PORT:-8080}"
echo "啟動 Matome (間調) 會議室預約系統 (Mono XSP4)，監聽通訊埠: ${PORT} ..."
exec xsp4 --nonstop --port "${PORT}" --address 0.0.0.0 --root /app
