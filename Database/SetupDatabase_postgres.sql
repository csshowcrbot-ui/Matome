-- ========================================================
-- 間調 (Matome) - 會議室及設備借用系統
-- PostgreSQL 資料庫結構與初始資料建立指令檔
-- ========================================================

-- 1. 清除舊有表（若存在）
DROP TABLE IF EXISTS MatomeBookingEquipments CASCADE;
DROP TABLE IF EXISTS MatomeEquipmentBookings CASCADE;
DROP TABLE IF EXISTS MatomeBookings CASCADE;
DROP TABLE IF EXISTS MatomeEquipments CASCADE;
DROP TABLE IF EXISTS MatomeRooms CASCADE;
DROP TABLE IF EXISTS MatomeUsers CASCADE;
DROP TABLE IF EXISTS MatomeDepartments CASCADE;

-- 相容清理舊命名表
DROP TABLE IF EXISTS BookingEquipments CASCADE;
DROP TABLE IF EXISTS EquipmentBookings CASCADE;
DROP TABLE IF EXISTS Bookings CASCADE;
DROP TABLE IF EXISTS Equipments CASCADE;
DROP TABLE IF EXISTS Rooms CASCADE;
DROP TABLE IF EXISTS Users CASCADE;
DROP TABLE IF EXISTS Departments CASCADE;

-- 2. 建立部門表 (MatomeDepartments)
CREATE TABLE MatomeDepartments (
    DeptId SERIAL PRIMARY KEY,
    DeptName VARCHAR(50) NOT NULL
);

-- 3. 建立使用者與審核人員表 (MatomeUsers)
CREATE TABLE MatomeUsers (
    UserId SERIAL PRIMARY KEY,
    Account VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(256) NOT NULL,
    FullName VARCHAR(50) NOT NULL,
    DeptId INT NULL REFERENCES MatomeDepartments(DeptId),
    Email VARCHAR(100) NOT NULL,
    Role VARCHAR(20) NOT NULL, -- 'Admin', 'Approver', 'User'
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

-- 4. 建立會議室主表 (MatomeRooms)
CREATE TABLE MatomeRooms (
    RoomId SERIAL PRIMARY KEY,
    RoomName VARCHAR(100) NOT NULL,
    Capacity INT NOT NULL,
    Location VARCHAR(100) NULL,
    IsApprovalRequired BOOLEAN NOT NULL DEFAULT FALSE,
    ApprovalDeptId INT NULL REFERENCES MatomeDepartments(DeptId),
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreateTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 5. 建立設備資產表 (MatomeEquipments)
CREATE TABLE MatomeEquipments (
    EquipmentId SERIAL PRIMARY KEY,
    EquipmentName VARCHAR(100) NOT NULL,
    TotalQuantity INT NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

-- 6. 建立預約主表 (MatomeBookings)
CREATE TABLE MatomeBookings (
    BookingId SERIAL PRIMARY KEY,
    RoomId INT NOT NULL REFERENCES MatomeRooms(RoomId),
    UserId INT NOT NULL REFERENCES MatomeUsers(UserId),
    Subject VARCHAR(150) NOT NULL,
    StartTime TIMESTAMP NOT NULL,
    EndTime TIMESTAMP NOT NULL,
    Status VARCHAR(20) NOT NULL, -- 'Pending', 'Approved', 'Rejected', 'Cancelled'
    ApprovedBy INT NULL REFERENCES MatomeUsers(UserId),
    ApprovedTime TIMESTAMP NULL,
    RejectReason VARCHAR(250) NULL,
    CreateTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 7. 建立附加設備明細表 (MatomeBookingEquipments)
CREATE TABLE MatomeBookingEquipments (
    DetailId SERIAL PRIMARY KEY,
    BookingId INT NOT NULL REFERENCES MatomeBookings(BookingId) ON DELETE CASCADE,
    EquipmentId INT NOT NULL REFERENCES MatomeEquipments(EquipmentId),
    Quantity INT NOT NULL DEFAULT 1
);

-- 8. 建立獨立設備預約主表 (MatomeEquipmentBookings)
CREATE TABLE MatomeEquipmentBookings (
    BookingId SERIAL PRIMARY KEY,
    EquipmentId INT NOT NULL REFERENCES MatomeEquipments(EquipmentId),
    UserId INT NOT NULL REFERENCES MatomeUsers(UserId),
    Subject VARCHAR(150) NOT NULL,
    StartTime TIMESTAMP NOT NULL,
    EndTime TIMESTAMP NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    Status VARCHAR(20) NOT NULL, -- 'Approved', 'Pending', 'Cancelled', 'Rejected'
    ApprovedBy INT NULL REFERENCES MatomeUsers(UserId),
    ApprovedTime TIMESTAMP NULL,
    RejectReason VARCHAR(250) NULL,
    CreateTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 建立索引加速衝突查詢與狀態檢索
CREATE INDEX IF NOT EXISTS IX_MatomeBookings_Room_Time ON MatomeBookings(RoomId, StartTime, EndTime, Status);
CREATE INDEX IF NOT EXISTS IX_MatomeBookings_User ON MatomeBookings(UserId, StartTime);
CREATE INDEX IF NOT EXISTS IX_MatomeBookingEquipments_Equip ON MatomeBookingEquipments(EquipmentId, BookingId);
CREATE INDEX IF NOT EXISTS IX_MatomeEquipmentBookings_Equip_Time ON MatomeEquipmentBookings(EquipmentId, StartTime, EndTime, Status);

-- ========================================================
-- 初始種子資料 (Seed Data)
-- ========================================================

-- 部門資料
INSERT INTO MatomeDepartments (DeptName) VALUES 
('行政總務課'),
('資訊技術處'),
('業務行銷部'),
('人力資源處');

-- 使用者資料 (密碼 admin123 或 pass123)
INSERT INTO MatomeUsers (Account, PasswordHash, FullName, DeptId, Email, Role, IsActive) VALUES
('admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', '系統管理員 (佐藤一朗)', 2, 'admin@corp.local', 'Admin', TRUE),
('approver_ga', 'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', '總務主管 (高橋健二)', 1, 'takahashi@corp.local', 'Approver', TRUE),
('approver_it', 'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', '資訊主管 (渡邊直樹)', 2, 'watanabe@corp.local', 'Approver', TRUE),
('user_chen', 'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', '陳小明 (行銷專員)', 3, 'chen.xm@corp.local', 'User', TRUE),
('user_lin', 'ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f', '林雅晴 (人資管理師)', 4, 'lin.yc@corp.local', 'User', TRUE);

-- 會議室資料
INSERT INTO MatomeRooms (RoomName, Capacity, Location, IsApprovalRequired, ApprovalDeptId, IsActive) VALUES
('和風階梯演講廳', 100, 'A棟 3F', TRUE, 1, TRUE),      -- 需審核 (總務課)
('檜木大會議室', 24, 'B棟 2F', TRUE, 1, TRUE),         -- 需審核 (總務課)
('櫻花多功能研討室', 12, 'B棟 3F', FALSE, NULL, TRUE),   -- 免審核
('竹林創意討論室', 8, 'A棟 2F', FALSE, NULL, TRUE);      -- 免審核

-- 設備資料
INSERT INTO MatomeEquipments (EquipmentName, TotalQuantity, IsActive) VALUES
('無線手握式麥克風 (組)', 6, TRUE),
('4K高畫質視訊會議主機', 3, TRUE),
('簡報專用高亮度雷射翻頁筆', 8, TRUE),
('投影延伸螢幕 / 電子白板', 2, TRUE),
('便攜式藍牙擴音喇叭', 4, TRUE);

-- 動態計算當週日期以建立示範預約資料
DO $$
DECLARE
    v_today DATE := CURRENT_DATE;
    v_monday TIMESTAMP := date_trunc('week', CURRENT_TIMESTAMP);
    v_b1 INT;
    v_b2 INT;
    v_b3 INT;
    v_b4 INT;
BEGIN
    -- 預約 1: 檜木大會議室 - 專案週會 (已核准)
    INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, ApprovedBy, ApprovedTime, CreateTime)
    VALUES (2, 4, '行銷部 Q4 產品週會', v_monday + interval '9 hours', v_monday + interval '11 hours', 'Approved', 2, CURRENT_TIMESTAMP - interval '2 hours', CURRENT_TIMESTAMP - interval '1 day')
    RETURNING BookingId INTO v_b1;

    INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (v_b1, 1, 2), (v_b1, 2, 1);

    -- 預約 2: 櫻花多功能研討室 - 跨部門專案對焦 (免審核已通過)
    INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, ApprovedBy, ApprovedTime, CreateTime)
    VALUES (3, 5, '招募計畫面試會議', v_monday + interval '1 day 14 hours', v_monday + interval '1 day 15 hours 30 minutes', 'Approved', NULL, NULL, CURRENT_TIMESTAMP - interval '1 day')
    RETURNING BookingId INTO v_b2;

    INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (v_b2, 3, 1);

    -- 預約 3: 和風階梯演講廳 - 全球客戶交流大會 (待審核)
    INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, CreateTime)
    VALUES (1, 4, '全球策略夥伴交流座談會', v_monday + interval '2 days 13 hours', v_monday + interval '2 days 17 hours', 'Pending', CURRENT_TIMESTAMP)
    RETURNING BookingId INTO v_b3;

    INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (v_b3, 1, 4), (v_b3, 2, 2), (v_b3, 4, 1);

    -- 預約 4: 檜木大會議室 - 董事會籌備會議 (待審核)
    INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, CreateTime)
    VALUES (2, 5, '年度考核委員會籌備會', v_monday + interval '3 days 10 hours', v_monday + interval '3 days 12 hours', 'Pending', CURRENT_TIMESTAMP)
    RETURNING BookingId INTO v_b4;

    INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity) VALUES (v_b4, 1, 1), (v_b4, 3, 1);
END $$;
