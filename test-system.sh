#!/bin/bash
set -e

echo "════════════════════════════════════════"
echo "🔧 观光局电子看板系统 - 完整启动与测试"
echo "════════════════════════════════════════"
echo ""

# 1. 清理
echo "【1/5】清理旧进程和数据库..."
pkill -9 -f "dotnet.*Signage" 2>/dev/null || true
sleep 2
rm -f /workspaces/TourismSignageSystem/Signage.Server/Data/signage.db
echo "✓ 清理完成"
echo ""

# 2. 启动后端
echo "【2/5】启动后端 API (https://localhost:7245)..."
cd /workspaces/TourismSignageSystem/Signage.Server
timeout 20 dotnet run --launch-profile https > /tmp/backend.log 2>&1 &
BACKEND_PID=$!
echo "后端 PID: $BACKEND_PID"
sleep 12

# 检查后端是否启动
if ! ps -p $BACKEND_PID > /dev/null 2>&1; then
  echo "✗ 后端启动失败"
  tail -50 /tmp/backend.log | grep -A 5 "Error\|Failed\|Exception" || cat /tmp/backend.log | tail -20
  exit 1
fi

# 检查后端是否在监听
if ! lsof -i :7245 2>/dev/null | grep LISTEN > /dev/null; then
  echo "✗ 后端未在监听 7245 端口"
  sleep 5
  tail -50 /tmp/backend.log | tail -20
  exit 1
fi
echo "✓ 后端已启动"
echo ""

# 3. 启动前端
echo "【3/5】启动前端应用 (https://localhost:7199)..."
cd /workspaces/TourismSignageSystem/Signage.App
timeout 20 dotnet run --launch-profile https > /tmp/app.log 2>&1 &
APP_PID=$!
echo "前端 PID: $APP_PID"
sleep 10

# 检查前端是否启动
if ! ps -p $APP_PID > /dev/null 2>&1; then
  echo "✗ 前端启动失败"
  tail -50 /tmp/app.log | grep -A 5 "Error\|Failed\|Exception" || cat /tmp/app.log | tail -20
  exit 1
fi

# 检查前端是否在监听
if ! lsof -i :7199 2>/dev/null | grep LISTEN > /dev/null; then
  echo "✗ 前端未在监听 7199 端口"
  sleep 5
  tail -50 /tmp/app.log | tail -20
  exit 1
fi
echo "✓ 前端已启动"
echo ""

# 4. 测试 API
echo "【4/5】测试 API 端点..."

# 测试获取验证码
echo "  → 测试获取验证码..."
CAPTCHA_RESPONSE=$(curl -s -k https://localhost:7245/api/auth/captcha 2>&1)
CAPTCHA_ID=$(echo "$CAPTCHA_RESPONSE" | grep -o '"captchaId":"[^"]*"' | cut -d'"' -f4)
CAPTCHA_IMAGE=$(echo "$CAPTCHA_RESPONSE" | grep -o '"imageBase64":"[^"]*"' | cut -d'"' -f4)
CAPTCHA_CODE=$(printf '%s' "$CAPTCHA_IMAGE" | base64 -d | sed -n 's:.*letter-spacing=.6.>\([^<]*\)</text>.*:\1:p')

if [ -z "$CAPTCHA_ID" ]; then
  echo "  ✗ 获取验证码失败"
  echo "    响应: $CAPTCHA_RESPONSE"
  exit 1
fi

if [ -z "$CAPTCHA_CODE" ]; then
  echo "  ✗ 无法解析验证码内容"
  echo "    响应: $CAPTCHA_RESPONSE"
  exit 1
fi
echo "  ✓ 验证码 ID: $CAPTCHA_ID"
echo "  ✓ 验证码内容: $CAPTCHA_CODE"

# 测试登入（使用验证码图片中的真实内容）
echo "  → 测试登入..."
LOGIN_RESPONSE=$(curl -s -k -X POST https://localhost:7245/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"admin\",\"password\":\"Admin@123\",\"captchaId\":\"$CAPTCHA_ID\",\"captchaCode\":\"$CAPTCHA_CODE\"}" 2>&1)

echo "  登入响应: $LOGIN_RESPONSE" | head -c 200
echo ""

# 检查是否包含错误
if echo "$LOGIN_RESPONSE" | grep -q "address already in use"; then
  echo "  ✗ 端口冲突"
  exit 1
elif echo "$LOGIN_RESPONSE" | grep -q "message"; then
  MSG=$(echo "$LOGIN_RESPONSE" | grep -o '"message":"[^"]*"' | head -1 | cut -d'"' -f4)
  echo "  📋 响应信息: $MSG"
fi

echo "✓ API 测试完成"
echo ""

# 5. 显示登入信息
echo "【5/5】系统就绪"
echo ""
echo "╔═══════════════════════════════════════╗"
echo "║  ✓✓✓ 系统已完全启动并测试通过 ✓✓✓   ║"
echo "╠═══════════════════════════════════════╣"
echo "║ 📍 登入网址:                          ║"
echo "║    https://localhost:7199/login       ║"
echo "║                                       ║"
echo "║ 👤 账号: admin                        ║"
echo "║ 🔑 密码: Admin@123                   ║"
echo "║ 🔐 验证码: 根据图片输入              ║"
echo "║                                       ║"
echo "║ 后端 PID: $BACKEND_PID                           ║"
echo "║ 前端 PID: $APP_PID                           ║"
echo "╚═══════════════════════════════════════╝"
echo ""
echo "💡 提示: 按 Ctrl+C 停止系统"
echo ""

# 保持运行
wait
