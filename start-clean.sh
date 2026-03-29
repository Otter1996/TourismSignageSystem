#!/bin/bash

echo "🔥 激進端口清理中..."

# 殺除所有 dotnet 進程
pkill -9 -f dotnet 2>/dev/null || true

# 使用 fuser 強制釋放端口
sudo fuser -k 7245/tcp 2>/dev/null || fuser -k 7245/tcp 2>/dev/null || true
sudo fuser -k 7199/tcp 2>/dev/null || fuser -k 7199/tcp 2>/dev/null || true

# 等待端口釋放
sleep 3

# 驗證端口是否空閒
if ! lsof -i :7245 2>/dev/null | grep LISTEN > /dev/null; then
  echo "✓ 端口 7245 已釋放"
else
  echo "⚠️  端口 7245 仍被占用，嘗試通過其他方式..."
  # 最後手段：找出所有監聽該端口的進程並殺死
  for pid in $(lsof -ti :7245 2>/dev/null); do
    echo "殺除 PID $pid..."
    kill -9 $pid 2>/dev/null || sudo kill -9 $pid 2>/dev/null || true
  done
  sleep 2
fi

# 清理數據庫
echo "清理舊數據庫..."
rm -f /workspaces/TourismSignageSystem/Signage.Server/Data/signage.db

# 啟動後端
echo "🚀 啟動後端 API..."
cd /workspaces/TourismSignageSystem/Signage.Server
dotnet run --launch-profile https 2>&1 &
BACKEND_PID=$!
echo "後端 PID: $BACKEND_PID"

sleep 15

# 驗證後端是否在監聽
if lsof -i :7245 2>/dev/null | grep LISTEN > /dev/null; then
  echo "✓ 後端已就緒 (https://localhost:7245)"
  
  # 啟動前端
  echo ""
  echo "🚀 啟動前端應用..."
  cd /workspaces/TourismSignageSystem/Signage.App
  dotnet run --launch-profile https 2>&1 &
  APP_PID=$!
  echo "前端 PID: $APP_PID"
  
  sleep 10
  
  if lsof -i :7199 2>/dev/null | grep LISTEN > /dev/null; then
    echo "✓ 前端已就緒 (https://localhost:7199)"
    
    echo ""
    echo "╔═════════════════════════════════════╗"
    echo "║  ✓✓✓ 系統已完全啟動 ✓✓✓           ║"
    echo "╠═════════════════════════════════════╣"
    echo "║ 📍 登入網址:                        ║"
    echo "║    https://localhost:7199/login     ║"
    echo "║                                     ║"
    echo "║ 👤 帳號: admin                      ║"
    echo "║ 🔑 密碼: Admin@123                 ║"
    echo "║ 🔐 驗證碼: 依登入頁圖片輸入         ║"
    echo "╚═════════════════════════════════════╝"
    
    # 持續運行
    echo ""
    echo "系統正在運行中... 按 Ctrl+C 停止"
    wait
  else
    echo "✗ 前端啟動失敗"
    exit 1
  fi
else
  echo "✗ 後端啟動失敗"
  exit 1
fi
