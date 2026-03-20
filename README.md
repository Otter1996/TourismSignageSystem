# 觀光局電子看板多媒體管理系統

## 📺 系統介紹

本系統提供了一套完整的電子看板管理解決方案，讓觀光機構能夠遠端管理分佈於各地的看板設備，支持多種版型、實時推送、和設備監控。

### 核心功能
- 🎨 **多版型支持** - 三段式、L型、全螢幕
- 📱 **遠端管理** - 一站式管理後台
- 🎬 **媒體管理** - 圖片、影片、文字素材
- 📊 **設備監控** - 實時狀態追蹤
- 🎯 **實時更新** - 即時推送配置

### 📘 文件導覽
- [中文操作手冊](操作手冊.md)

---

## 🚀 快速開始

### 前置需求
- **.NET 10.0 SDK** 或更新版本
- **Visual Studio 2022** 或 **VS Code** + C# 擴展
- **Windows / macOS / Linux** 環境

### 安裝步驟

#### 1️⃣ 克隆或下載專案
```bash
git clone https://github.com/Otter1996/TourismSignageSystem.git
cd TourismSignageSystem
```

#### 2️⃣ 啟動後端 API 服務
```bash
cd Signage.Server
dotnet restore
dotnet run --launch-profile https
```

**預期輸出：**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
      Now listening on: http://localhost:5000
```

#### 3️⃣ 新開一個終端，啟動前端應用
```bash
cd ../Signage.App
dotnet restore
dotnet run --launch-profile https
```

**預期輸出：**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5002
      Now listening on: http://localhost:5000
```

#### 4️⃣ 打開瀏覽器
- **管理後台**: https://localhost:5002
- **看板播放端**: https://localhost:5002/display

---

## 📖 使用指南

### 管理後台功能

#### 🏢 遊客中心管理
1. 在左側邊欄查看已註冊的遊客中心
2. 點擊中心名稱選擇
3. 使用介面底部新增新中心
4. 點擊編輯圖示修改中心資訊
5. 點擊刪除圖示移除中心

#### 🎨 版型編輯與預覽
1. 在主頁面左側選擇或新建版型
2. 從下拉菜單選擇版型類型：
   - **三段式** - 適合多內容展示
   - **L型** - 適合特色資訊展示
   - **全螢幕** - 適合宣傳影片
3. 編輯各區域內容：
   - 跑馬燈文字
   - 影片 URL
   - 圖片 URL
4. 右側預覽區實時顯示效果
5. 點擊「儲存設定」保存
6. 點擊「推送至看板」同步至設備

#### 📁 媒體庫管理
1. 在菜單中選擇「媒體庫」
2. 選擇媒體類型（圖片/影片/文字）
3. 輸入媒體名稱和 URL
4. 設置播放時長（秒）
5. 點擊「新增素材」上傳
6. 媒體將出現在右側庫中
7. 可預覽、複製 URL、或刪除

#### 🎮 設備管理
1. 在菜單中選擇「設備管理」
2. 在左側輸入設備名稱和 IP 地址
3. 點擊「註冊設備」
4. 右側將顯示設備列表和狀態
5. 綠色 ● 表示在線，紅色 ● 表示離線
6. 可點擊「推送配置」或「移除」

### 看板播放端

#### 訪問播放端
- 瀏覽器打開: `https://localhost:5002/display`
- 選擇要播放的版型
- 實時看到播放效果

#### 版型展示效果

**三段式版型 (30/40/30)**
```
┌────────────────────────────┐
│  ★ 歡迎光臨 ★ 跑馬燈 →   │ 30%
├────────────────────────────┤
│                            │
│   影片播放區域（循環）      │ 40%
│                            │
├────────────────────────────┤
│        圖片展示區域          │ 30%
└────────────────────────────┘
```

**L型版型**
```
┌──────────┬──────────────┐
│ 景點資訊  │   影片播放   │
│          │              │
│ • 景點1  │  (主要內容)  │
│ • 景點2  │              │
│ • 景點3  │              │
└──────────┴──────────────┘
```

**全螢幕版型**
```
┌──────────────────────────────┐
│                              │
│  ★ 跑馬燈 →                 │
│                              │
│   背景影片 (全屏、循環)       │
│                              │
│                              │
└──────────────────────────────┘
```

---

## 🔌 API 使用示例

### 獲取所有遊客中心
```bash
curl -X GET https://localhost:5001/api/visitorcenters
```

### 建立新遊客中心
```bash
curl -X POST https://localhost:5001/api/visitorcenters \
  -H "Content-Type: application/json" \
  -d '{
    "name": "新中心",
    "location": "台北市"
  }'
```

### 推送配置至設備
```bash
curl -X POST https://localhost:5001/api/commands/push \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId": "device-123",
    "pageConfig": {
      "layout": "TripleVertical",
      "topContent": "★ 歡迎 ★",
      "middleContentUrl": "https://example.com/video.mp4",
      "bottomContentUrl": "https://example.com/image.jpg"
    }
  }'
```

### 註冊看板設備
```bash
curl -X POST https://localhost:5001/api/devices/register \
  -H "Content-Type: application/json" \
  -d '{
    "centerId": "center-123",
    "deviceName": "看板-01",
    "ipAddress": "192.168.1.10"
  }'
```

### 設備心跳更新
```bash
curl -X POST https://localhost:5001/api/devices/device-123/heartbeat
```

---

## 📸 截圖示例

### 管理後台首頁
```
┌─────────────────────────────────────────────┐
│  觀光局電子看板管理系統               v1.0  │
├──────────────┬──────────────────────────────┤
│ 遊客中心清單 │                              │
│              │  版型配置管理                │
│ • 左營       │  ┌─────────────────────────┐ │
│ • 旗津       │  │ 三段式版型              │ │
│ • 新北       │  │ 跑馬燈: ............... │ │
│              │  │ 影片: https://...       │ │
│ + 新增       │  │ 圖片: https://...       │ │
│              │  │                         │ │
│              │  │ [儲存] [推送]          │ │
│              │  └─────────────────────────┘ │
│              │  ┌─────────────────────────┐ │
│              │  │ 終端畫面模擬            │ │
│              │  │ ★ 歡迎光臨 ★           │ │
│              │  │ [播放影片]              │ │
│              │  │ [圖片區域]              │ │
│              │  └─────────────────────────┘ │
└──────────────┴──────────────────────────────┘
```

---

## 🛠️ 常見問題 (FAQ)

### Q1: 無法連接到後端 API？
**A:** 確保：
- 後端服務已啟動（應在 `https://localhost:5001` 運行）
- 前端的 `appsettings.json` 中的 API URL 正確
- 防火牆未阻止 localhost 連接

### Q2: 看板端無法播放影片？
**A:** 檢查：
- 影片 URL 是否有效（可在瀏覽器中直接打開）
- 影片格式是否為 MP4/WebM
- 有無跨域 CORS 問題

### Q3: 設備狀態始終顯示離線？
**A:** 
- 在開發環境中，設備狀態是模擬的
- 實際部署時需配置設備端應用發送心跳信號
- 可透過 `/api/devices/{deviceId}/heartbeat` 手動更新

### Q4: 如何修改推送的媒體 URL？
**A:**
1. 在「媒體庫」頁面新增或修改媒體
2. 在「版型配置」中引用新的媒體 URL
3. 點擊「推送至看板」同步

### Q5: 支援多少個看板設備？
**A:**
- 目前使用內存存儲，理論上無限制
- 實際部署后需根據資料庫性能調整
- 建議進行負載測試

---

## 🔐 安全建議

### 開發環境
- 使用自簽名 HTTPS 證書
- 禁用 CORS 限制只在本地開發中使用

### 生產環境
- [ ] 啟用用戶認証 (JWT/OAuth2)
- [ ] 配置 HTTPS 證書
- [ ] 設置 CORS 白名單
- [ ] 實現 API 速率限制
- [ ] 使用資料庫代替內存存儲
- [ ] 啟用輸入驗證與消毒
- [ ] 配置 Web 應用防火牆
- [ ] 定期安全審計

---

## 📚 項目結構

```
TourismSignageSystem/
├── Signage.App/                    # 前端應用
│   ├── Components/
│   │   ├── Pages/
│   │   │   ├── Home.razor         # 版型編輯
│   │   │   ├── Media.razor        # 媒體管理
│   │   │   ├── Devices.razor      # 設備管理
│   │   │   ├── Display.razor      # 看板播放端
│   │   │   └── ...
│   │   └── Layout/
│   │       └── MainLayout.razor
│   ├── Services/
│   │   └── SignageApiClient.cs    # API 客戶端
│   ├── Program.cs
│   ├── appsettings.json
│   └── wwwroot/
│
├── Signage.Server/                 # 後端 API
│   ├── Controllers/
│   │   ├── VisitorCentersController.cs
│   │   ├── SignagePagesController.cs
│   │   ├── MediaController.cs
│   │   ├── DevicesController.cs
│   │   └── CommandsController.cs
│   ├── Services/
│   │   └── SignageDataService.cs  # 數據服務
│   ├── Program.cs
│   └── appsettings.json
│
├── Signage.Common/                 # 共享庫
│   └── Model.cs                   # 數據模型
│
└── IMPLEMENTATION_REPORT.md        # 實現報告
```

---

## 🔄 開發工作流

### 添加新的版型
1. 在 `Model.cs` 中的 `LayoutType` 枚舉新增類型
2. 在 `Home.razor` 中新增版型渲染邏輯
3. 在 `Display.razor` 中新增對應的播放邏輯
4. 測試版型效果

### 添加新的 API 端點
1. 在 `SignageDataService.cs` 中實現業務邏輯
2. 在 `Controllers` 中建立新的控制器或方法
3. 在 `SignageApiClient.cs` 中新增客戶端方法
4. 在前端組件中呼叫新的 API

### 連接資料庫
1. 安裝 Entity Framework Core
2. 建立 DbContext 類
3. 設置連接字符串
4. 執行遷移建立表
5. 使用 Repository 模式替換 SignageDataService

---

## 🆘 故障排查

### 構建失敗
```bash
# 清理
dotnet clean

# 恢復依賴
dotnet restore

# 重新構建
dotnet build
```

### 遷移問題
```bash
# 檢查項目依賴
dotnet list package

# 更新所有包
dotnet package update
```

### 運行時錯誤
- 查看 Visual Studio 的「輸出」窗口
- 檢查瀏覽器的開發者工具控制台
- 查看應用日誌文件

---

## 📝 許可證

MIT License

---

## 👨‍💻 作者

開發團隊: Otter1996

---

## 🙏 致謝

感謝所有為該項目做出貢獻的開發者和測試人員。

---

**最後更新**: 2026-03-19  
**版本**: 1.0.0  
**狀態**: ✅ 可用

