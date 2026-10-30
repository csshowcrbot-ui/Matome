-- ========================================================
-- 間調 (Matome) - 會議室及設備借用系統
-- 資料庫結構與初始資料建立指令檔
-- ========================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'MeetingRoomBookingDb')
BEGIN
    CREATE DATABASE MeetingRoomBookingDb;
END
GO

USE MeetingRoomBookingDb;
GO

-- 1. 清除舊有表（若存在）
IF OBJECT_ID('MatomeBookingEquipments', 'U') IS NOT NULL DROP TABLE MatomeBookingEquipments;
IF OBJECT_ID('MatomeEquipmentBookings', 'U') IS NOT NULL DROP TABLE MatomeEquipmentBookings;
IF OBJECT_ID('MatomeBookings', 'U') IS NOT NULL DROP TABLE MatomeBookings;
IF OBJECT_ID('MatomeEquipments', 'U') IS NOT NULL DROP TABLE MatomeEquipments;
IF OBJECT_ID('MatomeRooms', 'U') IS NOT NULL DROP TABLE MatomeRooms;
IF OBJECT_ID('MatomeUsers', 'U') IS NOT NULL DROP TABLE MatomeUsers;
IF OBJECT_ID('MatomeDepartments', 'U') IS NOT NULL DROP TABLE MatomeDepartments;

-- 相容清理舊命名表
IF OBJECT_ID('BookingEquipments', 'U') IS NOT NULL DROP TABLE BookingEquipments;
IF OBJECT_ID('EquipmentBookings', 'U') IS NOT NULL DROP TABLE EquipmentBookings;
IF OBJECT_ID('Bookings', 'U') IS NOT NULL DROP TABLE Bookings;
IF OBJECT_ID('Equipments', 'U') IS NOT NULL DROP TABLE Equipments;
IF OBJECT_ID('Rooms', 'U') IS NOT NULL DROP TABLE Rooms;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
IF OBJECT_ID('Departments', 'U') IS NOT NULL DROP TABLE Departments;
GO

-- 2. 建立部門表 (MatomeDepartments)
CREATE TABLE MatomeDepartments (
    DeptId INT IDENTITY(1,1) PRIMARY KEY,
    DeptName NVARCHAR(50) NOT NULL
);
GO

-- 3. 建立使用者與審核人員表 (MatomeUsers)
CREATE TABLE MatomeUsers (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Account NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    FullName NVARCHAR(50) NOT NULL,
    DeptId INT NULL FOREIGN KEY REFERENCES MatomeDepartments(DeptId),
    Email NVARCHAR(100) NOT NULL,
    Role NVARCHAR(20) NOT NULL, -- 'Admin', 'Approver', 'User'
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- 4. 建立會議室主表 (MatomeRooms)
CREATE TABLE MatomeRooms (
    RoomId INT IDENTITY(1,1) PRIMARY KEY,
    RoomName NVARCHAR(100) NOT NULL,
    Capacity INT NOT NULL,
    Location NVARCHAR(100) NULL,
    IsApprovalRequired BIT NOT NULL DEFAULT 0,
    ApprovalDeptId INT NULL FOREIGN KEY REFERENCES MatomeDepartments(DeptId),
    IsActive BIT NOT NULL DEFAULT 1,
    CreateTime DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- 5. 建立設備資產表 (MatomeEquipments)
CREATE TABLE MatomeEquipments (
    EquipmentId INT IDENTITY(1,1) PRIMARY KEY,
    EquipmentName NVARCHAR(100) NOT NULL,
    TotalQuantity INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- 6. 建立預約主表 (MatomeBookings)
CREATE TABLE MatomeBookings (
    BookingId INT IDENTITY(1,1) PRIMARY KEY,
    RoomId INT NOT NULL FOREIGN KEY REFERENCES MatomeRooms(RoomId),
    UserId INT NOT NULL FOREIGN KEY REFERENCES MatomeUsers(UserId),
    Subject NVARCHAR(150) NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NOT NULL,
    Status NVARCHAR(20) NOT NULL, -- 'Pending', 'Approved', 'Rejected', 'Cancelled'
    ApprovedBy INT NULL FOREIGN KEY REFERENCES MatomeUsers(UserId),
    ApprovedTime DATETIME NULL,
    RejectReason NVARCHAR(250) NULL,
    CreateTime DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- 7. 建立附加設備明細表 (MatomeBookingEquipments)
CREATE TABLE MatomeBookingEquipments (
    DetailId INT IDENTITY(1,1) PRIMARY KEY,
    BookingId INT NOT NULL FOREIGN KEY REFERENCES MatomeBookings(BookingId) ON DELETE CASCADE,
    EquipmentId INT NOT NULL FOREIGN KEY REFERENCES MatomeEquipments(EquipmentId),
    Quantity INT NOT NULL DEFAULT 1
);
GO

-- 8. 建立獨立設備預約主表 (MatomeEquipmentBookings)
CREATE TABLE MatomeEquipmentBookings (
    BookingId INT IDENTITY(1,1) PRIMARY KEY,
    EquipmentId INT NOT NULL FOREIGN KEY REFERENCES MatomeEquipments(EquipmentId),
    UserId INT NOT NULL FOREIGN KEY REFERENCES MatomeUsers(UserId),
    Subject NVARCHAR(150) NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    Status NVARCHAR(20) NOT NULL, -- 'Approved', 'Pending', 'Cancelled', 'Rejected'
    ApprovedBy INT NULL FOREIGN KEY REFERENCES MatomeUsers(UserId),
    ApprovedTime DATETIME NULL,
    RejectReason NVARCHAR(250) NULL,
    CreateTime DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- 建立索引加速衝突查詢與狀態檢索
CREATE INDEX IX_MatomeBookings_Room_Time ON MatomeBookings(RoomId, StartTime, EndTime, Status);
CREATE INDEX IX_MatomeBookings_User ON MatomeBookings(UserId, StartTime);
CREATE INDEX IX_MatomeBookingEquipments_Equip ON MatomeBookingEquipments(EquipmentId, BookingId);
CREATE INDEX IX_MatomeEquipmentBookings_Equip_Time ON MatomeEquipmentBookings(EquipmentId, StartTime, EndTime, Status);
GO

-- ========================================================
-- 初始種子資料 (Seed Data)
-- ========================================================

-- 部門資料
INSERT INTO MatomeDepartments (DeptName) VALUES 
(N'行政總務課'),
(N'資訊技術處'),
(N'業務行銷部'),
(N'人力資源處');
GO

-- 使用者資料 (密碼 admin123 或 pass123)
INSERT INTO MatomeUsers (Account, PasswordHash, FullName, DeptId, Email, Role, IsActive) VALUES
(N'admin', N'240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', N'系統管理員 (佐藤一朗)', 2, N'admin@corp.local', N'Admin', 1),
(N'approver_ga', N'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', N'總務主管 (高橋健二)', 1, N'takahashi@corp.local', N'Approver', 1),
(N'approver_it', N'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', N'資訊主管 (渡邊直樹)', 2, N'watanabe@corp.local', N'Approver', 1),
(N'user_chen', N'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', N'陳小明 (行銷專員)', 3, N'chen.xm@corp.local', N'User', 1),
(N'user_lin', N'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', N'林雅晴 (人資管理師)', 4, N'lin.yc@corp.local', N'User', 1);
GO

-- 會議室資料
INSERT INTO MatomeRooms (RoomName, Capacity, Location, IsApprovalRequired, ApprovalDeptId, IsActive) VALUES
(N'和風階梯演講廳', 100, N'A棟 3F', 1, 1, 1),      -- 需審核 (總務課)
(N'檜木大會議室', 24, N'B棟 2F', 1, 1, 1),         -- 需審核 (總務課)
(N'櫻花多功能研討室', 12, N'B棟 3F', 0, NULL, 1),   -- 免審核
(N'竹林創意討論室', 8, N'A棟 2F', 0, NULL, 1);      -- 免審核
GO

-- 設備資料
INSERT INTO MatomeEquipments (EquipmentName, TotalQuantity, IsActive) VALUES
(N'無線手握式麥克風 (組)', 6, 1),
(N'4K高畫質視訊會議主機', 3, 1),
(N'簡報專用高亮度雷射翻頁筆', 8, 1),
(N'投影延伸螢幕 / 電子白板', 2, 1),
(N'便攜式藍牙擴音喇叭', 4, 1);
GO

-- 動態計算當週日期以建立豐富的示範預約資料
DECLARE @Today DATE = CAST(GETDATE() AS DATE);
-- 找出本週一 (Monday)
DECLARE @Monday DATETIME = DATEADD(DAY, 1 - DATEPART(WEEKDAY, @Today) + 1, CAST(@Today AS DATETIME));

-- 預約 1: 檜木大會議室 - 專案週會 (已核准)
INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, ApprovedBy, ApprovedTime, CreateTime)
VALUES (2, 4, N'行銷部 Q4 產品週會', DATEADD(HOUR, 9, @Monday), DATEADD(HOUR, 11, @Monday), 'Approved', 2, DATEADD(HOUR, -2, GETDATE()), DATEADD(DAY, -1, GETDATE()));
DECLARE @B1 INT = SCOPE_IDENTITY();
INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (@B1, 1, 2), (@B1, 2, 1);

-- 預約 2: 櫻花多功能研討室 - 跨部門專案對焦 (免審核已通過)
INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, ApprovedBy, ApprovedTime, CreateTime)
VALUES (3, 5, N'招募計畫面試會議', DATEADD(HOUR, 14, DATEADD(DAY, 1, @Monday)), DATEADD(MINUTE, 90, DATEADD(HOUR, 14, DATEADD(DAY, 1, @Monday))), 'Approved', NULL, NULL, DATEADD(DAY, -1, GETDATE()));
DECLARE @B2 INT = SCOPE_IDENTITY();
INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (@B2, 3, 1);

-- 預約 3: 和風階梯演講廳 - 全球客戶交流大會 (待審核)
INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, CreateTime)
VALUES (1, 4, N'全球策略夥伴交流座談會', DATEADD(HOUR, 13, DATEADD(DAY, 2, @Monday)), DATEADD(HOUR, 17, DATEADD(DAY, 2, @Monday)), 'Pending', GETDATE());
DECLARE @B3 INT = SCOPE_IDENTITY();
INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (@B3, 1, 4), (@B3, 2, 2), (@B3, 4, 1);

-- 預約 4: 檜木大會議室 - 董事會籌備會議 (待審核)
INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, CreateTime)
VALUES (2, 5, N'年度考核委員會籌備會', DATEADD(HOUR, 10, DATEADD(DAY, 3, @Monday)), DATEADD(HOUR, 12, DATEADD(DAY, 3, @Monday)), 'Pending', GETDATE());
DECLARE @B4 INT = SCOPE_IDENTITY();
INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (@B4, 1, 1), (@B4, 3, 1);
GO

SELECT 'Setup Completed Successfully' AS Result, (SELECT COUNT(1) FROM MatomeRooms) AS RoomsCount, (SELECT COUNT(1) FROM MatomeBookings) AS BookingsCount;
GO
