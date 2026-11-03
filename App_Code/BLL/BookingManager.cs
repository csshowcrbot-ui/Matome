using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Matome.DAL;
using Matome.Models;

namespace Matome.BLL
{
    public class BookingManager
    {
        public bool CreateBooking(int userId, int roomId, string subject, DateTime startTime, DateTime endTime, 
                                 List<EquipmentItem> equipments, out string errorMessage, out int createdBookingId)
        {
            errorMessage = string.Empty;
            createdBookingId = 0;

            // 1. 驗證 30 分鐘單位規則
            if ((startTime.Minute != 0 && startTime.Minute != 30) || (endTime.Minute != 0 && endTime.Minute != 30) || startTime >= endTime)
            {
                errorMessage = "預約時段必須以 30 分鐘為單位（00 或 30 分），且結束時間必須大於開始時間。";
                return false;
            }

            if (startTime < DateTime.Now.AddMinutes(-5))
            {
                errorMessage = "無法預約已經過去的時間。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                errorMessage = "會議事由為必填項目。";
                return false;
            }

            using (NpgsqlConnection conn = SqlHelper.GetConnection())
            {
                using (NpgsqlTransaction trans = conn.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    try
                    {
                        // 2. 檢驗會議室有效性與是否需審核
                        string roomSql = "SELECT RoomName, IsApprovalRequired, IsActive FROM MatomeRooms WHERE RoomId = @RoomId;";
                        string roomName = "";
                        bool isApprovalRequired = false;

                        DataTable dtRoom = new DataTable();
                        using (NpgsqlCommand cmd = new NpgsqlCommand(roomSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RoomId", roomId);
                            using (NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd))
                            {
                                da.Fill(dtRoom);
                            }
                        }

                        if (dtRoom.Rows.Count == 0)
                        {
                            errorMessage = "找不到指定的會議室。";
                            return false;
                        }

                        DataRow roomRow = dtRoom.Rows[0];
                        if (!Convert.ToBoolean(roomRow["IsActive"]))
                        {
                            errorMessage = "該會議室目前停用中，無法預約。";
                            return false;
                        }

                        roomName = roomRow["RoomName"].ToString();
                        isApprovalRequired = Convert.ToBoolean(roomRow["IsApprovalRequired"]);

                        // 3. 檢驗時段衝突
                        string conflictSql = @"
                            SELECT COUNT(1) FROM MatomeBookings
                            WHERE RoomId = @RoomId 
                              AND Status IN ('Approved', 'Pending')
                              AND (StartTime < @EndTime AND EndTime > @StartTime);";

                        using (NpgsqlCommand cmd = new NpgsqlCommand(conflictSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RoomId", roomId);
                            cmd.Parameters.AddWithValue("@StartTime", startTime);
                            cmd.Parameters.AddWithValue("@EndTime", endTime);

                            object val = cmd.ExecuteScalar();
                            int conflictCount = (val != null && val != DBNull.Value) ? Convert.ToInt32(val) : 0;
                            if (conflictCount > 0)
                            {
                                errorMessage = string.Format("很抱歉，【{0}】在所選時段已被其他人員預約或審核中。", roomName);
                                return false;
                            }
                        }

                        // 4. 檢驗設備庫存重疊上限
                        if (equipments != null && equipments.Count > 0)
                        {
                            foreach (var eq in equipments)
                            {
                                if (eq.Quantity <= 0) continue;

                                string checkEquipSql = @"
                                    SELECT (e.TotalQuantity - COALESCE(SUM(CASE WHEN b.BookingId IS NOT NULL THEN be.Quantity ELSE 0 END), 0)) AS AvailableQty, e.EquipmentName
                                    FROM MatomeEquipments e
                                    LEFT JOIN MatomeBookingEquipments be ON e.EquipmentId = be.EquipmentId
                                    LEFT JOIN MatomeBookings b ON be.BookingId = b.BookingId 
                                        AND b.Status IN ('Approved', 'Pending')
                                        AND (b.StartTime < @EndTime AND b.EndTime > @StartTime)
                                    WHERE e.EquipmentId = @EquipmentId AND e.IsActive = TRUE
                                    GROUP BY e.EquipmentId, e.EquipmentName, e.TotalQuantity;";

                                DataTable dtEq = new DataTable();
                                using (NpgsqlCommand cmd = new NpgsqlCommand(checkEquipSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@EquipmentId", eq.EquipmentId);
                                    cmd.Parameters.AddWithValue("@StartTime", startTime);
                                    cmd.Parameters.AddWithValue("@EndTime", endTime);
                                    using (NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd))
                                    {
                                        da.Fill(dtEq);
                                    }
                                }

                                if (dtEq.Rows.Count > 0)
                                {
                                    int available = Convert.ToInt32(dtEq.Rows[0]["AvailableQty"]);
                                    string eqName = dtEq.Rows[0]["EquipmentName"].ToString();
                                    if (available < eq.Quantity)
                                    {
                                        errorMessage = string.Format("設備【{0}】在此時段可用數量僅剩 {1} 個，不足申請需求 ({2})。", eqName, available, eq.Quantity);
                                        return false;
                                    }
                                }
                                else
                                {
                                    errorMessage = "所選設備已停用或不存在。";
                                    return false;
                                }
                            }
                        }

                        // 5. 寫入主預約記錄
                        string initialStatus = isApprovalRequired ? "Pending" : "Approved";
                        string insertBookingSql = @"
                            INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, CreateTime)
                            VALUES (@RoomId, @UserId, @Subject, @StartTime, @EndTime, @Status, CURRENT_TIMESTAMP)
                            RETURNING BookingId;";

                        using (NpgsqlCommand cmd = new NpgsqlCommand(insertBookingSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RoomId", roomId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@Subject", subject.Trim());
                            cmd.Parameters.AddWithValue("@StartTime", startTime);
                            cmd.Parameters.AddWithValue("@EndTime", endTime);
                            cmd.Parameters.AddWithValue("@Status", initialStatus);

                            object val = cmd.ExecuteScalar();
                            createdBookingId = Convert.ToInt32(val);
                        }

                        // 6. 寫入借用設備明細
                        if (equipments != null && equipments.Count > 0)
                        {
                            foreach (var eq in equipments)
                            {
                                if (eq.Quantity <= 0) continue;

                                string insertEqSql = @"
                                    INSERT INTO MatomeBookingEquipments (BookingId, EquipmentId, Quantity)
                                    VALUES (@BookingId, @EquipmentId, @Quantity);";

                                using (NpgsqlCommand cmd = new NpgsqlCommand(insertEqSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@BookingId", createdBookingId);
                                    cmd.Parameters.AddWithValue("@EquipmentId", eq.EquipmentId);
                                    cmd.Parameters.AddWithValue("@Quantity", eq.Quantity);
                                    cmd.ExecuteNonQuery();
                                }
                            }
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

                        errorMessage = "系統資料庫處理失敗：" + ex.Message;
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// 每週定期預約批次建立方法
        /// 在單一資料庫交易中，逐一核驗所有指定週次的排程衝突；任一場次重疊則整批復原並警示。
        /// </summary>
        public bool CreateRecurringBookings(int userId, int roomId, string subject, DateTime initialStartTime, DateTime initialEndTime,
                                            DateTime recurringEndDate,
                                            out string errorMessage, out List<int> createdBookingIds)
        {
            errorMessage = string.Empty;
            createdBookingIds = new List<int>();

            // 1. 檢核初始時段格式
            if ((initialStartTime.Minute != 0 && initialStartTime.Minute != 30) || (initialEndTime.Minute != 0 && initialEndTime.Minute != 30) || initialStartTime >= initialEndTime)
            {
                errorMessage = "預約時段必須以 30 分鐘為單位（00 或 30 分），且結束時間必須大於開始時間。";
                return false;
            }

            if (initialStartTime < DateTime.Now.AddMinutes(-5))
            {
                errorMessage = "無法預約已經過去的時間。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                errorMessage = "會議事由為必填項目。";
                return false;
            }

            // 計算全系列週次區間 (最多延伸 26 週 = 半年，確保系統資源防護)
            DateTime cutoff = recurringEndDate.Date.AddDays(1).AddSeconds(-1);
            if (cutoff <= initialStartTime)
            {
                errorMessage = "定期會議迄止日期必須晚於開始日期。";
                return false;
            }

            TimeSpan sessionDuration = initialEndTime - initialStartTime;
            List<Tuple<DateTime, DateTime>> sessionList = new List<Tuple<DateTime, DateTime>>();

            DateTime curStart = initialStartTime;
            while (curStart <= cutoff)
            {
                DateTime curEnd = curStart + sessionDuration;
                sessionList.Add(Tuple.Create(curStart, curEnd));

                // 往後跳一週 (7天)
                curStart = curStart.AddDays(7);

                if (sessionList.Count > 26)
                {
                    errorMessage = "定期會議最長僅支援預約未來 26 週 (半年) 內之時段。";
                    return false;
                }
            }

            if (sessionList.Count == 0)
            {
                errorMessage = "未能產生有效的定期預約時段，請確認迄止日期。";
                return false;
            }

            using (NpgsqlConnection conn = SqlHelper.GetConnection())
            {
                using (NpgsqlTransaction trans = conn.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    try
                    {
                        // 1. 檢驗會議室有效性與審核設定
                        string roomSql = "SELECT RoomName, IsApprovalRequired, IsActive FROM MatomeRooms WHERE RoomId = @RoomId;";
                        string roomName = "";
                        bool isApprovalRequired = false;

                        DataTable dtRoom = new DataTable();
                        using (NpgsqlCommand cmd = new NpgsqlCommand(roomSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RoomId", roomId);
                            using (NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd))
                            {
                                da.Fill(dtRoom);
                            }
                        }

                        if (dtRoom.Rows.Count == 0)
                        {
                            errorMessage = "找不到指定的會議室。";
                            return false;
                        }

                        DataRow roomRow = dtRoom.Rows[0];
                        if (!Convert.ToBoolean(roomRow["IsActive"]))
                        {
                            errorMessage = "該會議室目前停用中，無法預約。";
                            return false;
                        }

                        roomName = roomRow["RoomName"].ToString();
                        isApprovalRequired = Convert.ToBoolean(roomRow["IsApprovalRequired"]);

                        // 2. 依序檢驗每一個週次的衝突
                        string conflictSql = @"
                            SELECT COUNT(1) FROM MatomeBookings
                            WHERE RoomId = @RoomId 
                              AND Status IN ('Approved', 'Pending')
                              AND (StartTime < @EndTime AND EndTime > @StartTime);";

                        foreach (var session in sessionList)
                        {
                            using (NpgsqlCommand cmd = new NpgsqlCommand(conflictSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@RoomId", roomId);
                                cmd.Parameters.AddWithValue("@StartTime", session.Item1);
                                cmd.Parameters.AddWithValue("@EndTime", session.Item2);

                                object val = cmd.ExecuteScalar();
                                int conflictCount = (val != null && val != DBNull.Value) ? Convert.ToInt32(val) : 0;
                                if (conflictCount > 0)
                                {
                                    errorMessage = string.Format("很抱歉，【{0}】在 {1:yyyy-MM-dd (ddd) HH:mm} 已被其他人員預約或審核中，為維護時段完整性，整批定期會議預約未送出。", roomName, session.Item1);
                                    return false;
                                }
                            }
                        }

                        // 3. 全數時段檢核無誤，批次寫入主預約記錄
                        string initialStatus = isApprovalRequired ? "Pending" : "Approved";
                        string insertBookingSql = @"
                            INSERT INTO MatomeBookings (RoomId, UserId, Subject, StartTime, EndTime, Status, CreateTime)
                            VALUES (@RoomId, @UserId, @Subject, @StartTime, @EndTime, @Status, CURRENT_TIMESTAMP)
                            RETURNING BookingId;";

                        foreach (var session in sessionList)
                        {
                            using (NpgsqlCommand cmd = new NpgsqlCommand(insertBookingSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@RoomId", roomId);
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@Subject", subject.Trim());
                                cmd.Parameters.AddWithValue("@StartTime", session.Item1);
                                cmd.Parameters.AddWithValue("@EndTime", session.Item2);
                                cmd.Parameters.AddWithValue("@Status", initialStatus);

                                object val = cmd.ExecuteScalar();
                                int newId = Convert.ToInt32(val);
                                createdBookingIds.Add(newId);
                            }
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

                        errorMessage = "定期預約批次處理異常：" + ex.Message;
                        return false;
                    }
                }
            }
        }

        public bool CancelBooking(int bookingId, int currentUserId, bool isAdmin, out string errorMessage)
        {
            errorMessage = string.Empty;

            var booking = GetBookingById(bookingId);
            if (booking == null)
            {
                errorMessage = "找不到該筆預約紀錄。";
                return false;
            }

            if (!isAdmin && booking.UserId != currentUserId)
            {
                errorMessage = "您無權限取消他人的預約紀錄。";
                return false;
            }

            if (booking.Status != "Pending" && booking.Status != "Approved")
            {
                errorMessage = string.Format("該預約狀態為【{0}】，無法進行取消。", booking.Status);
                return false;
            }

            if (booking.StartTime <= DateTime.Now && !isAdmin)
            {
                errorMessage = "會議起始時間已過或正在進行中，無法取消。";
                return false;
            }

            string updateSql = "UPDATE MatomeBookings SET Status = 'Cancelled' WHERE BookingId = @BookingId;";
            int rows = SqlHelper.ExecuteNonQuery(updateSql, new[] {
                new NpgsqlParameter("@BookingId", bookingId)
            });

            return rows > 0;
        }

        public List<CalendarEventDto> GetCalendarEvents(int roomId, DateTime start, DateTime end, int currentUserId)
        {
            var list = new List<CalendarEventDto>();
            string sql = @"
                SELECT b.BookingId, b.RoomId, r.RoomName, b.UserId, u.FullName, d.DeptName,
                       b.Subject, b.StartTime, b.EndTime, b.Status
                FROM MatomeBookings b
                INNER JOIN MatomeRooms r ON b.RoomId = r.RoomId
                INNER JOIN MatomeUsers u ON b.UserId = u.UserId
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                WHERE b.RoomId = @RoomId 
                  AND b.Status IN ('Approved', 'Pending')
                  AND (b.StartTime < @End AND b.EndTime > @Start)
                ORDER BY b.StartTime ASC;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@RoomId", roomId),
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
                    title = string.Format("{0} {1}", subject, isMine ? "(我的預約)" : string.Format("({0})", userName)),
                    start = sTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = eTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    className = cssClass
                };

                evt.extendedProps["bookingId"] = bookingId;
                evt.extendedProps["roomId"] = Convert.ToInt32(row["RoomId"]);
                evt.extendedProps["roomName"] = row["RoomName"].ToString();
                evt.extendedProps["subject"] = subject;
                evt.extendedProps["userName"] = userName;
                evt.extendedProps["deptName"] = deptName;
                evt.extendedProps["status"] = status;
                evt.extendedProps["isMine"] = isMine;
                evt.extendedProps["timeDisplay"] = string.Format("{0:HH:mm} ~ {1:HH:mm}", sTime, eTime);

                list.Add(evt);
            }

            return list;
        }

        public List<Booking> GetUserBookings(int userId, string filter)
        {
            if (string.IsNullOrEmpty(filter)) filter = "all";
            var list = new List<Booking>();
            string condition = "WHERE b.UserId = @UserId ";

            if (string.Equals(filter, "upcoming", StringComparison.OrdinalIgnoreCase))
            {
                condition += "AND b.EndTime >= CURRENT_TIMESTAMP ";
            }
            else if (string.Equals(filter, "history", StringComparison.OrdinalIgnoreCase))
            {
                condition += "AND b.EndTime < CURRENT_TIMESTAMP ";
            }

            string sql = string.Format(@"
                SELECT b.BookingId, b.RoomId, r.RoomName, b.UserId, u.FullName AS UserName, u.Email AS UserEmail,
                       d.DeptName AS UserDeptName, b.Subject, b.StartTime, b.EndTime, b.Status,
                       b.ApprovedBy, ap.FullName AS ApproverName, b.ApprovedTime, b.RejectReason, b.CreateTime
                FROM MatomeBookings b
                INNER JOIN MatomeRooms r ON b.RoomId = r.RoomId
                INNER JOIN MatomeUsers u ON b.UserId = u.UserId
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                LEFT JOIN MatomeUsers ap ON b.ApprovedBy = ap.UserId
                {0}
                ORDER BY b.StartTime DESC;", condition);

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@UserId", userId)
            });

            foreach (DataRow row in dt.Rows)
            {
                var booking = MapBookingFromRow(row);
                booking.Equipments = GetBookingEquipments(booking.BookingId);
                list.Add(booking);
            }

            return list;
        }

        public Booking GetBookingById(int bookingId)
        {
            string sql = @"
                SELECT b.BookingId, b.RoomId, r.RoomName, b.UserId, u.FullName AS UserName, u.Email AS UserEmail,
                       d.DeptName AS UserDeptName, b.Subject, b.StartTime, b.EndTime, b.Status,
                       b.ApprovedBy, ap.FullName AS ApproverName, b.ApprovedTime, b.RejectReason, b.CreateTime
                FROM MatomeBookings b
                INNER JOIN MatomeRooms r ON b.RoomId = r.RoomId
                INNER JOIN MatomeUsers u ON b.UserId = u.UserId
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                LEFT JOIN MatomeUsers ap ON b.ApprovedBy = ap.UserId
                WHERE b.BookingId = @BookingId;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@BookingId", bookingId)
            });

            if (dt.Rows.Count > 0)
            {
                var booking = MapBookingFromRow(dt.Rows[0]);
                booking.Equipments = GetBookingEquipments(booking.BookingId);
                return booking;
            }
            return null;
        }

        public List<BookingEquipment> GetBookingEquipments(int bookingId)
        {
            var list = new List<BookingEquipment>();
            string sql = @"
                SELECT be.DetailId, be.BookingId, be.EquipmentId, e.EquipmentName, be.Quantity
                FROM MatomeBookingEquipments be
                INNER JOIN MatomeEquipments e ON be.EquipmentId = e.EquipmentId
                WHERE be.BookingId = @BookingId
                ORDER BY be.DetailId ASC;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@BookingId", bookingId)
            });

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new BookingEquipment {
                    DetailId = Convert.ToInt32(row["DetailId"]),
                    BookingId = Convert.ToInt32(row["BookingId"]),
                    EquipmentId = Convert.ToInt32(row["EquipmentId"]),
                    EquipmentName = row["EquipmentName"].ToString(),
                    Quantity = Convert.ToInt32(row["Quantity"])
                });
            }

            return list;
        }

        private Booking MapBookingFromRow(DataRow row)
        {
            return new Booking {
                BookingId = Convert.ToInt32(row["BookingId"]),
                RoomId = Convert.ToInt32(row["RoomId"]),
                RoomName = row["RoomName"].ToString(),
                UserId = Convert.ToInt32(row["UserId"]),
                UserName = row["UserName"].ToString(),
                UserEmail = row["UserEmail"] != DBNull.Value ? row["UserEmail"].ToString() : "",
                UserDeptName = row["UserDeptName"] != DBNull.Value ? row["UserDeptName"].ToString() : "",
                Subject = row["Subject"].ToString(),
                StartTime = Convert.ToDateTime(row["StartTime"]),
                EndTime = Convert.ToDateTime(row["EndTime"]),
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
