using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Matome.DAL;
using Matome.Models;

namespace Matome.BLL
{
    public class ApprovalManager
    {
        private readonly BookingManager _bookingMgr = new BookingManager();

        public List<Booking> GetPendingBookingsForUser(User user)
        {
            if (user == null) return new List<Booking>();
            bool isGlobalAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            return GetPendingBookings(user.UserId, isGlobalAdmin, user.DeptId);
        }

        public List<Booking> GetPendingBookings(int approverUserId, bool isGlobalAdmin, int? deptId)
        {
            var list = new List<Booking>();
            string sql;
            NpgsqlParameter[] parameters;

            if (isGlobalAdmin)
            {
                // 最高管理員可審核全公司所有會議室待審案
                sql = @"
                    SELECT b.BookingId, b.RoomId, r.RoomName, b.UserId, u.FullName AS UserName, u.Email AS UserEmail,
                           d.DeptName AS UserDeptName, b.Subject, b.StartTime, b.EndTime, b.Status,
                           b.ApprovedBy, ap.FullName AS ApproverName, b.ApprovedTime, b.RejectReason, b.CreateTime,
                           r.ApprovalDeptId, ad.DeptName AS ApprovalDeptName
                    FROM MatomeBookings b
                    INNER JOIN MatomeRooms r ON b.RoomId = r.RoomId
                    INNER JOIN MatomeUsers u ON b.UserId = u.UserId
                    LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                    LEFT JOIN MatomeUsers ap ON b.ApprovedBy = ap.UserId
                    LEFT JOIN MatomeDepartments ad ON r.ApprovalDeptId = ad.DeptId
                    WHERE b.Status = 'Pending'
                    ORDER BY b.StartTime ASC;";
                parameters = new NpgsqlParameter[0];
            }
            else
            {
                // 部門審核主管僅能審核歸屬其部門管轄之會議室預約案
                sql = @"
                    SELECT b.BookingId, b.RoomId, r.RoomName, b.UserId, u.FullName AS UserName, u.Email AS UserEmail,
                           d.DeptName AS UserDeptName, b.Subject, b.StartTime, b.EndTime, b.Status,
                           b.ApprovedBy, ap.FullName AS ApproverName, b.ApprovedTime, b.RejectReason, b.CreateTime,
                           r.ApprovalDeptId, ad.DeptName AS ApprovalDeptName
                    FROM MatomeBookings b
                    INNER JOIN MatomeRooms r ON b.RoomId = r.RoomId
                    INNER JOIN MatomeUsers u ON b.UserId = u.UserId
                    LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                    LEFT JOIN MatomeUsers ap ON b.ApprovedBy = ap.UserId
                    LEFT JOIN MatomeDepartments ad ON r.ApprovalDeptId = ad.DeptId
                    WHERE b.Status = 'Pending'
                      AND (r.ApprovalDeptId = @DeptId OR r.ApprovalDeptId IS NULL)
                    ORDER BY b.StartTime ASC;";
                parameters = new[] {
                    new NpgsqlParameter("@DeptId", (object)deptId ?? DBNull.Value)
                };
            }

            var dt = SqlHelper.ExecuteDataTable(sql, parameters);
            foreach (DataRow row in dt.Rows)
            {
                var booking = new Booking {
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

                booking.Equipments = _bookingMgr.GetBookingEquipments(booking.BookingId);
                list.Add(booking);
            }

            return list;
        }

        public bool CheckHasConflictWithApproved(int bookingId, int roomId, DateTime start, DateTime end)
        {
            string sql = @"
                SELECT COUNT(1) FROM MatomeBookings
                WHERE RoomId = @RoomId 
                  AND BookingId <> @BookingId
                  AND Status = 'Approved'
                  AND (StartTime < @EndTime AND EndTime > @StartTime);";

            int count = Convert.ToInt32(SqlHelper.ExecuteScalar(sql, new[] {
                new NpgsqlParameter("@RoomId", roomId),
                new NpgsqlParameter("@BookingId", bookingId),
                new NpgsqlParameter("@StartTime", start),
                new NpgsqlParameter("@EndTime", end)
            }));

            return count > 0;
        }

        public bool ApproveBooking(int bookingId, int approverUserId, out string errorMessage)
        {
            errorMessage = string.Empty;

            var booking = _bookingMgr.GetBookingById(bookingId);
            if (booking == null)
            {
                errorMessage = "找不到該筆預約紀錄。";
                return false;
            }

            if (booking.Status != "Pending")
            {
                errorMessage = string.Format("該預約當前狀態為【{0}】，無需重複核准。", booking.Status);
                return false;
            }

            // 嚴格依權限維護所屬部門核對審核權限
            var userMgr = new UserManager();
            var approver = userMgr.GetUserById(approverUserId);
            if (approver != null && !string.Equals(approver.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var roomMgr = new RoomManager();
                var room = roomMgr.GetRoomById(booking.RoomId);
                if (room != null && room.ApprovalDeptId.HasValue)
                {
                    if (!approver.DeptId.HasValue || approver.DeptId.Value != room.ApprovalDeptId.Value)
                    {
                        errorMessage = "權限不足：您在權限維護所屬之部門單位與此會議室指定管轄審核單位不符。";
                        return false;
                    }
                }
            }

            using (NpgsqlConnection conn = SqlHelper.GetConnection())
            {
                using (NpgsqlTransaction trans = conn.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    try
                    {
                        // 1. 再次防呆檢驗會議室衝突 (是否有其它同室已 Approved)
                        string conflictSql = @"
                            SELECT COUNT(1) FROM MatomeBookings
                            WHERE RoomId = @RoomId 
                              AND BookingId <> @BookingId
                              AND Status = 'Approved'
                              AND (StartTime < @EndTime AND EndTime > @StartTime);";

                        using (NpgsqlCommand cmd = new NpgsqlCommand(conflictSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@RoomId", booking.RoomId);
                            cmd.Parameters.AddWithValue("@BookingId", bookingId);
                            cmd.Parameters.AddWithValue("@StartTime", booking.StartTime);
                            cmd.Parameters.AddWithValue("@EndTime", booking.EndTime);

                            int approvedConflict = Convert.ToInt32(cmd.ExecuteScalar());
                            if (approvedConflict > 0)
                            {
                                errorMessage = "核准失敗：該時段已被其他已通過核准之預約佔用。";
                                trans.Rollback();
                                return false;
                            }
                        }

                        // 2. 檢驗設備庫存是否已被佔滿
                        if (booking.Equipments != null && booking.Equipments.Count > 0)
                        {
                            foreach (var eq in booking.Equipments)
                            {
                                string checkEquipSql = @"
                                    SELECT (e.TotalQuantity - COALESCE(SUM(CASE WHEN b.BookingId IS NOT NULL THEN be.Quantity ELSE 0 END), 0)) AS AvailableQty, e.EquipmentName
                                    FROM MatomeEquipments e
                                    LEFT JOIN MatomeBookingEquipments be ON e.EquipmentId = be.EquipmentId
                                    LEFT JOIN MatomeBookings b ON be.BookingId = b.BookingId 
                                        AND b.Status = 'Approved'
                                        AND b.BookingId <> @BookingId
                                        AND (b.StartTime < @EndTime AND b.EndTime > @StartTime)
                                    WHERE e.EquipmentId = @EquipmentId AND e.IsActive = TRUE
                                    GROUP BY e.EquipmentId, e.EquipmentName, e.TotalQuantity;";

                                using (NpgsqlCommand cmd = new NpgsqlCommand(checkEquipSql, conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@EquipmentId", eq.EquipmentId);
                                    cmd.Parameters.AddWithValue("@BookingId", bookingId);
                                    cmd.Parameters.AddWithValue("@StartTime", booking.StartTime);
                                    cmd.Parameters.AddWithValue("@EndTime", booking.EndTime);

                                    using (var eqRdr = cmd.ExecuteReader())
                                    {
                                        if (eqRdr.Read())
                                        {
                                            int avail = Convert.ToInt32(eqRdr["AvailableQty"]);
                                            string eqName = eqRdr["EquipmentName"].ToString();
                                            if (avail < eq.Quantity)
                                            {
                                                errorMessage = string.Format("核准失敗：設備【{0}】可用數量已不足 ({1} < {2})。", eqName, avail, eq.Quantity);
                                                trans.Rollback();
                                                return false;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 3. 更新狀態為 Approved
                        string approveSql = @"
                            UPDATE MatomeBookings 
                            SET Status = 'Approved',
                                ApprovedBy = @ApprovedBy,
                                ApprovedTime = CURRENT_TIMESTAMP
                            WHERE BookingId = @BookingId;";

                        using (NpgsqlCommand cmd = new NpgsqlCommand(approveSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@ApprovedBy", approverUserId);
                            cmd.Parameters.AddWithValue("@BookingId", bookingId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        errorMessage = "核准處理異常：" + ex.Message;
                        return false;
                    }
                }
            }
        }

        public bool RejectBooking(int bookingId, int approverUserId, string rejectReason, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                errorMessage = "駁回原因為必填項目。";
                return false;
            }

            var booking = _bookingMgr.GetBookingById(bookingId);
            if (booking == null)
            {
                errorMessage = "找不到該筆預約紀錄。";
                return false;
            }

            if (booking.Status != "Pending")
            {
                errorMessage = string.Format("該預約當前狀態為【{0}】，無法進行駁回。", booking.Status);
                return false;
            }

            // 嚴格依權限維護所屬部門核對審核權限
            var userMgr = new UserManager();
            var approver = userMgr.GetUserById(approverUserId);
            if (approver != null && !string.Equals(approver.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var roomMgr = new RoomManager();
                var room = roomMgr.GetRoomById(booking.RoomId);
                if (room != null && room.ApprovalDeptId.HasValue)
                {
                    if (!approver.DeptId.HasValue || approver.DeptId.Value != room.ApprovalDeptId.Value)
                    {
                        errorMessage = "權限不足：您在權限維護所屬之部門單位與此會議室指定管轄審核單位不符。";
                        return false;
                    }
                }
            }

            string updateSql = @"
                UPDATE MatomeBookings 
                SET Status = 'Rejected',
                    RejectReason = @RejectReason,
                    ApprovedBy = @ApprovedBy,
                    ApprovedTime = CURRENT_TIMESTAMP
                WHERE BookingId = @BookingId;";

            int rows = SqlHelper.ExecuteNonQuery(updateSql, new[] {
                new NpgsqlParameter("@RejectReason", rejectReason.Trim()),
                new NpgsqlParameter("@ApprovedBy", approverUserId),
                new NpgsqlParameter("@BookingId", bookingId)
            });

            return rows > 0;
        }
    }
}
