#!/bin/bash

# 清理舊進程
echo "清理舊進程..."
pkill -9 -f "dotnet.*Signage" 2>/dev/null || true
sleep 2

# 清理舊數據庫
echo "清理舊數據庫..."
rm -f /workspaces/TourismSignageSystem/Signage.Server/Data/signage.db

# 啟動後端
echo "啟動後端 API (https://localhost:7245)..."
cd /workspaces/TourismSignageSystem/Signage.Server
dotnet run --launch-profile https > /tmp/backend.log 2>&1 &
BACKEND_PID=$!
echo "後端 PID: $BACKEND_PID"
sleep 12

# 檢查後端是否運行
if ps -p $BACKEND_PID > /dev/null; then
  echo "✓ 後端已啟動"
else
  echo "✗ 後端啟動失敗"
  cat /tmp/backend.log | tail -30
  exit 1
fi

# 啟動前端
echo ""
echo "啟動前端應用 (https://localhost:7199)..."
cd /workspaces/TourismSignageSystem/Signage.App
dotnet run --launch-profile https > /tmp/app.log 2>&1 &
APP_PID=$!
echo "前端 PID: $APP_PID"
sleep 10

# 檢查前端是否運行
if ps -p $APP_PID > /dev/null; then
  echo "✓ 前端已啟動"
else
  echo "✗ 前端啟動失敗"
  cat /tmp/app.log | tail -30
  exit 1
fi

echo ""
echo "✓✓✓ 系統已完全啟動 ✓✓✓"
echo ""
echo "📍 登入網址: https://localhost:7199/login"
echo "👤 帳號: admin"
echo "🔑 密碼: Admin@123"
echo "🔐 驗證碼: 請依登入頁圖片輸入"
echo ""
echo "後端進程 PID: $BACKEND_PID"
echo "前端進程 PID: $APP_PID"
echo ""
echo "按 Ctrl+C 停止系統"

# 保持運行
wait
