# 會議室及設備借用系統 (ASP.NET C# Web Forms) 系統開發規格書

**專案環境**：.NET Framework 4.8 / ASP.NET Web Forms (C#)  
**資料庫**：Microsoft SQL Server 2016+  
**前端整合**：FullCalendar v6 (TimeGridWeek 30m slots), jQuery 3.6+, SweetAlert2, 日式簡約 CSS  
**架構模式**：三層式架構 (Presentation Layer -> BLL -> DAL / ADO.NET or Dapper)

---

## 1. 系統整體架構 (System Architecture)

系統採用三層式架構，解耦前端展示、業務邏輯與資料存取，確保未來易於維護與擴充：

```
┌────────────────────────────────────────────────────────┐
│                   展示層 (UI Layer)                     │
│  - ASPX 頁面 (Default.aspx, MyBooking.aspx, Admin.aspx)│
│  - FullCalendar.js (30 分鐘週視圖)                      │
│  - [WebMethod] / ASHX (非同步 AJAX JSON 資料交換)        │
└───────────────────────────┬────────────────────────────┘
                            │ (DTO / View Models)
┌───────────────────────────▼────────────────────────────┐
│                 商業邏輯層 (BLL Layer)                  │
│  - BookingManager (時段防衝突、設備庫存動態運算)       │
│  - RoomManager (場地 CRUD、審核路由與權限檢核)          │
│  - EquipmentManager (設備庫存檢核)                     │
│  - ApprovalWorkflow (核准、駁回、自動取消連動)          │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                 資料存取層 (DAL Layer)                  │
│  - SqlHelper / Dapper (參數化查詢避免 SQL Injection)   │
│  - 事務交易控制 (SqlTransaction)                       │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                    MS SQL Server 2016+                 │
│  - 表結構、索引、唯一約束、外鍵關聯                      │
└────────────────────────────────────────────────────────┘
```

---

## 2. 資料庫結構設計 (Database Schema)

資料庫採用標準正規化設計，針對「防重疊預約」及「設備扣減」提供完整的關聯與交易保障。

### 2.1 會議室主表 (`Rooms`)
| 欄位名稱 | 型態 | 允許空值 | 說明 |
| :--- | :--- | :--- | :--- |
| `RoomId` | `INT IDENTITY(1,1)` | 否 (PK) | 會議室流水號 |
| `RoomName` | `NVARCHAR(100)` | 否 | 會議室名稱 (如：和風階梯演講廳) |
| `Capacity` | `INT` | 否 | 容納人數 |
| `Location` | `NVARCHAR(100)` | 是 | 所在位置/樓層 (如：A棟 3F) |
| `IsApprovalRequired` | `BIT` | 否 | 是否需審核 (1: 是, 0: 免審核) |
| `ApprovalDeptId` | `INT` | 是 (FK) | 指定審核單位流水號 (可為 NULL，代表不指定/全域審核) |
| `IsActive` | `BIT` | 否 | 是否啟用 (預設 1) |
| `CreateTime` | `DATETIME` | 否 | 建立時間 (DEFAULT GETDATE()) |

### 2.2 審核單位與人員表 (`Departments`, `Users`)
```sql
-- 單位/部門表
CREATE TABLE Departments (
    DeptId INT IDENTITY(1,1) PRIMARY KEY,
    DeptName NVARCHAR(50) NOT NULL
);

-- 使用者與審核人員表
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Account NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    FullName NVARCHAR(50) NOT NULL,
    DeptId INT NULL FOREIGN KEY REFERENCES Departments(DeptId),
    Email NVARCHAR(100) NOT NULL,
    Role NVARCHAR(20) NOT NULL, -- 'Admin', 'Approver', 'User'
    IsActive BIT NOT NULL DEFAULT 1
);
```

### 2.3 設備資產表 (`Equipments`)
| 欄位名稱 | 型態 | 允許空值 | 說明 |
| :--- | :--- | :--- | :--- |
| `EquipmentId` | `INT IDENTITY(1,1)` | 否 (PK) | 設備代碼 |
| `EquipmentName`| `NVARCHAR(100)` | 否 | 設備名稱 (如：無線麥克風、視訊主機) |
| `TotalQuantity`| `INT` | 否 | 總庫存數量 |
| `IsActive` | `BIT` | 否 | 是否可用 (預設 1) |

### 2.4 會議室預約主表 (`Bookings`)
| 欄位名稱 | 型態 | 允許空值 | 說明 |
| :--- | :--- | :--- | :--- |
| `BookingId` | `INT IDENTITY(1,1)` | 否 (PK) | 預約編號 |
| `RoomId` | `INT` | 否 (FK) | 借用會議室 |
| `UserId` | `INT` | 否 (FK) | 申請人 |
| `Subject` | `NVARCHAR(150)` | 否 | 會議事由 / 主題 |
| `StartTime` | `DATETIME` | 否 | 會議開始時間 (必須為 00 或 30 分) |
| `EndTime` | `DATETIME` | 否 | 會議結束時間 (必須為 00 或 30 分) |
| `Status` | `NVARCHAR(20)` | 否 | 狀態：`Pending`(審核中), `Approved`(已通過), `Rejected`(已駁回), `Cancelled`(已取消) |
| `ApprovedBy` | `INT` | 是 (FK) | 審核人 UserId |
| `ApprovedTime` | `DATETIME` | 是 | 審核時間 |
| `RejectReason` | `NVARCHAR(250)` | 是 | 駁回理由 |
| `CreateTime` | `DATETIME` | 否 | 預約申請送出時間 |

### 2.5 借用附加設備明細表 (`BookingEquipments`)
| 欄位名稱 | 型態 | 允許空值 | 說明 |
| :--- | :--- | :--- | :--- |
| `DetailId` | `INT IDENTITY(1,1)` | 否 (PK) | 流水編號 |
| `BookingId` | `INT` | 否 (FK) | 關聯預約編號 (CASCADE DELETE) |
| `EquipmentId` | `INT` | 否 (FK) | 設備代碼 |
| `Quantity` | `INT` | 否 | 借用數量 (預設 1) |

---

## 3. 核心商業邏輯與演算法設計

### 3.1 時段重疊防衝突演算法 (Room Overlap Check)
判斷申請時段 $[Start_{A}, End_{A}]$ 與現有時段 $[Start_{B}, End_{B}]$ 是否衝突的數學邏輯：
$$\text{IsOverlap} = (Start_{A} < End_{B}) \land (End_{A} > Start_{B})$$

在 SQL 中檢核指定會議室是否已被佔用（排除已被駁回或取消者）：
```sql
SELECT COUNT(1) 
FROM Bookings WITH (UPDLOCK, HOLDLOCK)
WHERE RoomId = @RoomId
  AND Status IN ('Approved', 'Pending') -- 審核中與已通過者皆視為佔用
  AND (@BookingId IS NULL OR BookingId <> @BookingId)
  AND (StartTime < @EndTime AND EndTime > @StartTime);
```
*備註：使用 `UPDLOCK, HOLDLOCK` 避免在高並行（Concurrency）搶借時發生 Race Condition。*

### 3.2 設備庫存重疊計算演算法 (Equipment Availability Check)
設備非固定綁定於單一會議室，多個會議室可能同時預約同一種設備（例如：公司共有 4 支麥克風）。在某個時段 $[Start, End]$，該設備已被佔用的最大數量計算方式如下：

```sql
SELECT 
    e.EquipmentId,
    e.EquipmentName,
    e.TotalQuantity,
    ISNULL(SUM(be.Quantity), 0) AS UsedQuantity,
    (e.TotalQuantity - ISNULL(SUM(be.Quantity), 0)) AS AvailableQuantity
FROM Equipments e
LEFT JOIN BookingEquipments be ON e.EquipmentId = be.EquipmentId
LEFT JOIN Bookings b ON be.BookingId = b.BookingId 
    AND b.Status IN ('Approved', 'Pending')
    AND (b.StartTime < @TargetEndTime AND b.EndTime > @TargetStartTime)
WHERE e.IsActive = 1
GROUP BY e.EquipmentId, e.EquipmentName, e.TotalQuantity;
```

---

## 4. 模組功能與程式開發規格

### 4.1 管理後台 (Admin Console)

#### 模組 1：會議室與設備管理維護 (`Admin/RoomManage.aspx`, `Admin/EquipmentManage.aspx`)
* **會議室功能清單**：
  * 列出所有會議室，支援啟用/停用（非實體刪除，避免破壞預約歷史資料外鍵）。
  * 新增/編輯會議室表單：
    * `txtRoomName` (必填)
    * `txtCapacity` (正整數)
    * `chkIsApprovalRequired` (Checkbox，勾選時展開審核單位下拉選單)
    * `ddlApprovalDept` (資料來源：`Departments` 表，可選擇「不指定 (全體審核者皆可審)」)。
* **設備管理功能清單**：
  * 維護設備庫存：設備名稱、總可用量。

#### 模組 2：審核人員與權限管理 (`Admin/UserManage.aspx`)
* 維護審核者帳號，並指派所屬 `DeptId`。
* 權限判斷矩陣：
  * 若會議室 `IsApprovalRequired = 0`：預約送出時狀態直接給定 `Approved`。
  * 若會議室 `IsApprovalRequired = 1` 且 `ApprovalDeptId IS NULL`：所有 `Role = 'Approver'` 或 `'Admin'` 皆有權審核。
  * 若會議室 `IsApprovalRequired = 1` 且 `ApprovalDeptId = N`：只有該部門的審核人員能看到並進行審批。

---

### 4.2 審核模組 (Approval Module)

#### 審核工作台 (`Approval/PendingList.aspx`)
* **資料過濾 (Query Filter)**：
  根據登入使用者的 `DeptId` 與 `Role`，撈取符合其管轄範圍、狀態為 `Pending` 的所有預約。
* **操作介面**：
  * **[核准]**：
    * 再次執行時段衝突檢驗（防呆）。
    * 更新狀態為 `Approved`，寫入 `ApprovedBy = CurrentUserId` 與 `ApprovedTime = GETDATE()`。
    * 發送通知郵件至申請人信箱。
  * **[駁回]**：
    * 彈出 SweetAlert 輸入駁回理由（必填）。
    * 更新狀態為 `Rejected`，寫入 `RejectReason`。

---

### 4.3 一般使用者端模組 (User Booking Module)

#### 1. 週行事曆畫面 (`Default.aspx`)
* **前端整合 FullCalendar v6 (30分鐘刻度)**：
  * 視圖：`timeGridWeek`
  * 槽位間隔：`slotDuration: '00:30:00'`
  * 可選時間範圍：`slotMinTime: '08:00:00'`, `slotMaxTime: '19:00:00'`
  * 網格點擊：使用者在空白方格點擊或框選，直接獲取 `info.startStr` 與 `info.endStr`，並喚起右側預約 Drawer 抽屜。

#### 2. 非同步資料端點 (`Services/BookingService.ashx` 或 PageMethods)
* **端點 1：獲取週行事曆事件**
  * `GET /Services/BookingService.ashx?action=get_events&roomId=1&start=2026-10-26&end=2026-11-01`
  * 回傳格式：
    ```json
    [
      {
        "id": "101",
        "title": "行銷部週會 (陳O明)",
        "start": "2026-10-26T09:00:00",
        "end": "2026-10-26T10:30:00",
        "className": "status-approved",
        "extendedProps": {
          "status": "Approved",
          "isMine": true
        }
      }
    ]
    ```
* **端點 2：提交預約申請**
  * `POST /Services/BookingService.ashx?action=create_booking`
  * Payload：
    ```json
    {
      "roomId": 1,
      "subject": "產品發表會籌備",
      "startTime": "2026-10-27 14:00",
      "endTime": "2026-10-27 16:00",
      "equipments": [
        { "equipmentId": 2, "quantity": 1 },
        { "equipmentId": 3, "quantity": 2 }
      ]
    }
    ```
  * 後端邏輯：
    1. 驗證起始與結束時間需以 30 分鐘為單位（分需為 00 或 30）。
    2. 開啟 `SqlTransaction`。
    3. 執行衝突驗證（若已被佔用，拋出 "該時段已被預訂"）。
    4. 執行設備庫存驗證（若庫存不足，拋出 "指定設備在此時段庫存不足"）。
    5. 檢查該會議室是否需審核：若免審核則狀態為 `Approved`，需審核則為 `Pending`。
    6. 寫入 `Bookings` 與 `BookingEquipments`，Commit 交易。

#### 3. 我的預約與取消 (`MyBookings.aspx`)
* 列出登入者所有的預約紀錄。
* **取消預約規則 (Cancellation Guard)**：
  * 僅允許取消狀態為 `Pending` 或 `Approved` 的申請。
  * 會議起始時間已過者，不允許取消（按鈕反灰）。
  * 點擊「取消申請」後，系統更新狀態為 `Cancelled`，資料庫連帶自動釋出會議室與附加設備佔用額度。

---

## 5. C# 後端核心程式碼骨架範例

### 5.1 交易鎖定衝突檢核與建立預約 (BLL/BookingManager.cs)

```csharp
using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;

public class BookingManager
{
    private readonly string _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

    public bool CreateBooking(int userId, int roomId, string subject, DateTime startTime, DateTime endTime, List<EquipmentItem> equipments, out string errorMessage)
    {
        errorMessage = string.Empty;

        // 1. 驗證 30 分鐘單位
        if ((startTime.Minute != 0 && startTime.Minute != 30) || (endTime.Minute != 0 && endTime.Minute != 30) || startTime >= endTime)
        {
            errorMessage = "預約時段必須以 30 分鐘為單位且結束時間需大於開始時間。";
            return false;
        }

        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            using (SqlTransaction trans = conn.BeginTransaction(IsolationLevel.RepeatableRead))
            {
                try
                {
                    // 2. 檢驗會議室衝突 (使用 UPDLOCK 防競爭態)
                    string checkRoomSql = @"
                        SELECT COUNT(1) FROM Bookings WITH (UPDLOCK, HOLDLOCK)
                        WHERE RoomId = @RoomId 
                          AND Status IN ('Approved', 'Pending')
                          AND (StartTime < @EndTime AND EndTime > @StartTime);";

                    using (SqlCommand cmd = new SqlCommand(checkRoomSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@RoomId", roomId);
                        cmd.Parameters.AddWithValue("@StartTime", startTime);
                        cmd.Parameters.AddWithValue("@EndTime", endTime);

                        int conflictCount = (int)cmd.ExecuteScalar();
                        if (conflictCount > 0)
                        {
                            errorMessage = "很抱歉，該時段已被其他人員預約或正在審核中。";
                            trans.Rollback();
                            return false;
                        }
                    }

                    // 3. 取得會議室是否需要審核
                    string roomAttrSql = "SELECT IsApprovalRequired FROM Rooms WHERE RoomId = @RoomId;";
                    bool isApprovalRequired = false;
                    using (SqlCommand cmd = new SqlCommand(roomAttrSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@RoomId", roomId);
                        object result = cmd.ExecuteScalar();
                        if (result != null) isApprovalRequired = Convert.ToBoolean(result);
                    }

                    string initialStatus = isApprovalRequired ? "Pending" : "Approved";

                    // 4. 寫入主預約記錄
                    int bookingId = 0;
                    string insertBookingSql = @"
                        INSERT INTO Bookings (RoomId, UserId, Subject, StartTime, EndTime, Status, CreateTime)
                        VALUES (@RoomId, @UserId, @Subject, @StartTime, @EndTime, @Status, GETDATE());
                        SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(insertBookingSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@RoomId", roomId);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@Subject", subject);
                        cmd.Parameters.AddWithValue("@StartTime", startTime);
                        cmd.Parameters.AddWithValue("@EndTime", endTime);
                        cmd.Parameters.AddWithValue("@Status", initialStatus);

                        bookingId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 5. 寫入設備並檢查庫存
                    if (equipments != null && equipments.Count > 0)
                    {
                        foreach (var item in equipments)
                        {
                            // 檢查當前時段該設備的最大可用量
                            string checkEquipSql = @"
                                SELECT (e.TotalQuantity - ISNULL(SUM(be.Quantity), 0)) AS AvailableQty
                                FROM Equipments e
                                LEFT JOIN BookingEquipments be ON e.EquipmentId = be.EquipmentId
                                LEFT JOIN Bookings b ON be.BookingId = b.BookingId 
                                    AND b.Status IN ('Approved', 'Pending')
                                    AND (b.StartTime < @EndTime AND b.EndTime > @StartTime)
                                WHERE e.EquipmentId = @EquipmentId AND e.IsActive = 1
                                GROUP BY e.TotalQuantity;";

                            using (SqlCommand cmd = new SqlCommand(checkEquipSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@EquipmentId", item.EquipmentId);
                                cmd.Parameters.AddWithValue("@StartTime", startTime);
                                cmd.Parameters.AddWithValue("@EndTime", endTime);

                                object availObj = cmd.ExecuteScalar();
                                int available = availObj != null ? Convert.ToInt32(availObj) : 0;

                                if (available < item.Quantity)
                                {
                                    errorMessage = $"設備庫存不足，無法滿足需求。";
                                    trans.Rollback();
                                    return false;
                                }
                            }

                            // 扣減 (寫入明細)
                            string insertDetailSql = @"
                                INSERT INTO BookingEquipments (BookingId, EquipmentId, Quantity)
                                VALUES (@BookingId, @EquipmentId, @Quantity);";

                            using (SqlCommand cmd = new SqlCommand(insertDetailSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@BookingId", bookingId);
                                cmd.Parameters.AddWithValue("@EquipmentId", item.EquipmentId);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    trans.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    errorMessage = "系統發生未預期錯誤：" + ex.Message;
                    return false;
                }
            }
        }
    }
}

public class EquipmentItem
{
    public int EquipmentId { get; set; }
    public int Quantity { get; set; }
}
```

---

## 6. 前端週行事曆初始化範例 (JavaScript / FullCalendar)

```javascript
document.addEventListener('DOMContentLoaded', function () {
    var calendarEl = document.getElementById('weeklyCalendar');
    
    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'timeGridWeek',
        slotDuration: '00:30:00', // 30 分鐘一格
        slotMinTime: '08:00:00',
        slotMaxTime: '19:00:00',
        allDaySlot: false,
        locale: 'zh-tw',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: ''
        },
        selectable: true,
        selectMirror: true,
        // 點選空白時段直接觸發
        select: function (info) {
            openBookingDrawer(info.startStr, info.endStr);
        },
        // 取得預約資料
        events: function (fetchInfo, successCallback, failureCallback) {
            var currentRoomId = $('#ddlRoomFilter').val();
            $.ajax({
                url: 'Services/BookingService.ashx',
                type: 'GET',
                data: {
                    action: 'get_events',
                    roomId: currentRoomId,
                    start: fetchInfo.startStr,
                    end: fetchInfo.endStr
                },
                dataType: 'json',
                success: function (data) {
                    successCallback(data);
                },
                error: function () {
                    failureCallback();
                }
            });
        },
        // 日式客製化事件外觀
        eventClassNames: function (arg) {
            if (arg.event.extendedProps.status === 'Pending') {
                return ['cal-event-pending'];
            } else if (arg.event.extendedProps.isMine) {
                return ['cal-event-mine'];
            }
            return ['cal-event-approved'];
        }
    });

    calendar.render();
    
    // 切換會議室時重刷資料
    $('#ddlRoomFilter').on('change', function () {
        calendar.refetchEvents();
    });
});
```

---

## 7. 安全性與部署注意事項

1. **防止跨站請求偽造 (CSRF)**：在 Web Forms 中為每一筆 AJAX 請求附加 ASP.NET 的 Anti-CSRF Token (`ViewStateUserKey` 或 RequestVerificationToken)。
2. **時區一致性**：伺服器統一採 `CST (UTC+8)` 處理時間，資料庫儲存規格嚴格對齊 30 分鐘刻度。
3. **歷史資料歸檔**：建立排程作業（SQL Agent Job），定期封存一年前的已結束歷史預約至 `Bookings_History`，確保線上排程查詢效率。