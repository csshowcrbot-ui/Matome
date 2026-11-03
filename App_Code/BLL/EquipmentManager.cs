using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Matome.DAL;
using Matome.Models;

namespace Matome.BLL
{
    public class EquipmentManager
    {
        public List<Equipment> GetAllEquipments(bool onlyActive = false)
        {
            var list = new List<Equipment>();
            string sql = "SELECT EquipmentId, EquipmentName, TotalQuantity, IsActive FROM MatomeEquipments " +
                         (onlyActive ? "WHERE IsActive = TRUE " : "") +
                         "ORDER BY EquipmentId ASC;";

            var dt = SqlHelper.ExecuteDataTable(sql);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new Equipment {
                    EquipmentId = Convert.ToInt32(row["EquipmentId"]),
                    EquipmentName = row["EquipmentName"].ToString(),
                    TotalQuantity = Convert.ToInt32(row["TotalQuantity"]),
                    IsActive = Convert.ToBoolean(row["IsActive"])
                });
            }
            return list;
        }

        public Equipment GetEquipmentById(int equipmentId)
        {
            string sql = "SELECT EquipmentId, EquipmentName, TotalQuantity, IsActive FROM MatomeEquipments WHERE EquipmentId = @EquipmentId;";
            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@EquipmentId", equipmentId)
            });

            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                return new Equipment {
                    EquipmentId = Convert.ToInt32(row["EquipmentId"]),
                    EquipmentName = row["EquipmentName"].ToString(),
                    TotalQuantity = Convert.ToInt32(row["TotalQuantity"]),
                    IsActive = Convert.ToBoolean(row["IsActive"])
                };
            }
            return null;
        }

        public bool AddEquipment(Equipment eq, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(eq.EquipmentName))
            {
                errorMessage = "設備名稱為必填項目。";
                return false;
            }
            if (eq.TotalQuantity < 0)
            {
                errorMessage = "總庫存量不可為負數。";
                return false;
            }

            string sql = @"
                INSERT INTO MatomeEquipments (EquipmentName, TotalQuantity, IsActive)
                VALUES (@EquipmentName, @TotalQuantity, @IsActive);";

            SqlHelper.ExecuteNonQuery(sql, new[] {
                new NpgsqlParameter("@EquipmentName", eq.EquipmentName.Trim()),
                new NpgsqlParameter("@TotalQuantity", eq.TotalQuantity),
                new NpgsqlParameter("@IsActive", eq.IsActive)
            });
            return true;
        }

        public bool UpdateEquipment(Equipment eq, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(eq.EquipmentName))
            {
                errorMessage = "設備名稱為必填項目。";
                return false;
            }
            if (eq.TotalQuantity < 0)
            {
                errorMessage = "總庫存量不可為負數。";
                return false;
            }

            string sql = @"
                UPDATE MatomeEquipments
                SET EquipmentName = @EquipmentName,
                    TotalQuantity = @TotalQuantity,
                    IsActive = @IsActive
                WHERE EquipmentId = @EquipmentId;";

            SqlHelper.ExecuteNonQuery(sql, new[] {
                new NpgsqlParameter("@EquipmentName", eq.EquipmentName.Trim()),
                new NpgsqlParameter("@TotalQuantity", eq.TotalQuantity),
                new NpgsqlParameter("@IsActive", eq.IsActive),
                new NpgsqlParameter("@EquipmentId", eq.EquipmentId)
            });
            return true;
        }

        public void ToggleEquipmentStatus(int equipmentId, bool isActive)
        {
            string sql = "UPDATE MatomeEquipments SET IsActive = @IsActive WHERE EquipmentId = @EquipmentId;";
            SqlHelper.ExecuteNonQuery(sql, new[] {
                new NpgsqlParameter("@IsActive", isActive),
                new NpgsqlParameter("@EquipmentId", equipmentId)
            });
        }

        public bool DeleteEquipment(int equipmentId, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (equipmentId <= 0)
            {
                errorMessage = "請指定欲刪除的設備資產編號。";
                return false;
            }

            try
            {
                using (var conn = SqlHelper.GetConnection())
                {
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. 刪除所有會議室預約附加之此設備項目明細
                            string sqlDeleteBookingEquips = "DELETE FROM MatomeBookingEquipments WHERE EquipmentId = @EquipmentId;";
                            using (var cmd = new NpgsqlCommand(sqlDeleteBookingEquips, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                                cmd.ExecuteNonQuery();
                            }

                            // 2. 刪除所有獨立借用此設備之記錄
                            string sqlDeleteEquipBookings = "DELETE FROM MatomeEquipmentBookings WHERE EquipmentId = @EquipmentId;";
                            using (var cmd = new NpgsqlCommand(sqlDeleteEquipBookings, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                                cmd.ExecuteNonQuery();
                            }

                            // 3. 刪除設備資產本體
                            string sqlDeleteEquip = "DELETE FROM MatomeEquipments WHERE EquipmentId = @EquipmentId;";
                            using (var cmd = new NpgsqlCommand(sqlDeleteEquip, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                                int rows = cmd.ExecuteNonQuery();
                                if (rows == 0)
                                {
                                    tran.Rollback();
                                    errorMessage = "找不到指定的設備資產項目，可能已被刪除。";
                                    return false;
                                }
                            }

                            tran.Commit();
                            return true;
                        }
                        catch (Exception exInner)
                        {
                            tran.Rollback();
                            errorMessage = "刪除設備資產失敗：" + exInner.Message;
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = "資料庫連線或操作發生錯誤：" + ex.Message;
                return false;
            }
        }

        public List<EquipmentAvailabilityDto> GetAvailableEquipments(DateTime startTime, DateTime endTime, int? excludeBookingId = null)
        {
            var list = new List<EquipmentAvailabilityDto>();
            string sql = @"
                SELECT 
                    e.EquipmentId,
                    e.EquipmentName,
                    e.TotalQuantity,
                    COALESCE(SUM(CASE WHEN b.BookingId IS NOT NULL THEN be.Quantity ELSE 0 END), 0) AS UsedQuantity,
                    (e.TotalQuantity - COALESCE(SUM(CASE WHEN b.BookingId IS NOT NULL THEN be.Quantity ELSE 0 END), 0)) AS AvailableQuantity
                FROM MatomeEquipments e
                LEFT JOIN MatomeBookingEquipments be ON e.EquipmentId = be.EquipmentId
                LEFT JOIN MatomeBookings b ON be.BookingId = b.BookingId 
                    AND b.Status IN ('Approved', 'Pending')
                    AND (@ExcludeBookingId IS NULL OR b.BookingId <> @ExcludeBookingId)
                    AND (b.StartTime < @EndTime AND b.EndTime > @StartTime)
                WHERE e.IsActive = TRUE
                GROUP BY e.EquipmentId, e.EquipmentName, e.TotalQuantity
                ORDER BY e.EquipmentId ASC;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@StartTime", startTime),
                new NpgsqlParameter("@EndTime", endTime),
                new NpgsqlParameter("@ExcludeBookingId", (object)excludeBookingId ?? DBNull.Value)
            });

            foreach (DataRow row in dt.Rows)
            {
                int total = Convert.ToInt32(row["TotalQuantity"]);
                int used = Convert.ToInt32(row["UsedQuantity"]);
                int avail = Convert.ToInt32(row["AvailableQuantity"]);
                if (avail < 0) avail = 0;

                list.Add(new EquipmentAvailabilityDto {
                    EquipmentId = Convert.ToInt32(row["EquipmentId"]),
                    EquipmentName = row["EquipmentName"].ToString(),
                    TotalQuantity = total,
                    UsedQuantity = used,
                    AvailableQuantity = avail
                });
            }
            return list;
        }

        // ==========================================
        // 獨立設備借用核心邏輯
        // ==========================================

        public int GetEquipmentAvailableQty(int equipmentId, DateTime startTime, DateTime endTime, int? excludeBookingId = null)
        {
            var eq = GetEquipmentById(equipmentId);
            if (eq == null || !eq.IsActive) return 0;

            string sql = @"
                SELECT COALESCE(SUM(Quantity), 0)
                FROM MatomeEquipmentBookings
                WHERE EquipmentId = @EquipmentId
                  AND Status IN ('Approved', 'Pending')
                  AND (@ExcludeId IS NULL OR BookingId <> @ExcludeId)
                  AND (StartTime < @EndTime AND EndTime > @StartTime);";

            object result = SqlHelper.ExecuteScalar(sql, new[] {
                new NpgsqlParameter("@EquipmentId", equipmentId),
                new NpgsqlParameter("@ExcludeId", (object)excludeBookingId ?? DBNull.Value),
                new NpgsqlParameter("@StartTime", startTime),
                new NpgsqlParameter("@EndTime", endTime)
            });

            int used = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            int avail = eq.TotalQuantity - used;
            return avail < 0 ? 0 : avail;
        }

        public bool CreateEquipmentBooking(int userId, int equipmentId, string subject, DateTime startTime, DateTime endTime, int quantity, out string errorMessage, out int createdBookingId)
        {
            errorMessage = string.Empty;
            createdBookingId = 0;

            if ((startTime.Minute != 0 && startTime.Minute != 30) || (endTime.Minute != 0 && endTime.Minute != 30) || startTime >= endTime)
            {
                errorMessage = "借用時段必須以 30 分鐘為單位（00 或 30 分），且結束時間必須大於開始時間。";
                return false;
            }

            if (startTime < DateTime.Now.AddMinutes(-5))
            {
                errorMessage = "無法借用已經過去的時間。";
                return false;
            }

            if (quantity <= 0)
            {
                errorMessage = "借用數量必須大於 0。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                errorMessage = "借用事由為必填項目。";
                return false;
            }

            using (NpgsqlConnection conn = SqlHelper.GetConnection())
            {
                using (NpgsqlTransaction trans = conn.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    try
                    {
                        // 1. 檢驗設備狀態
                        string checkEqSql = "SELECT EquipmentName, TotalQuantity, IsActive FROM MatomeEquipments WHERE EquipmentId = @EquipmentId;";
                        string eqName = "";
                        int totalQty = 0;

                        DataTable dtEq = new DataTable();
                        using (NpgsqlCommand cmd = new NpgsqlCommand(checkEqSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                            using (NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd))
                            {
                                da.Fill(dtEq);
                            }
                        }

                        if (dtEq.Rows.Count == 0)
                        {
                            errorMessage = "找不到指定的設備。";
                            return false;
                        }

                        DataRow eqRow = dtEq.Rows[0];
                        if (!Convert.ToBoolean(eqRow["IsActive"]))
                        {
                            errorMessage = "該設備目前維護停用中，無法借用。";
                            return false;
                        }
                        eqName = eqRow["EquipmentName"].ToString();
                        totalQty = Convert.ToInt32(eqRow["TotalQuantity"]);

                        // 2. 檢驗並發可用庫存
                        string checkUsageSql = @"
                            SELECT COALESCE(SUM(Quantity), 0)
                            FROM MatomeEquipmentBookings
                            WHERE EquipmentId = @EquipmentId
                              AND Status IN ('Approved', 'Pending')
                              AND (StartTime < @EndTime AND EndTime > @StartTime);";

                        int usedQty = 0;
                        using (NpgsqlCommand cmd = new NpgsqlCommand(checkUsageSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                            cmd.Parameters.AddWithValue("@StartTime", startTime);
                            cmd.Parameters.AddWithValue("@EndTime", endTime);
                            object val = cmd.ExecuteScalar();
                            usedQty = (val != null && val != DBNull.Value) ? Convert.ToInt32(val) : 0;
                        }

                        int availQty = totalQty - usedQty;
                        if (availQty < quantity)
                        {
                            errorMessage = string.Format("很抱歉，設備【{0}】在此時段可用庫存僅剩 {1} 個，不足申請需求 ({2} 個)。", eqName, availQty, quantity);
                            return false;
                        }

                        // 3. 寫入設備借用紀錄 (預設直接 Approved 免審核，或依需求)
                        string insertSql = @"
                            INSERT INTO MatomeEquipmentBookings (EquipmentId, UserId, Subject, StartTime, EndTime, Quantity, Status, CreateTime)
                            VALUES (@EquipmentId, @UserId, @Subject, @StartTime, @EndTime, @Quantity, 'Approved', CURRENT_TIMESTAMP)
                            RETURNING BookingId;";

                        using (NpgsqlCommand cmd = new NpgsqlCommand(insertSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@EquipmentId", equipmentId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@Subject", subject.Trim());
                            cmd.Parameters.AddWithValue("@StartTime", startTime);
                            cmd.Parameters.AddWithValue("@EndTime", endTime);
                            cmd.Parameters.AddWithValue("@Quantity", quantity);

                            object val = cmd.ExecuteScalar();
                            createdBookingId = Convert.ToInt32(val);
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            if (trans.Connection != null)
                            {
                                trans.Rollback();
                            }
                        }
                        catch { }

                        errorMessage = "設備預約處理失敗：" + ex.Message;
                        return false;
                    }
                }
            }
        }

        public bool CancelEquipmentBooking(int bookingId, int currentUserId, bool isAdmin, out string errorMessage)
        {
            errorMessage = string.Empty;

            var booking = GetEquipmentBookingById(bookingId);
            if (booking == null)
            {
                errorMessage = "找不到該筆設備借用紀錄。";
                return false;
            }

            if (!isAdmin && booking.UserId != currentUserId)
            {
                errorMessage = "您無權限取消他人的設備借用申請。";
                return false;
            }

            if (booking.Status != "Pending" && booking.Status != "Approved")
            {
                errorMessage = string.Format("該預約狀態為【{0}】，無法取消。", booking.Status);
                return false;
            }

            if (booking.StartTime <= DateTime.Now && !isAdmin)
            {
                errorMessage = "借用起始時間已過或已在進行中，無法取消。";
                return false;
            }

            string updateSql = "UPDATE MatomeEquipmentBookings SET Status = 'Cancelled' WHERE BookingId = @BookingId;";
            int rows = SqlHelper.ExecuteNonQuery(updateSql, new[] {
                new NpgsqlParameter("@BookingId", bookingId)
            });

            return rows > 0;
        }

        public EquipmentBooking GetEquipmentBookingById(int bookingId)
        {
            string sql = @"
                SELECT eb.BookingId, eb.EquipmentId, e.EquipmentName, eb.UserId, u.FullName AS UserName,
                       u.Email AS UserEmail, d.DeptName AS UserDeptName, eb.Subject, eb.StartTime, eb.EndTime,
                       eb.Quantity, eb.Status, eb.ApprovedBy, ap.FullName AS ApproverName, eb.ApprovedTime,
                       eb.RejectReason, eb.CreateTime
                FROM MatomeEquipmentBookings eb
                INNER JOIN MatomeEquipments e ON eb.EquipmentId = e.EquipmentId
                INNER JOIN MatomeUsers u ON eb.UserId = u.UserId
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                LEFT JOIN MatomeUsers ap ON eb.ApprovedBy = ap.UserId
                WHERE eb.BookingId = @BookingId;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@BookingId", bookingId)
            });

            if (dt.Rows.Count > 0)
            {
                return MapEquipmentBookingFromRow(dt.Rows[0]);
            }
            return null;
        }

        public List<EquipmentBooking> GetUserEquipmentBookings(int userId, string filter)
        {
            if (string.IsNullOrEmpty(filter)) filter = "all";
            var list = new List<EquipmentBooking>();
            string condition = "WHERE eb.UserId = @UserId ";

            if (string.Equals(filter, "upcoming", StringComparison.OrdinalIgnoreCase))
            {
                condition += "AND eb.EndTime >= CURRENT_TIMESTAMP ";
            }
            else if (string.Equals(filter, "history", StringComparison.OrdinalIgnoreCase))
            {
                condition += "AND eb.EndTime < CURRENT_TIMESTAMP ";
            }

            string sql = string.Format(@"
                SELECT eb.BookingId, eb.EquipmentId, e.EquipmentName, eb.UserId, u.FullName AS UserName,
                       u.Email AS UserEmail, d.DeptName AS UserDeptName, eb.Subject, eb.StartTime, eb.EndTime,
                       eb.Quantity, eb.Status, eb.ApprovedBy, ap.FullName AS ApproverName, eb.ApprovedTime,
                       eb.RejectReason, eb.CreateTime
                FROM MatomeEquipmentBookings eb
                INNER JOIN MatomeEquipments e ON eb.EquipmentId = e.EquipmentId
                INNER JOIN MatomeUsers u ON eb.UserId = u.UserId
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                LEFT JOIN MatomeUsers ap ON eb.ApprovedBy = ap.UserId
                {0}
                ORDER BY eb.StartTime DESC;", condition);

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@UserId", userId)
            });

            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapEquipmentBookingFromRow(row));
            }

            return list;
        }

        public List<CalendarEventDto> GetEquipmentCalendarEvents(int equipmentId, DateTime start, DateTime end, int currentUserId)
        {
            var list = new List<CalendarEventDto>();
            string sql = @"
                SELECT eb.BookingId, eb.EquipmentId, e.EquipmentName, eb.UserId, u.FullName, d.DeptName,
                       eb.Subject, eb.StartTime, eb.EndTime, eb.Quantity, eb.Status
                FROM MatomeEquipmentBookings eb
                INNER JOIN MatomeEquipments e ON eb.EquipmentId = e.EquipmentId
                INNER JOIN MatomeUsers u ON eb.UserId = u.UserId
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                WHERE eb.EquipmentId = @EquipmentId
                  AND eb.Status IN ('Approved', 'Pending')
                  AND (eb.StartTime < @End AND eb.EndTime > @Start)
                ORDER BY eb.StartTime ASC;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@EquipmentId", equipmentId),
                new NpgsqlParameter("@Start", start),
                new NpgsqlParameter("@End", end)
            });

            foreach (DataRow row in dt.Rows)
            {
                int bookingId = Convert.ToInt32(row["BookingId"]);
                int userId = Convert.ToInt32(row["UserId"]);
                string status = row["Status"].ToString();
                string subject = row["Subject"].ToString();
                string userName = row["FullName"].ToString();
                string deptName = row["DeptName"] != DBNull.Value ? row["DeptName"].ToString() : "";
                int qty = Convert.ToInt32(row["Quantity"]);
                DateTime sTime = Convert.ToDateTime(row["StartTime"]);
                DateTime eTime = Convert.ToDateTime(row["EndTime"]);

                bool isMine = (userId == currentUserId);

                string cssClass = "cal-event-approved";
                if (status == "Pending")
                {
                    cssClass = isMine ? "cal-event-pending cal-event-mine-pending" : "cal-event-pending";
                }
                else if (isMine)
                {
                    cssClass = "cal-event-mine";
                }

                var evt = new CalendarEventDto {
                    id = bookingId.ToString(),
                    title = string.Format("{0} x{1} {2}", subject, qty, isMine ? "(我借)" : string.Format("({0})", userName)),
                    start = sTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = eTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    className = cssClass
                };

                evt.extendedProps["bookingId"] = bookingId;
                evt.extendedProps["equipmentId"] = equipmentId;
                evt.extendedProps["equipmentName"] = row["EquipmentName"].ToString();
                evt.extendedProps["subject"] = subject;
                evt.extendedProps["quantity"] = qty;
                evt.extendedProps["userName"] = userName;
                evt.extendedProps["deptName"] = deptName;
                evt.extendedProps["status"] = status;
                evt.extendedProps["isMine"] = isMine;
                evt.extendedProps["timeDisplay"] = string.Format("{0:HH:mm} ~ {1:HH:mm}", sTime, eTime);

                list.Add(evt);
            }

            return list;
        }

        private EquipmentBooking MapEquipmentBookingFromRow(DataRow row)
        {
            return new EquipmentBooking {
                BookingId = Convert.ToInt32(row["BookingId"]),
                EquipmentId = Convert.ToInt32(row["EquipmentId"]),
                EquipmentName = row["EquipmentName"].ToString(),
                UserId = Convert.ToInt32(row["UserId"]),
                UserName = row["UserName"].ToString(),
                UserEmail = row["UserEmail"] != DBNull.Value ? row["UserEmail"].ToString() : "",
                UserDeptName = row["UserDeptName"] != DBNull.Value ? row["UserDeptName"].ToString() : "",
                Subject = row["Subject"].ToString(),
                StartTime = Convert.ToDateTime(row["StartTime"]),
                EndTime = Convert.ToDateTime(row["EndTime"]),
                Quantity = Convert.ToInt32(row["Quantity"]),
                Status = row["Status"].ToString(),
                ApprovedBy = row["ApprovedBy"] != DBNull.Value ? (int?)Convert.ToInt32(row["ApprovedBy"]) : null,
                ApproverName = row["ApproverName"] != DBNull.Value ? row["ApproverName"].ToString() : "",
                ApprovedTime = row["ApprovedTime"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ApprovedTime"]) : null,
                RejectReason = row["RejectReason"] != DBNull.Value ? row["RejectReason"].ToString() : "",
                CreateTime = Convert.ToDateTime(row["CreateTime"])
            };
        }
    }
}
