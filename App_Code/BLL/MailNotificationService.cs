using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Npgsql;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Matome.DAL;
using Matome.Models;

namespace Matome.BLL
{
    public static class MailNotificationService
    {
        private static readonly string[] WeekDayNames = { "日", "一", "二", "三", "四", "五", "六" };

        public static string SmtpHost
        {
            get
            {
                string host = ConfigurationManager.AppSettings["SmtpHost"];
                return !string.IsNullOrEmpty(host) ? host : "mail.csshow.org.tw";
            }
        }

        public static int SmtpPort
        {
            get
            {
                int port;
                if (int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out port) && port > 0)
                {
                    return port;
                }
                return 25;
            }
        }

        public static string SmtpFrom
        {
            get
            {
                string from = ConfigurationManager.AppSettings["SmtpFrom"];
                return !string.IsNullOrEmpty(from) ? from : "CR@csshow.org.tw";
            }
        }

        public static string SmtpFromName
        {
            get
            {
                string name = ConfigurationManager.AppSettings["SmtpFromName"];
                return !string.IsNullOrEmpty(name) ? name : "間調 (Matome) 會議室借用系統";
            }
        }

        /// <summary>
        /// 取得通知收件人 Email 清單（依管轄審核單位篩選對應之審核主管與管理員）
        /// </summary>
        public static List<string> GetApproverEmails(int? approvalDeptId)
        {
            var emails = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string sql;
            List<NpgsqlParameter> parameters = new List<NpgsqlParameter>();

            if (approvalDeptId.HasValue && approvalDeptId.Value > 0)
            {
                // 優先取得該部門之審核主管 (Approver) 與該部門管理員 (Admin)
                sql = @"
                    SELECT Email, FullName, Role, DeptId 
                    FROM MatomeUsers 
                    WHERE IsActive = TRUE 
                      AND Email IS NOT NULL 
                      AND LENGTH(TRIM(Email)) > 0
                      AND DeptId = @DeptId
                      AND Role IN ('Approver', 'Admin');";
                parameters.Add(new NpgsqlParameter("@DeptId", approvalDeptId.Value));
            }
            else
            {
                // 未指定特定單位（全域審核），通知所有主管與系統管理員
                sql = @"
                    SELECT Email, FullName, Role, DeptId 
                    FROM MatomeUsers 
                    WHERE IsActive = TRUE 
                      AND Email IS NOT NULL 
                      AND LENGTH(TRIM(Email)) > 0
                      AND Role IN ('Approver', 'Admin');";
            }

            var dt = SqlHelper.ExecuteDataTable(sql, parameters.ToArray());
            foreach (DataRow row in dt.Rows)
            {
                string email = row["Email"].ToString().Trim();
                if (IsValidEmail(email) && seen.Add(email))
                {
                    emails.Add(email);
                }
            }

            // 若該指定部門尚無任何設定信箱的主管，回退通知所有活躍系統管理員 (Admin) 作為安全防呆
            if (emails.Count == 0 && approvalDeptId.HasValue)
            {
                string fallbackSql = @"
                    SELECT Email FROM MatomeUsers 
                    WHERE IsActive = TRUE 
                      AND Email IS NOT NULL 
                      AND LENGTH(TRIM(Email)) > 0
                      AND Role = 'Admin';";
                var dtFallback = SqlHelper.ExecuteDataTable(fallbackSql);
                foreach (DataRow row in dtFallback.Rows)
                {
                    string email = row["Email"].ToString().Trim();
                    if (IsValidEmail(email) && seen.Add(email))
                    {
                        emails.Add(email);
                    }
                }
            }

            return emails;
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            int atIdx = email.IndexOf('@');
            int dotIdx = email.LastIndexOf('.');
            return atIdx > 0 && dotIdx > atIdx + 1 && dotIdx < email.Length - 1;
        }

        /// <summary>
        /// 非同步發送：單次會議室預約之審核單位通知信
        /// </summary>
        public static void SendPendingReviewNotificationAsync(int bookingId, string baseUrl)
        {
            Action work = () =>
            {
                try
                {
                    SendPendingReviewNotificationInternal(bookingId, baseUrl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("發送審核通知郵件失敗 (BookingId: {0}): {1}", bookingId, ex);
                }
            };

            if (HostingEnvironment.IsHosted)
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct => work());
            }
            else
            {
                Task.Run(work);
            }
        }

        /// <summary>
        /// 非同步發送：批次定期會議預約之審核單位彙總通知信
        /// </summary>
        public static void SendRecurringPendingReviewNotificationAsync(List<int> bookingIds, string baseUrl)
        {
            if (bookingIds == null || bookingIds.Count == 0) return;

            Action work = () =>
            {
                try
                {
                    SendRecurringPendingReviewNotificationInternal(bookingIds, baseUrl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("發送定期審核通知郵件失敗 (BookingCount: {0}): {1}", bookingIds.Count, ex);
                }
            };

            if (HostingEnvironment.IsHosted)
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct => work());
            }
            else
            {
                Task.Run(work);
            }
        }

        private static void SendPendingReviewNotificationInternal(int bookingId, string baseUrl)
        {
            var bookingMgr = new BookingManager();
            var booking = bookingMgr.GetBookingById(bookingId);
            if (booking == null || !string.Equals(booking.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var roomMgr = new RoomManager();
            var room = roomMgr.GetRoomById(booking.RoomId);
            int? approvalDeptId = room != null ? room.ApprovalDeptId : null;
            string approvalDeptName = room != null && !string.IsNullOrEmpty(room.ApprovalDeptName) ? room.ApprovalDeptName : "全域主管審核";

            var recipients = GetApproverEmails(approvalDeptId);
            if (recipients.Count == 0)
            {
                return;
            }

            string subject = string.Format("【間調 Matome】會議室借用待審核通知 - {0}（申請人：{1}）", booking.RoomName, booking.UserName);
            string body = BuildSingleBookingHtmlBody(booking, room, approvalDeptName, baseUrl);

            SendMail(recipients, subject, body);
        }

        private static void SendRecurringPendingReviewNotificationInternal(List<int> bookingIds, string baseUrl)
        {
            var bookingMgr = new BookingManager();
            var firstBooking = bookingMgr.GetBookingById(bookingIds[0]);
            if (firstBooking == null || !string.Equals(firstBooking.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var roomMgr = new RoomManager();
            var room = roomMgr.GetRoomById(firstBooking.RoomId);
            int? approvalDeptId = room != null ? room.ApprovalDeptId : null;
            string approvalDeptName = room != null && !string.IsNullOrEmpty(room.ApprovalDeptName) ? room.ApprovalDeptName : "全域主管審核";

            var recipients = GetApproverEmails(approvalDeptId);
            if (recipients.Count == 0)
            {
                return;
            }

            // 取得最後一筆預約以計算跨週迄日
            var lastBooking = bookingIds.Count > 1 ? bookingMgr.GetBookingById(bookingIds[bookingIds.Count - 1]) : firstBooking;

            string subject = string.Format("【間調 Matome】每週定期會議室借用待審核通知 - {0}（共 {1} 場，申請人：{2}）", firstBooking.RoomName, bookingIds.Count, firstBooking.UserName);
            string body = BuildRecurringBookingHtmlBody(firstBooking, lastBooking, bookingIds.Count, room, approvalDeptName, baseUrl);

            SendMail(recipients, subject, body);
        }

        private static void SendMail(List<string> toList, string subject, string htmlBody)
        {
            if (toList == null || toList.Count == 0) return;

            using (var smtp = new SmtpClient(SmtpHost, SmtpPort))
            {
                smtp.Timeout = 10000;

                using (var msg = new MailMessage())
                {
                    msg.From = new MailAddress(SmtpFrom, SmtpFromName, Encoding.UTF8);
                    foreach (var to in toList)
                    {
                        msg.To.Add(to);
                    }

                    msg.Subject = subject;
                    msg.SubjectEncoding = Encoding.UTF8;
                    msg.Body = htmlBody;
                    msg.BodyEncoding = Encoding.UTF8;
                    msg.IsBodyHtml = true;

                    smtp.Send(msg);
                }
            }
        }

        private static string FormatDateTime(DateTime dt)
        {
            string wName = WeekDayNames[(int)dt.DayOfWeek];
            return string.Format("{0:yyyy/MM/dd} ({1}) {0:HH:mm}", dt, wName);
        }

        private static string FormatEquipmentsSummary(List<BookingEquipment> equips)
        {
            if (equips == null || equips.Count == 0)
            {
                return "無附加借用設備";
            }

            var parts = new List<string>();
            foreach (var e in equips)
            {
                parts.Add(string.Format("{0} × {1}", e.EquipmentName, e.Quantity));
            }
            return string.Join("、", parts.ToArray());
        }

        private static string BuildSingleBookingHtmlBody(Booking booking, Room room, string approvalDeptName, string baseUrl)
        {
            string approvalUrl = !string.IsNullOrEmpty(baseUrl) 
                ? baseUrl.TrimEnd('/') + "/Approval/PendingList.aspx" 
                : "http://localhost:51888/Approval/PendingList.aspx";

            string sTimeStr = FormatDateTime(booking.StartTime);
            string eTimeStr = string.Format("{0:HH:mm}", booking.EndTime);
            string equipStr = FormatEquipmentsSummary(booking.Equipments);
            string locStr = room != null && !string.IsNullOrEmpty(room.Location) ? room.Location : "未註記";
            int cap = room != null ? room.Capacity : 0;

            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head><meta charset='utf-8'/><title>會議室借用審核通知</title></head>");
            sb.Append("<body style='margin:0; padding:24px 0; background-color:#F5F3EF; font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", \"Noto Sans TC\", Roboto, Helvetica, Arial, sans-serif; color:#2B2B2B;'>");
            sb.Append("<div style='max-width:620px; margin:0 auto; background-color:#FFFFFF; border:1px solid #E6E4DF; border-radius:8px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.04);'>");
            
            // Header
            sb.Append("<div style='background-color:#6B4E3D; padding:24px 30px; color:#FFFFFF;'>");
            sb.Append("<div style='display:inline-block; background-color:rgba(255,255,255,0.2); padding:3px 10px; border-radius:12px; font-size:12px; font-weight:600; letter-spacing:0.05em; margin-bottom:8px;'>間調 (Matome) · 會議室借用審核</div>");
            sb.Append("<h1 style='margin:0; font-size:20px; font-weight:600; color:#FFFFFF;'>新會議室借用申請待審核通知</h1>");
            sb.Append("<div style='margin-top:6px; font-size:13px; color:rgba(255,255,255,0.85);'>同仁已送出借用登記，請撥冗至審核工作台進行查核批示。</div>");
            sb.Append("</div>");

            // Content
            sb.Append("<div style='padding:28px 30px;'>");

            // Alert Box
            sb.Append("<div style='background-color:#FBF6EB; border:1px solid #EEDFBA; border-radius:6px; padding:12px 16px; margin-bottom:20px; font-size:13px; color:#8F6F20;'>");
            sb.Append("<strong>審核指派提示：</strong>此會議室設定需經由【<strong>" + HttpUtility.HtmlEncode(approvalDeptName) + "</strong>】審批核准，始得正式保留使用。");
            sb.Append("</div>");

            // Info Table
            sb.Append("<table style='width:100%; border-collapse:collapse; font-size:14px; line-height:1.6;'>");
            sb.Append("<tr><td style='width:130px; padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>預約單號</td><td style='padding:9px 0; font-family:monospace; font-weight:bold; color:#6B4E3D; border-bottom:1px solid #F0EEE9;'>#" + booking.BookingId + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>會議事由 / 主題</td><td style='padding:9px 0; font-weight:600; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(booking.Subject) + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>申請會議室</td><td style='padding:9px 0; font-weight:500; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(booking.RoomName) + " <span style='font-size:12px; color:#787571;'>（" + HttpUtility.HtmlEncode(locStr) + " · 容納 " + cap + " 人）</span></td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>借用預約時段</td><td style='padding:9px 0; font-weight:600; color:#6B4E3D; border-bottom:1px solid #F0EEE9;'>" + sTimeStr + " ～ " + eTimeStr + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>附加借用設備</td><td style='padding:9px 0; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(equipStr) + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>管轄審核單位</td><td style='padding:9px 0; font-weight:500; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(approvalDeptName) + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>申請人員</td><td style='padding:9px 0; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(booking.UserName) + " <span style='font-size:12px; color:#787571;'>（" + HttpUtility.HtmlEncode(booking.UserDeptName) + "）</span></td></tr>");
            if (!string.IsNullOrEmpty(booking.UserEmail))
            {
                sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>申請人聯絡信箱</td><td style='padding:9px 0; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'><a href='mailto:" + HttpUtility.HtmlAttributeEncode(booking.UserEmail) + "' style='color:#6B4E3D; text-decoration:none;'>" + HttpUtility.HtmlEncode(booking.UserEmail) + "</a></td></tr>");
            }
            sb.Append("<tr><td style='padding:9px 0; color:#787571;'>申請送出時間</td><td style='padding:9px 0; color:#787571;'>" + FormatDateTime(booking.CreateTime) + "</td></tr>");
            sb.Append("</table>");

            // Button
            sb.Append("<div style='margin-top:28px; text-align:center;'>");
            sb.Append("<a href='" + HttpUtility.HtmlAttributeEncode(approvalUrl) + "' target='_blank' style='display:inline-block; background-color:#6B4E3D; color:#FFFFFF; text-decoration:none; font-size:15px; font-weight:600; padding:12px 28px; border-radius:6px; box-shadow:0 2px 4px rgba(107,78,61,0.25);'>前往審核工作台處理</a>");
            sb.Append("<div style='margin-top:10px; font-size:12px; color:#A09D98;'>若無法點擊按鈕，請複製連結至瀏覽器開啟：<br/><a href='" + HttpUtility.HtmlAttributeEncode(approvalUrl) + "' style='color:#6B4E3D; word-break:break-all;'>" + HttpUtility.HtmlEncode(approvalUrl) + "</a></div>");
            sb.Append("</div>");

            sb.Append("</div>");

            // Footer
            sb.Append("<div style='background-color:#FAF9F6; border-top:1px solid #E6E4DF; padding:18px 30px; font-size:12px; color:#A09D98; text-align:center;'>");
            sb.Append("此信件為【間調 (Matome) 會議室及設備借用系統】自動發送之重要通知，請勿直接回覆。");
            sb.Append("</div>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        private static string BuildRecurringBookingHtmlBody(Booking firstBooking, Booking lastBooking, int count, Room room, string approvalDeptName, string baseUrl)
        {
            string approvalUrl = !string.IsNullOrEmpty(baseUrl) 
                ? baseUrl.TrimEnd('/') + "/Approval/PendingList.aspx" 
                : "http://localhost:51888/Approval/PendingList.aspx";

            string sTimeStr = FormatDateTime(firstBooking.StartTime);
            string eTimeStr = string.Format("{0:HH:mm}", firstBooking.EndTime);
            string lastDateStr = string.Format("{0:yyyy/MM/dd}", lastBooking.EndTime);
            string equipStr = FormatEquipmentsSummary(firstBooking.Equipments);
            string locStr = room != null && !string.IsNullOrEmpty(room.Location) ? room.Location : "未註記";
            int cap = room != null ? room.Capacity : 0;

            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head><meta charset='utf-8'/><title>每週定期會議室借用審核通知</title></head>");
            sb.Append("<body style='margin:0; padding:24px 0; background-color:#F5F3EF; font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", \"Noto Sans TC\", Roboto, Helvetica, Arial, sans-serif; color:#2B2B2B;'>");
            sb.Append("<div style='max-width:620px; margin:0 auto; background-color:#FFFFFF; border:1px solid #E6E4DF; border-radius:8px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.04);'>");
            
            // Header
            sb.Append("<div style='background-color:#6B4E3D; padding:24px 30px; color:#FFFFFF;'>");
            sb.Append("<div style='display:inline-block; background-color:rgba(255,255,255,0.2); padding:3px 10px; border-radius:12px; font-size:12px; font-weight:600; letter-spacing:0.05em; margin-bottom:8px;'>間調 (Matome) · 每週定期會議審核</div>");
            sb.Append("<h1 style='margin:0; font-size:20px; font-weight:600; color:#FFFFFF;'>每週定期會議借用申請待審核通知</h1>");
            sb.Append("<div style='margin-top:6px; font-size:13px; color:rgba(255,255,255,0.85);'>同仁已送出每週定期借用登記（共 " + count + " 場次），請撥冗進行審核。</div>");
            sb.Append("</div>");

            // Content
            sb.Append("<div style='padding:28px 30px;'>");

            // Alert Box
            sb.Append("<div style='background-color:#FBF6EB; border:1px solid #EEDFBA; border-radius:6px; padding:12px 16px; margin-bottom:20px; font-size:13px; color:#8F6F20;'>");
            sb.Append("<strong>定期審核提示：</strong>申請人預約了每週固定時段之定期會議，全數共 <strong>" + count + " 場次</strong>，需經由【<strong>" + HttpUtility.HtmlEncode(approvalDeptName) + "</strong>】審查。");
            sb.Append("</div>");

            // Info Table
            sb.Append("<table style='width:100%; border-collapse:collapse; font-size:14px; line-height:1.6;'>");
            sb.Append("<tr><td style='width:130px; padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>會議性質</td><td style='padding:9px 0; font-weight:bold; color:#6B4E3D; border-bottom:1px solid #F0EEE9;'>每週定期會議（共 " + count + " 場次）</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>會議事由 / 主題</td><td style='padding:9px 0; font-weight:600; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(firstBooking.Subject) + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>申請會議室</td><td style='padding:9px 0; font-weight:500; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(firstBooking.RoomName) + " <span style='font-size:12px; color:#787571;'>（" + HttpUtility.HtmlEncode(locStr) + " · 容納 " + cap + " 人）</span></td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>首場預約時段</td><td style='padding:9px 0; font-weight:600; color:#6B4E3D; border-bottom:1px solid #F0EEE9;'>" + sTimeStr + " ～ " + eTimeStr + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>每週重複區間</td><td style='padding:9px 0; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>每週固定時段，持續至 " + lastDateStr + " 止</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>管轄審核單位</td><td style='padding:9px 0; font-weight:500; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(approvalDeptName) + "</td></tr>");
            sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>申請人員</td><td style='padding:9px 0; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'>" + HttpUtility.HtmlEncode(firstBooking.UserName) + " <span style='font-size:12px; color:#787571;'>（" + HttpUtility.HtmlEncode(firstBooking.UserDeptName) + "）</span></td></tr>");
            if (!string.IsNullOrEmpty(firstBooking.UserEmail))
            {
                sb.Append("<tr><td style='padding:9px 0; color:#787571; border-bottom:1px solid #F0EEE9;'>申請人聯絡信箱</td><td style='padding:9px 0; color:#2B2B2B; border-bottom:1px solid #F0EEE9;'><a href='mailto:" + HttpUtility.HtmlAttributeEncode(firstBooking.UserEmail) + "' style='color:#6B4E3D; text-decoration:none;'>" + HttpUtility.HtmlEncode(firstBooking.UserEmail) + "</a></td></tr>");
            }
            sb.Append("<tr><td style='padding:9px 0; color:#787571;'>申請送出時間</td><td style='padding:9px 0; color:#787571;'>" + FormatDateTime(firstBooking.CreateTime) + "</td></tr>");
            sb.Append("</table>");

            // Button
            sb.Append("<div style='margin-top:28px; text-align:center;'>");
            sb.Append("<a href='" + HttpUtility.HtmlAttributeEncode(approvalUrl) + "' target='_blank' style='display:inline-block; background-color:#6B4E3D; color:#FFFFFF; text-decoration:none; font-size:15px; font-weight:600; padding:12px 28px; border-radius:6px; box-shadow:0 2px 4px rgba(107,78,61,0.25);'>前往審核工作台簽核</a>");
            sb.Append("<div style='margin-top:10px; font-size:12px; color:#A09D98;'>若無法點擊按鈕，請複製連結至瀏覽器開啟：<br/><a href='" + HttpUtility.HtmlAttributeEncode(approvalUrl) + "' style='color:#6B4E3D; word-break:break-all;'>" + HttpUtility.HtmlEncode(approvalUrl) + "</a></div>");
            sb.Append("</div>");

            sb.Append("</div>");

            // Footer
            sb.Append("<div style='background-color:#FAF9F6; border-top:1px solid #E6E4DF; padding:18px 30px; font-size:12px; color:#A09D98; text-align:center;'>");
            sb.Append("此信件為【間調 (Matome) 會議室及設備借用系統】自動發送之重要通知，請勿直接回覆。");
            sb.Append("</div>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }
    }
}
