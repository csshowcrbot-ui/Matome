using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Matome.DAL;
using Matome.Models;

namespace Matome.BLL
{
    public class RoomManager
    {
        public List<Room> GetAllRooms(bool onlyActive = false)
        {
            var list = new List<Room>();
            string sql = @"
                SELECT r.RoomId, r.RoomName, r.Capacity, r.Location, r.IsApprovalRequired, 
                       r.ApprovalDeptId, d.DeptName AS ApprovalDeptName, r.IsActive, r.CreateTime
                FROM MatomeRooms r
                LEFT JOIN MatomeDepartments d ON r.ApprovalDeptId = d.DeptId
                " + (onlyActive ? "WHERE r.IsActive = TRUE " : "") + @"
                ORDER BY r.RoomId ASC;";

            var dt = SqlHelper.ExecuteDataTable(sql);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapRoomFromRow(row));
            }
            return list;
        }

        public Room GetRoomById(int roomId)
        {
            string sql = @"
                SELECT r.RoomId, r.RoomName, r.Capacity, r.Location, r.IsApprovalRequired, 
                       r.ApprovalDeptId, d.DeptName AS ApprovalDeptName, r.IsActive, r.CreateTime
                FROM MatomeRooms r
                LEFT JOIN MatomeDepartments d ON r.ApprovalDeptId = d.DeptId
                WHERE r.RoomId = @RoomId;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@RoomId", roomId)
            });

            if (dt.Rows.Count > 0)
            {
                return MapRoomFromRow(dt.Rows[0]);
            }
            return null;
        }

        public bool AddRoom(Room room, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(room.RoomName))
            {
                errorMessage = "會議室名稱為必填項目。";
                return false;
            }
            if (room.Capacity <= 0)
            {
                errorMessage = "容納人數必須為大於 0 的正整數。";
                return false;
            }

            string sql = @"
                INSERT INTO MatomeRooms (RoomName, Capacity, Location, IsApprovalRequired, ApprovalDeptId, IsActive, CreateTime)
                VALUES (@RoomName, @Capacity, @Location, @IsApprovalRequired, @ApprovalDeptId, @IsActive, CURRENT_TIMESTAMP);";

            SqlHelper.ExecuteNonQuery(sql, new[] {
                new NpgsqlParameter("@RoomName", room.RoomName.Trim()),
                new NpgsqlParameter("@Capacity", room.Capacity),
                new NpgsqlParameter("@Location", room.Location ?? string.Empty),
                new NpgsqlParameter("@IsApprovalRequired", room.IsApprovalRequired),
                new NpgsqlParameter("@ApprovalDeptId", (room.IsApprovalRequired && room.ApprovalDeptId.HasValue) ? (object)room.ApprovalDeptId.Value : DBNull.Value),
                new NpgsqlParameter("@IsActive", room.IsActive)
            });
            return true;
        }

        public bool UpdateRoom(Room room, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(room.RoomName))
            {
                errorMessage = "會議室名稱為必填項目。";
                return false;
            }
            if (room.Capacity <= 0)
            {
                errorMessage = "容納人數必須為大於 0 的正整數。";
                return false;
            }

            string sql = @"
                UPDATE MatomeRooms 
                SET RoomName = @RoomName,
                    Capacity = @Capacity,
                    Location = @Location,
                    IsApprovalRequired = @IsApprovalRequired,
                    ApprovalDeptId = @ApprovalDeptId,
                    IsActive = @IsActive
                WHERE RoomId = @RoomId;";

            SqlHelper.ExecuteNonQuery(sql, new[] {
                new NpgsqlParameter("@RoomName", room.RoomName.Trim()),
                new NpgsqlParameter("@Capacity", room.Capacity),
                new NpgsqlParameter("@Location", room.Location ?? string.Empty),
                new NpgsqlParameter("@IsApprovalRequired", room.IsApprovalRequired),
                new NpgsqlParameter("@ApprovalDeptId", (room.IsApprovalRequired && room.ApprovalDeptId.HasValue) ? (object)room.ApprovalDeptId.Value : DBNull.Value),
                new NpgsqlParameter("@IsActive", room.IsActive),
                new NpgsqlParameter("@RoomId", room.RoomId)
            });
            return true;
        }

        public void ToggleRoomStatus(int roomId, bool isActive)
        {
            string sql = "UPDATE MatomeRooms SET IsActive = @IsActive WHERE RoomId = @RoomId;";
            SqlHelper.ExecuteNonQuery(sql, new[] {
                new NpgsqlParameter("@IsActive", isActive),
                new NpgsqlParameter("@RoomId", roomId)
            });
        }

        public bool DeleteRoom(int roomId, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (roomId <= 0)
            {
                errorMessage = "請指定欲刪除的會議室編號。";
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
                            // 1. 刪除該會議室下所有預約的附加設備記錄
                            string sqlDeleteBookingEquips = @"
                                DELETE FROM MatomeBookingEquipments 
                                WHERE BookingId IN (SELECT BookingId FROM MatomeBookings WHERE RoomId = @RoomId);";
                            using (var cmd = new NpgsqlCommand(sqlDeleteBookingEquips, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@RoomId", roomId);
                                cmd.ExecuteNonQuery();
                            }

                            // 2. 刪除該會議室的所有預約記錄
                            string sqlDeleteBookings = "DELETE FROM MatomeBookings WHERE RoomId = @RoomId;";
                            using (var cmd = new NpgsqlCommand(sqlDeleteBookings, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@RoomId", roomId);
                                cmd.ExecuteNonQuery();
                            }

                            // 3. 刪除會議室本體
                            string sqlDeleteRoom = "DELETE FROM MatomeRooms WHERE RoomId = @RoomId;";
                            using (var cmd = new NpgsqlCommand(sqlDeleteRoom, conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@RoomId", roomId);
                                int rows = cmd.ExecuteNonQuery();
                                if (rows == 0)
                                {
                                    tran.Rollback();
                                    errorMessage = "找不到指定的會議室項目，可能已被刪除。";
                                    return false;
                                }
                            }

                            tran.Commit();
                            return true;
                        }
                        catch (Exception exInner)
                        {
                            tran.Rollback();
                            errorMessage = "刪除會議室失敗：" + exInner.Message;
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

        private Room MapRoomFromRow(DataRow row)
        {
            return new Room {
                RoomId = Convert.ToInt32(row["RoomId"]),
                RoomName = row["RoomName"].ToString(),
                Capacity = Convert.ToInt32(row["Capacity"]),
                Location = row["Location"] != DBNull.Value ? row["Location"].ToString() : "",
                IsApprovalRequired = Convert.ToBoolean(row["IsApprovalRequired"]),
                ApprovalDeptId = row["ApprovalDeptId"] != DBNull.Value ? (int?)Convert.ToInt32(row["ApprovalDeptId"]) : null,
                ApprovalDeptName = row["ApprovalDeptName"] != DBNull.Value ? row["ApprovalDeptName"].ToString() : (Convert.ToBoolean(row["IsApprovalRequired"]) ? "全域主管審核" : "免審核"),
                IsActive = Convert.ToBoolean(row["IsActive"]),
                CreateTime = Convert.ToDateTime(row["CreateTime"])
            };
        }
    }
}
