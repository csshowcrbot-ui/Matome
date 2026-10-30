using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Matome.DAL;
using Matome.Models;

namespace Matome.BLL
{
    public class UserManager
    {
        public User Authenticate(string account, string password)
        {
            string hash = AuthHelper.HashPassword(password);
            string sql = @"
                SELECT u.UserId, u.Account, u.PasswordHash, u.FullName, u.DeptId, d.DeptName, u.Email, u.Role, u.IsActive
                FROM MatomeUsers u
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                WHERE u.Account = @Account AND u.IsActive = TRUE;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@Account", account)
            });

            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                string dbHash = row["PasswordHash"].ToString();
                if (string.Equals(dbHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    return MapUserFromRow(row);
                }
            }
            return null;
        }

        public User GetUserById(int userId)
        {
            string sql = @"
                SELECT u.UserId, u.Account, u.PasswordHash, u.FullName, u.DeptId, d.DeptName, u.Email, u.Role, u.IsActive
                FROM MatomeUsers u
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                WHERE u.UserId = @UserId;";

            var dt = SqlHelper.ExecuteDataTable(sql, new[] {
                new NpgsqlParameter("@UserId", userId)
            });

            if (dt.Rows.Count > 0)
            {
                return MapUserFromRow(dt.Rows[0]);
            }
            return null;
        }

        public List<User> GetAllUsers()
        {
            var list = new List<User>();
            string sql = @"
                SELECT u.UserId, u.Account, u.PasswordHash, u.FullName, u.DeptId, d.DeptName, u.Email, u.Role, u.IsActive
                FROM MatomeUsers u
                LEFT JOIN MatomeDepartments d ON u.DeptId = d.DeptId
                ORDER BY u.UserId ASC;";

            var dt = SqlHelper.ExecuteDataTable(sql);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapUserFromRow(row));
            }
            return list;
        }

        public bool AddUser(User user, string plainPassword, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(user.Account) || string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(user.FullName))
            {
                errorMessage = "帳號、密碼與全名均為必填項目。";
                return false;
            }

            string checkSql = "SELECT COUNT(1) FROM MatomeUsers WHERE Account = @Account;";
            int exists = Convert.ToInt32(SqlHelper.ExecuteScalar(checkSql, new[] { new NpgsqlParameter("@Account", user.Account) }));
            if (exists > 0)
            {
                errorMessage = "此帳號已存在，請使用其他帳號名稱。";
                return false;
            }

            string hash = AuthHelper.HashPassword(plainPassword);
            string insertSql = @"
                INSERT INTO MatomeUsers (Account, PasswordHash, FullName, DeptId, Email, Role, IsActive)
                VALUES (@Account, @PasswordHash, @FullName, @DeptId, @Email, @Role, @IsActive);";

            SqlHelper.ExecuteNonQuery(insertSql, new[] {
                new NpgsqlParameter("@Account", user.Account.Trim()),
                new NpgsqlParameter("@PasswordHash", hash),
                new NpgsqlParameter("@FullName", user.FullName.Trim()),
                new NpgsqlParameter("@DeptId", (object)user.DeptId ?? DBNull.Value),
                new NpgsqlParameter("@Email", user.Email ?? string.Empty),
                new NpgsqlParameter("@Role", user.Role ?? "User"),
                new NpgsqlParameter("@IsActive", user.IsActive)
            });
            return true;
        }

        public bool UpdateUser(User user, string newPlainPassword, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                errorMessage = "姓名不可為空。";
                return false;
            }

            if (!string.IsNullOrEmpty(newPlainPassword))
            {
                string hash = AuthHelper.HashPassword(newPlainPassword);
                string updateSql = @"
                    UPDATE MatomeUsers 
                    SET FullName = @FullName, DeptId = @DeptId, Email = @Email, Role = @Role, PasswordHash = @PasswordHash, IsActive = @IsActive
                    WHERE UserId = @UserId;";

                SqlHelper.ExecuteNonQuery(updateSql, new[] {
                    new NpgsqlParameter("@FullName", user.FullName.Trim()),
                    new NpgsqlParameter("@DeptId", (object)user.DeptId ?? DBNull.Value),
                    new NpgsqlParameter("@Email", user.Email ?? string.Empty),
                    new NpgsqlParameter("@Role", user.Role ?? "User"),
                    new NpgsqlParameter("@PasswordHash", hash),
                    new NpgsqlParameter("@IsActive", user.IsActive),
                    new NpgsqlParameter("@UserId", user.UserId)
                });
            }
            else
            {
                string updateSql = @"
                    UPDATE MatomeUsers 
                    SET FullName = @FullName, DeptId = @DeptId, Email = @Email, Role = @Role, IsActive = @IsActive
                    WHERE UserId = @UserId;";

                SqlHelper.ExecuteNonQuery(updateSql, new[] {
                    new NpgsqlParameter("@FullName", user.FullName.Trim()),
                    new NpgsqlParameter("@DeptId", (object)user.DeptId ?? DBNull.Value),
                    new NpgsqlParameter("@Email", user.Email ?? string.Empty),
                    new NpgsqlParameter("@Role", user.Role ?? "User"),
                    new NpgsqlParameter("@IsActive", user.IsActive),
                    new NpgsqlParameter("@UserId", user.UserId)
                });
            }

            return true;
        }

        public void ToggleUserStatus(int userId, bool isActive)
        {
            string sql = "UPDATE MatomeUsers SET IsActive = @IsActive WHERE UserId = @UserId;";
            SqlHelper.ExecuteNonQuery(sql, new[] {
                new NpgsqlParameter("@IsActive", isActive),
                new NpgsqlParameter("@UserId", userId)
            });
        }

        public bool DeleteUser(int userId, int currentAdminId, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (userId <= 0)
            {
                errorMessage = "請指定要刪除的使用者編號。";
                return false;
            }

            if (userId == currentAdminId)
            {
                errorMessage = "無法刪除目前正在登入操作中的管理員帳號。";
                return false;
            }

            var targetUser = GetUserById(userId);
            if (targetUser == null)
            {
                errorMessage = "找不到該使用者帳號。";
                return false;
            }

            using (NpgsqlConnection conn = SqlHelper.GetConnection())
            {
                using (NpgsqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. 若該使用者審核過其他人的預約，將審核人欄位清空以保留預約紀錄完整
                        string clearApproverSql = @"
                            UPDATE MatomeBookings SET ApprovedBy = NULL WHERE ApprovedBy = @UserId;
                            UPDATE MatomeEquipmentBookings SET ApprovedBy = NULL WHERE ApprovedBy = @UserId;";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(clearApproverSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 2. 清除該使用者的附加設備明細
                        string deleteEqDetailsSql = @"
                            DELETE FROM MatomeBookingEquipments 
                            WHERE BookingId IN (SELECT BookingId FROM MatomeBookings WHERE UserId = @UserId);";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(deleteEqDetailsSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 3. 刪除該使用者的會議預約主表記錄
                        string deleteBookingsSql = "DELETE FROM MatomeBookings WHERE UserId = @UserId;";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(deleteBookingsSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 4. 刪除該使用者的獨立設備預約主表記錄
                        string deleteEquipBookingsSql = "DELETE FROM MatomeEquipmentBookings WHERE UserId = @UserId;";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(deleteEquipBookingsSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 5. 刪除使用者主表記錄
                        string deleteUserSql = "DELETE FROM MatomeUsers WHERE UserId = @UserId;";
                        using (NpgsqlCommand cmd = new NpgsqlCommand(deleteUserSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        errorMessage = "刪除使用者帳號失敗：" + ex.Message;
                        return false;
                    }
                }
            }
        }

        public List<Department> GetDepartments()
        {
            var list = new List<Department>();
            string sql = "SELECT DeptId, DeptName FROM MatomeDepartments ORDER BY DeptId ASC;";
            var dt = SqlHelper.ExecuteDataTable(sql);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new Department {
                    DeptId = Convert.ToInt32(row["DeptId"]),
                    DeptName = row["DeptName"].ToString()
                });
            }
            return list;
        }

        public bool AddDepartment(string deptName, out string errorMessage, out int createdDeptId)
        {
            errorMessage = string.Empty;
            createdDeptId = 0;

            if (string.IsNullOrWhiteSpace(deptName))
            {
                errorMessage = "部門名稱不可為空。";
                return false;
            }

            string cleanName = deptName.Trim();

            string checkSql = "SELECT COUNT(1) FROM MatomeDepartments WHERE DeptName = @DeptName;";
            int exists = Convert.ToInt32(SqlHelper.ExecuteScalar(checkSql, new[] { new NpgsqlParameter("@DeptName", cleanName) }));
            if (exists > 0)
            {
                errorMessage = "此部門單位名稱已存在。";
                return false;
            }

            string insertSql = "INSERT INTO MatomeDepartments (DeptName) VALUES (@DeptName) RETURNING DeptId;";
            createdDeptId = Convert.ToInt32(SqlHelper.ExecuteScalar(insertSql, new[] { new NpgsqlParameter("@DeptName", cleanName) }));
            return true;
        }

        public bool UpdateDepartment(int deptId, string deptName, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(deptName))
            {
                errorMessage = "部門名稱不可為空。";
                return false;
            }

            string cleanName = deptName.Trim();

            string checkSql = "SELECT COUNT(1) FROM MatomeDepartments WHERE DeptName = @DeptName AND DeptId <> @DeptId;";
            int exists = Convert.ToInt32(SqlHelper.ExecuteScalar(checkSql, new[] {
                new NpgsqlParameter("@DeptName", cleanName),
                new NpgsqlParameter("@DeptId", deptId)
            }));
            if (exists > 0)
            {
                errorMessage = "此部門單位名稱已存在。";
                return false;
            }

            string updateSql = "UPDATE MatomeDepartments SET DeptName = @DeptName WHERE DeptId = @DeptId;";
            SqlHelper.ExecuteNonQuery(updateSql, new[] {
                new NpgsqlParameter("@DeptName", cleanName),
                new NpgsqlParameter("@DeptId", deptId)
            });
            return true;
        }

        public bool DeleteDepartment(int deptId, out string errorMessage)
        {
            errorMessage = string.Empty;

            string userCheckSql = "SELECT COUNT(1) FROM MatomeUsers WHERE DeptId = @DeptId;";
            int userCount = Convert.ToInt32(SqlHelper.ExecuteScalar(userCheckSql, new[] { new NpgsqlParameter("@DeptId", deptId) }));
            if (userCount > 0)
            {
                errorMessage = string.Format("無法刪除：已有 {0} 位同仁隸屬於此部門單位。", userCount);
                return false;
            }

            string roomCheckSql = "SELECT COUNT(1) FROM MatomeRooms WHERE ApprovalDeptId = @DeptId;";
            int roomCount = Convert.ToInt32(SqlHelper.ExecuteScalar(roomCheckSql, new[] { new NpgsqlParameter("@DeptId", deptId) }));
            if (roomCount > 0)
            {
                errorMessage = string.Format("無法刪除：已有 {0} 間會議室指定此單位為審核管轄部門。", roomCount);
                return false;
            }

            string deleteSql = "DELETE FROM MatomeDepartments WHERE DeptId = @DeptId;";
            SqlHelper.ExecuteNonQuery(deleteSql, new[] { new NpgsqlParameter("@DeptId", deptId) });
            return true;
        }

        private User MapUserFromRow(DataRow row)
        {
            return new User {
                UserId = Convert.ToInt32(row["UserId"]),
                Account = row["Account"].ToString(),
                PasswordHash = row["PasswordHash"].ToString(),
                FullName = row["FullName"].ToString(),
                DeptId = row["DeptId"] != DBNull.Value ? (int?)Convert.ToInt32(row["DeptId"]) : null,
                DeptName = row["DeptName"] != DBNull.Value ? row["DeptName"].ToString() : "未指定",
                Email = row["Email"].ToString(),
                Role = row["Role"].ToString(),
                IsActive = Convert.ToBoolean(row["IsActive"])
            };
        }
    }
}
