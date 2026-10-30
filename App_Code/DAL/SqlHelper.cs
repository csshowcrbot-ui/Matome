using System;
using System.Configuration;
using System.Data;
using Npgsql;

namespace Matome.DAL
{
    /// <summary>
    /// PostgreSQL 資料庫存取協助類別 (支援 Railway 雲端環境變數自動偵測)
    /// </summary>
    public static class SqlHelper
    {
        public static string ConnectionString
        {
            get
            {
                // 1. 優先檢查 Railway 常見的 DATABASE_URL 環境變數
                string databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
                if (!string.IsNullOrWhiteSpace(databaseUrl))
                {
                    return ConvertDatabaseUrlToNpgsql(databaseUrl);
                }

                // 2. 檢查個別環境變數 (PGHOST, PGPORT, etc.)
                string pgHost = Environment.GetEnvironmentVariable("PGHOST");
                if (!string.IsNullOrWhiteSpace(pgHost))
                {
                    string pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
                    string pgUser = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
                    string pgPass = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "";
                    string pgDb = Environment.GetEnvironmentVariable("PGDATABASE") ?? "railway";
                    return string.Format("Host={0};Port={1};Database={2};Username={3};Password={4};SSL Mode=Prefer;Trust Server Certificate=true;", pgHost, pgPort, pgDb, pgUser, pgPass);
                }

                // 3. 讀取 Web.config 內的 DefaultConnection 設定
                var connSetting = ConfigurationManager.ConnectionStrings["DefaultConnection"];
                if (connSetting != null && !string.IsNullOrEmpty(connSetting.ConnectionString))
                {
                    string connStr = connSetting.ConnectionString;
                    if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                        connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                    {
                        return ConvertDatabaseUrlToNpgsql(connStr);
                    }
                    return connStr;
                }

                // 4. 本機開發預設值
                return "Host=localhost;Port=5432;Database=MatomeDb;Username=postgres;Password=postgres;SSL Mode=Prefer;";
            }
        }

        /// <summary>
        /// 將 postgres:// 或 postgresql:// 網址格式轉換為 Npgsql 連線字串
        /// </summary>
        private static string ConvertDatabaseUrlToNpgsql(string databaseUrl)
        {
            try
            {
                Uri uri = new Uri(databaseUrl);
                string host = uri.Host;
                int port = uri.Port > 0 ? uri.Port : 5432;
                string database = uri.AbsolutePath.TrimStart('/');
                string username = "postgres";
                string password = "";

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    string[] userInfo = uri.UserInfo.Split(':');
                    if (userInfo.Length > 0) username = Uri.UnescapeDataString(userInfo[0]);
                    if (userInfo.Length > 1) password = Uri.UnescapeDataString(userInfo[1]);
                }

                return string.Format("Host={0};Port={1};Database={2};Username={3};Password={4};SSL Mode=Prefer;Trust Server Certificate=true;",
                    host, port, database, username, password);
            }
            catch
            {
                return databaseUrl;
            }
        }

        public static NpgsqlConnection GetConnection()
        {
            var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static int ExecuteNonQuery(string cmdText, NpgsqlParameter[] parameters = null, NpgsqlTransaction trans = null)
        {
            if (trans != null)
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand(cmdText, trans.Connection, trans))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteNonQuery();
                }
            }
            else
            {
                using (NpgsqlConnection conn = GetConnection())
                using (NpgsqlCommand cmd = new NpgsqlCommand(cmdText, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public static object ExecuteScalar(string cmdText, NpgsqlParameter[] parameters = null, NpgsqlTransaction trans = null)
        {
            if (trans != null)
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand(cmdText, trans.Connection, trans))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteScalar();
                }
            }
            else
            {
                using (NpgsqlConnection conn = GetConnection())
                using (NpgsqlCommand cmd = new NpgsqlCommand(cmdText, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteScalar();
                }
            }
        }

        public static DataTable ExecuteDataTable(string cmdText, NpgsqlParameter[] parameters = null, NpgsqlTransaction trans = null)
        {
            DataTable dt = new DataTable();
            if (trans != null)
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand(cmdText, trans.Connection, trans))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    using (NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }
            else
            {
                using (NpgsqlConnection conn = GetConnection())
                using (NpgsqlCommand cmd = new NpgsqlCommand(cmdText, conn))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    using (NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }
            return dt;
        }
    }
}
