# 間調 (Matome) - 會議室及設備借用系統 (Meeting Room & Equipment Booking System)

基於 **ASP.NET 4.8 Web Forms (C#)** 與 **PostgreSQL** 的會議室與設備預約審核借用系統，原生支援 **Railway** 容器化雲端部署與 **GitHub** 協作。
前端設計嚴格依據 **日式簡約極簡風 (Japanese Minimalism / MUJI Style)** 規範，整合 **FullCalendar v6 (30分鐘時段刻度)**、jQuery 與 SweetAlert2，後端落實三層式架構與交易並行衝突防護機制。

---

## 🌟 核心功能特色

1. **週行事曆時段總覽 (`Default.aspx`)**：
   - 專注於「會議場地」借用。
   - 採用 FullCalendar v6 TimeGridWeek 視圖，每格精確至 30 分鐘（06:00 ~ 22:00）。
   - 視覺化狀態區分：可借用（櫻雪白）、已核准（抹茶綠）、審核中（杏黃斜紋紋理）、我的預約（栗茶色）。
   - 支援空白格點擊/拖曳直接喚起右側預約抽屜（Booking Drawer）。
   - **每週同一時段定期會議功能 (跨週區間預約)**：勾選「🔁 設定為每週定期會議」並選取迄止日期，系統即時計算每週該星期與時段之總場次數；在交易內以批次隔離核驗全系列場次之排程衝突，任一週次衝突立即主動警示並安全回滾。

2. **設備獨立借用預約 (`EquipmentBooking.aspx`)**：
   - 與會議場地徹底解耦獨立，同仁可單獨申請借用各式設備（麥克風、視訊主機、雷射筆、白板等）。
   - 依所選設備呈現專屬週排程，點擊時段即時計算「該時段剩餘可用配額」。
   - 支援填寫借用數量（不可超過該時段剩餘可用上限）與借用專案事由。

3. **強固的並行防衝突機制 (Transaction & Concurrency Lock)**：
   - 採用 `IsolationLevel.RepeatableRead` 與原子性資料庫驗證，徹底防範高並行時段重疊搶借。
   - 支援審核型會議室與免審核即定會議室兩種模式。

4. **審核工作台 (`Approval/PendingList.aspx`)**：
   - 依審核人員所屬單位（`DeptId`）與角色權限自動過濾待審核清單。
   - 智慧衝突檢測標籤（自動警示多筆申請重疊搶借之預約案）。
   - 核准時二次驗證時段衝突防呆；駁回時強制輸入駁回理由並寫入紀錄。

5. **個人預約中心 (`MyBookings.aspx`)**：
   - 標籤分類：全部紀錄、即將到來、歷史紀錄。
   - 提供防呆之「取消預約」機制，取消後同步自動釋放會議室與設備庫存。

6. **管理控制台 (`Admin/`)**：
   - 會議室場地維護 (`RoomManage.aspx`)：名稱、容納人數、樓層、是否需審核、指定管轄部門、啟用/停用。
   - 設備資產管理 (`EquipmentManage.aspx`)：維護總配額（Quota）與可用狀態。
   - 人員帳號權限 (`UserManage.aspx`)：維護帳號、所屬單位、角色（Admin / Approver / User）。

---

## 🏗️ 專案架構 (Three-Tier Architecture)

```
Matome/
├── App_Code/
│   ├── Models/
│   │   └── Models.cs                    # 資料實體與 DTO (User, Room, Equipment, Booking, CalendarEventDto)
│   ├── DAL/
│   │   └── SqlHelper.cs                 # Npgsql PostgreSQL 資料庫連線、環境變數 (DATABASE_URL) 解析與交易控制
│   └── BLL/
│       ├── AuthHelper.cs                # 身分認證、SHA256 雜湊與 Session 管理
│       ├── BookingManager.cs            # 30分鐘檢核、衝突驗證、設備可用運算、預約建立與取消
│       ├── RoomManager.cs               # 會議室維護與規則管理
│       ├── EquipmentManager.cs          # 設備資產庫存與動態運算
│       ├── ApprovalManager.cs           # 審核路由、衝突警示、核准與駁回流程
│       └── UserManager.cs               # 使用者與部門權限管理
├── Admin/
│   ├── RoomManage.aspx (.cs, .designer.cs)       # 會議室管理
│   ├── EquipmentManage.aspx (.cs, .designer.cs)  # 設備庫存管理
│   └── UserManage.aspx (.cs, .designer.cs)       # 人員權限管理
├── Approval/
│   └── PendingList.aspx (.cs, .designer.cs)      # 審核工作台
├── Services/
│   └── BookingService.ashx (.cs)                 # 非同步 AJAX JSON 端點 (Events, Equipments, CRUD)
├── Content/
│   └── site.css                                  # 日式簡約極簡配色與元件樣式庫
├── Database/
│   ├── SetupDatabase_postgres.sql                # PostgreSQL 資料庫結構、外鍵約束與種子資料
│   └── SetupDatabase.sql                         # MS SQL 歷史備份檔
├── Dockerfile                                    # Railway / Linux 容器建置檔 (基於 Mono + XSP4)
├── entrypoint.sh                                 # 容器啟動腳本 (動態綁定 $PORT)
├── railway.json                                  # Railway 部署配置
├── Default.aspx (.cs, .designer.cs)              # 週行事曆與預約抽屜
├── MyBookings.aspx (.cs, .designer.cs)           # 我的預約清單與取消
├── Login.aspx (.cs, .designer.cs)                # 登入介面與一鍵身分切換
├── Site.Master (.cs, .designer.cs)               # 系統主版頁面與導覽列
├── Web.config                                    # 連線字串與 ASP.NET 4.8 伺服器設定
└── Matome.csproj / Matome.sln                   # Visual Studio 專案與方案檔
```

---

## 🎨 日式簡約極簡風格規範 (MUJI & Wabi-Sabi Palette)

- **基底色 (Base)**：`#F9F8F6`（櫻雪白 / 生成色，自然無眩光）
- **容器色 (Surface)**：`#FFFFFF`（純白卡片，細微 1px 邊框）
- **主要文字 (Text Primary)**：`#2B2B2B`（墨黑 Sumi）
- **次要文字 (Text Secondary)**：`#70706E`（鳩羽灰 Hato）
- **核心強調色 (Brand Primary)**：`#6B4E3D`（栗茶色 Kuricha）
- **審核核准/可用 (Matcha)**：`#5A6B5C`（抹茶綠）
- **待審核狀態 (Pending)**：`#C29B38`（杏黃 / 枯草）
- **駁回/取消 (Warning)**：`#B85D56`（洗柿紅 Araigaki）
- **極細邊框線 (Border)**：`#E7E5E0`（薄鈍 Usunibi）

---

## 🗄️ PostgreSQL 資料庫建置

系統已全面升級為 PostgreSQL，相關 SQL 指令檔位於 `Database/SetupDatabase_postgres.sql`。

### 初始預設測試帳號
| 帳號 | 預設密碼 | 角色 | 姓名 / 單位 |
| :--- | :--- | :--- | :--- |
| `admin` | `admin123` | 最高管理員 (Admin) | 系統管理員 (佐藤一朗) / 資訊技術處 |
| `approver_ga` | `pass123` | 審核主管 (Approver) | 總務主管 (高橋健二) / 行政總務課 |
| `approver_it` | `pass123` | 審核主管 (Approver) | 資訊主管 (渡邊直樹) / 資訊技術處 |
| `user_chen` | `pass123` | 一般使用者 (User) | 陳小明 (行銷專員) / 業務行銷部 |
| `user_lin` | `pass123` | 一般使用者 (User) | 林雅晴 (人資管理師) / 人力資源處 |

---

## 🚂 Railway 平台部署指南

### 步驟 1：建立 Railway 專案並加入 PostgreSQL
1. 登入 [Railway 控制台](https://railway.app/)。
2. 點擊 **「+ New Project」** -> 選擇 **「Provision PostgreSQL」**。
3. 建立完成後，點擊該 PostgreSQL 服務，進入 **「Data」** 或 **「Query」** 分頁。
4. 將本專案中的 [`Database/SetupDatabase_postgres.sql`](Database/SetupDatabase_postgres.sql) 完整內容貼上並執行，建立資料庫表格與種子資料。

### 步驟 2：部署 Web 應用程式
1. 在同一個 Railway 專案內，點擊 **「+ New」** -> 選擇 **「GitHub Repo」**。
2. 選擇已推送本專案的 GitHub 儲存庫。
3. Railway 會自動偵測到根目錄下的 `Dockerfile` 與 `railway.json`，並啟動建置。

### 步驟 3：綁定資料庫連線變數
1. 點擊進入剛建立的 Web 服務 -> 切換至 **「Variables」** 分頁。
2. 點擊 **「Add Reference Variable」**，選擇 PostgreSQL 提供的 `DATABASE_URL`。
3. 系統的 `SqlHelper` 會自動讀取 `DATABASE_URL` 並自動轉為 Npgsql 連線字串，無須手動設定！
4. 切換至 **「Settings」** 分頁 -> 在 **「Networking」** 點擊 **「Generate Domain」**，即可取得專屬公開網址並開始使用！

---

## 🐙 GitHub 平台推送指引

若您要將此專案推送至全新的 GitHub 儲存庫，請依序執行以下指令：

```powershell
# 1. 初始化本地 Git 儲存庫
git init

# 2. 將所有檔案加入暫存區
git add .

# 3. 提交變更
git commit -m "feat: 升級資料庫為 PostgreSQL 並加入 Railway 與 GitHub 部署設定"

# 4. 建立 main 分支
git branch -M main

# 5. 關聯遠端 GitHub 儲存庫 (請替換為您的實際 GitHub Repo URL)
git remote add origin https://github.com/<您的使用者名稱>/<您的儲存庫名稱>.git

# 6. 推送至 GitHub
git push -u origin main
```
