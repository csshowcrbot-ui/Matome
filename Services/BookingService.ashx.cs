using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using System.Web.SessionState;
using System.Web.Script.Serialization;
using Matome.BLL;
using Matome.Models;

namespace Matome.Services
{
    public class BookingService : IHttpHandler, IRequiresSessionState
    {
        private readonly BookingManager _bookingMgr = new BookingManager();
        private readonly RoomManager _roomMgr = new RoomManager();
        private readonly EquipmentManager _equipmentMgr = new EquipmentManager();
        private readonly ApprovalManager _approvalMgr = new ApprovalManager();
        private readonly UserManager _userMgr = new UserManager();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            string action = context.Request["action"] ?? "";

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "get_events":
                        HandleGetEvents(context);
                        break;
                    case "get_equipments":
                        HandleGetEquipments(context);
                        break;
                    case "create_booking":
                        HandleCreateBooking(context);
                        break;
                    case "cancel_booking":
                        HandleCancelBooking(context);
                        break;
                    case "approve_booking":
                        HandleApproveBooking(context);
                        break;
                    case "reject_booking":
                        HandleRejectBooking(context);
                        break;
                    case "switch_demo_user":
                        HandleSwitchDemoUser(context);
                        break;
                    case "get_equipment_events":
                        HandleGetEquipmentEvents(context);
                        break;
                    case "get_equipment_slot_avail":
                        HandleGetEquipmentSlotAvail(context);
                        break;
                    case "create_equipment_booking":
                        HandleCreateEquipmentBooking(context);
                        break;
                    case "cancel_equipment_booking":
                        HandleCancelEquipmentBooking(context);
                        break;
                    case "get_departments":
                        HandleGetDepartments(context);
                        break;
                    case "add_department":
                        HandleAddDepartment(context);
                        break;
                    case "delete_department":
                        HandleDeleteDepartment(context);
                        break;
                    case "delete_user":
                        HandleDeleteUser(context);
                        break;
                    case "delete_room":
                        HandleDeleteRoom(context);
                        break;
                    case "delete_equipment":
                        HandleDeleteEquipment(context);
                        break;
                    default:
                        SendResponse(context, false, "無效的 Action 操作端點。");
                        break;
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null)
                {
                    msg += " (" + ex.InnerException.Message + ")";
                }
                SendResponse(context, false, "伺服器內部發生異常：" + msg);
            }
        }

        private void HandleGetEvents(HttpContext context)
        {
            int roomId = 0;
            int.TryParse(context.Request["roomId"], out roomId);
            if (roomId <= 0) roomId = 1;

            DateTime start, end;
            if (!DateTime.TryParse(context.Request["start"], out start))
            {
                start = DateTime.Today.AddDays(-7);
            }
            if (!DateTime.TryParse(context.Request["end"], out end))
            {
                end = DateTime.Today.AddDays(14);
            }

            var currentUser = AuthHelper.GetCurrentUser();
            int currentUserId = currentUser != null ? currentUser.UserId : 0;

            var events = _bookingMgr.GetCalendarEvents(roomId, start, end, currentUserId);
            string jsonResult = _json.Serialize(events);
            context.Response.Write(jsonResult);
        }

        private void HandleGetEquipments(HttpContext context)
        {
            DateTime startTime, endTime;
            if (!DateTime.TryParse(context.Request["startTime"], out startTime) ||
                !DateTime.TryParse(context.Request["endTime"], out endTime))
            {
                SendResponse(context, false, "時段參數格式不正確。");
                return;
            }

            int? excludeId = null;
            int exId;
            if (int.TryParse(context.Request["excludeBookingId"], out exId))
            {
                excludeId = exId;
            }

            var list = _equipmentMgr.GetAvailableEquipments(startTime, endTime, excludeId);
            context.Response.Write(_json.Serialize(list));
        }

        private void HandleCreateBooking(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null)
            {
                SendResponse(context, false, "請先登入後再進行預約。");
                return;
            }

            string requestBody;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            CreateBookingPayload payload = null;
            if (!string.IsNullOrEmpty(requestBody))
            {
                payload = _json.Deserialize<CreateBookingPayload>(requestBody);
            }

            if (payload == null)
            {
                int roomId = 0;
                int.TryParse(context.Request["roomId"], out roomId);
                payload = new CreateBookingPayload {
                    roomId = roomId,
                    subject = context.Request["subject"],
                    startTime = context.Request["startTime"],
                    endTime = context.Request["endTime"]
                };
            }

            if (payload == null || payload.roomId <= 0 || string.IsNullOrEmpty(payload.startTime) || string.IsNullOrEmpty(payload.endTime))
            {
                SendResponse(context, false, "請完整填寫會議室與起訖預約時段。");
                return;
            }

            DateTime sTime, eTime;
            if (!DateTime.TryParse(payload.startTime, out sTime) || !DateTime.TryParse(payload.endTime, out eTime))
            {
                SendResponse(context, false, "預約時段格式不合法。");
                return;
            }

            // 處理每週同一時段定期預約 (選取日期區間)
            if (payload.isRecurring && !string.IsNullOrEmpty(payload.recurringEndDate))
            {
                DateTime recEndDate;
                if (!DateTime.TryParse(payload.recurringEndDate, out recEndDate))
                {
                    SendResponse(context, false, "定期預約的迄止日期格式不正確。");
                    return;
                }

                string recError;
                List<int> createdIds;
                bool recSuccess = _bookingMgr.CreateRecurringBookings(currentUser.UserId, payload.roomId, payload.subject, sTime, eTime, recEndDate, out recError, out createdIds);

                if (recSuccess)
                {
                    var firstBooking = _bookingMgr.GetBookingById(createdIds[0]);
                    var room = _roomMgr.GetRoomById(firstBooking.RoomId);
                    string baseUrl = "";
                    try
                    {
                        if (context.Request.Url != null)
                        {
                            baseUrl = context.Request.Url.GetLeftPart(UriPartial.Authority) + (context.Request.ApplicationPath ?? "");
                        }
                    }
                    catch { }

                    // 若為待審核狀態，以 mail 通知審核單位 (彙總通知)
                    if (string.Equals(firstBooking.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        MailNotificationService.SendRecurringPendingReviewNotificationAsync(createdIds, baseUrl);
                    }

                    string deptNotice = (room != null && !string.IsNullOrEmpty(room.ApprovalDeptName))
                        ? string.Format("【{0}】", room.ApprovalDeptName)
                        : "管轄單位";

                    string msg = firstBooking.Status == "Approved"
                        ? string.Format("🎉 成功預約 {0} 場每週定期會議！此會議室免審核，時段皆已確認保留。", createdIds.Count)
                        : string.Format("📋 已送出 {0} 場每週定期會議申請！已排入審核佇列，並已發送 Email 通知{1}審核單位進行簽核。", createdIds.Count, deptNotice);

                    var resData = new Dictionary<string, object>();
                    resData["bookingIds"] = createdIds;
                    resData["status"] = firstBooking.Status;
                    resData["count"] = createdIds.Count;

                    SendResponse(context, true, msg, resData);
                }
                else
                {
                    SendResponse(context, false, recError);
                }
                return;
            }

            List<EquipmentItem> equipments = new List<EquipmentItem>();
            if (payload.equipments != null)
            {
                foreach (var eq in payload.equipments)
                {
                    if (eq.quantity > 0)
                    {
                        equipments.Add(new EquipmentItem {
                            EquipmentId = eq.equipmentId,
                            Quantity = eq.quantity
                        });
                    }
                }
            }

            string errorMessage;
            int createdId;
            bool success = _bookingMgr.CreateBooking(currentUser.UserId, payload.roomId, payload.subject, sTime, eTime, equipments, out errorMessage, out createdId);

            if (success)
            {
                var booking = _bookingMgr.GetBookingById(createdId);
                var room = _roomMgr.GetRoomById(booking.RoomId);
                string baseUrl = "";
                try
                {
                    if (context.Request.Url != null)
                    {
                        baseUrl = context.Request.Url.GetLeftPart(UriPartial.Authority) + (context.Request.ApplicationPath ?? "");
                    }
                }
                catch { }

                // 若為待審核狀態，以 mail 通知審核單位
                if (string.Equals(booking.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    MailNotificationService.SendPendingReviewNotificationAsync(createdId, baseUrl);
                }

                string deptNotice = (room != null && !string.IsNullOrEmpty(room.ApprovalDeptName))
                    ? string.Format("【{0}】", room.ApprovalDeptName)
                    : "管轄單位";

                string msg = booking.Status == "Approved" 
                    ? "🎉 預約成功！此會議室免審核，時段已為您確認保留。" 
                    : string.Format("📋 預約申請已送出！已排入審核佇列，並已發送 Email 通知{0}審核單位進行簽核。", deptNotice);

                var resData = new Dictionary<string, object>();
                resData["bookingId"] = createdId;
                resData["status"] = booking.Status;

                SendResponse(context, true, msg, resData);
            }
            else
            {
                SendResponse(context, false, errorMessage);
            }
        }

        private void HandleCancelBooking(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null)
            {
                SendResponse(context, false, "請先登入後再進行操作。");
                return;
            }

            int bookingId;
            if (!int.TryParse(context.Request["bookingId"], out bookingId))
            {
                SendResponse(context, false, "預約識別編號不正確。");
                return;
            }

            bool isAdminOrApprover = string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(currentUser.Role, "Approver", StringComparison.OrdinalIgnoreCase);

            string errorMessage;
            bool success = _bookingMgr.CancelBooking(bookingId, currentUser.UserId, isAdminOrApprover, out errorMessage);

            if (success)
            {
                SendResponse(context, true, "預約已成功取消，時段與附帶設備額度已同步釋出。");
            }
            else
            {
                SendResponse(context, false, errorMessage);
            }
        }

        private void HandleApproveBooking(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null || (!string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) && 
                                        !string.Equals(currentUser.Role, "Approver", StringComparison.OrdinalIgnoreCase)))
            {
                SendResponse(context, false, "權限不足，無法執行審核操作。");
                return;
            }

            int bookingId;
            if (!int.TryParse(context.Request["bookingId"], out bookingId))
            {
                SendResponse(context, false, "預約識別編號不正確。");
                return;
            }

            string errorMessage;
            bool success = _approvalMgr.ApproveBooking(bookingId, currentUser.UserId, out errorMessage);

            if (success)
            {
                SendResponse(context, true, "該會議借用申請已成功核准！");
            }
            else
            {
                SendResponse(context, false, errorMessage);
            }
        }

        private void HandleRejectBooking(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null || (!string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) && 
                                        !string.Equals(currentUser.Role, "Approver", StringComparison.OrdinalIgnoreCase)))
            {
                SendResponse(context, false, "權限不足，無法執行駁回操作。");
                return;
            }

            int bookingId;
            if (!int.TryParse(context.Request["bookingId"], out bookingId))
            {
                SendResponse(context, false, "預約識別編號不正確。");
                return;
            }

            string reason = context.Request["rejectReason"];
            if (string.IsNullOrWhiteSpace(reason))
            {
                SendResponse(context, false, "請填寫具體的駁回原因。");
                return;
            }

            string errorMessage;
            bool success = _approvalMgr.RejectBooking(bookingId, currentUser.UserId, reason, out errorMessage);

            if (success)
            {
                SendResponse(context, true, "該申請已被駁回，並已更新歷程紀錄。");
            }
            else
            {
                SendResponse(context, false, errorMessage);
            }
        }

        private void HandleSwitchDemoUser(HttpContext context)
        {
            string account = context.Request["account"];
            if (string.IsNullOrEmpty(account))
            {
                SendResponse(context, false, "未指定帳號。");
                return;
            }

            var allUsers = _userMgr.GetAllUsers();
            var target = allUsers.Find(u => string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                AuthHelper.SignIn(target);
                SendResponse(context, true, string.Format("已成功切換為：{0} ({1})", target.FullName, target.Role));
            }
            else
            {
                SendResponse(context, false, "找不到指定的使用者。");
            }
        }

        private void HandleGetEquipmentEvents(HttpContext context)
        {
            int equipmentId = 0;
            int.TryParse(context.Request["equipmentId"], out equipmentId);
            if (equipmentId <= 0) equipmentId = 1;

            DateTime start, end;
            if (!DateTime.TryParse(context.Request["start"], out start))
            {
                start = DateTime.Today.AddDays(-7);
            }
            if (!DateTime.TryParse(context.Request["end"], out end))
            {
                end = DateTime.Today.AddDays(14);
            }

            var currentUser = AuthHelper.GetCurrentUser();
            int currentUserId = currentUser != null ? currentUser.UserId : 0;

            var events = _equipmentMgr.GetEquipmentCalendarEvents(equipmentId, start, end, currentUserId);
            string jsonResult = _json.Serialize(events);
            context.Response.Write(jsonResult);
        }

        private void HandleGetEquipmentSlotAvail(HttpContext context)
        {
            int equipmentId = 0;
            int.TryParse(context.Request["equipmentId"], out equipmentId);

            DateTime sTime, eTime;
            if (!DateTime.TryParse(context.Request["startTime"], out sTime) ||
                !DateTime.TryParse(context.Request["endTime"], out eTime))
            {
                SendResponse(context, false, "時段格式不正確。");
                return;
            }

            int avail = _equipmentMgr.GetEquipmentAvailableQty(equipmentId, sTime, eTime);
            SendResponse(context, true, "OK", new { availableQuantity = avail });
        }

        private void HandleCreateEquipmentBooking(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null)
            {
                SendResponse(context, false, "請先登入後再進行借用。");
                return;
            }

            string requestBody;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            CreateEquipmentBookingPayload payload = null;
            if (!string.IsNullOrEmpty(requestBody))
            {
                payload = _json.Deserialize<CreateEquipmentBookingPayload>(requestBody);
            }

            if (payload == null || payload.equipmentId <= 0 || string.IsNullOrEmpty(payload.startTime) || string.IsNullOrEmpty(payload.endTime))
            {
                SendResponse(context, false, "請完整填寫設備與起訖借用時段。");
                return;
            }

            DateTime sTime, eTime;
            if (!DateTime.TryParse(payload.startTime, out sTime) || !DateTime.TryParse(payload.endTime, out eTime))
            {
                SendResponse(context, false, "借用時段格式不合法。");
                return;
            }

            int qty = payload.quantity > 0 ? payload.quantity : 1;
            string errorMessage;
            int createdId;

            bool success = _equipmentMgr.CreateEquipmentBooking(currentUser.UserId, payload.equipmentId, payload.subject, sTime, eTime, qty, out errorMessage, out createdId);

            if (success)
            {
                SendResponse(context, true, "🎉 設備借用預約成功！時段與額度已為您確認保留。", new { bookingId = createdId });
            }
            else
            {
                SendResponse(context, false, errorMessage);
            }
        }

        private void HandleCancelEquipmentBooking(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null)
            {
                SendResponse(context, false, "請先登入後再進行操作。");
                return;
            }

            int bookingId;
            if (!int.TryParse(context.Request["bookingId"], out bookingId))
            {
                SendResponse(context, false, "預約識別編號不正確。");
                return;
            }

            bool isAdmin = string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            string errorMessage;
            bool success = _equipmentMgr.CancelEquipmentBooking(bookingId, currentUser.UserId, isAdmin, out errorMessage);

            if (success)
            {
                SendResponse(context, true, "設備借用已成功取消，庫存額度已同步釋出。");
            }
            else
            {
                SendResponse(context, false, errorMessage);
            }
        }

        private void HandleGetDepartments(HttpContext context)
        {
            var depts = _userMgr.GetDepartments();
            SendResponse(context, true, "OK", depts);
        }

        private void HandleAddDepartment(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null || !string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                SendResponse(context, false, "權限不足，僅系統管理員可新增部門單位。");
                return;
            }

            string deptName = context.Request["deptName"];
            if (string.IsNullOrWhiteSpace(deptName))
            {
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    string body = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(body))
                    {
                        var dict = _json.Deserialize<Dictionary<string, object>>(body);
                        if (dict != null && dict.ContainsKey("deptName"))
                        {
                            deptName = Convert.ToString(dict["deptName"]);
                        }
                    }
                }
            }

            string error;
            int newId;
            bool ok = _userMgr.AddDepartment(deptName, out error, out newId);
            if (ok)
            {
                SendResponse(context, true, "部門單位新增成功！", new { deptId = newId, deptName = deptName.Trim() });
            }
            else
            {
                SendResponse(context, false, error);
            }
        }

        private void HandleDeleteDepartment(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null || !string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                SendResponse(context, false, "權限不足，僅系統管理員可刪除部門單位。");
                return;
            }

            int deptId = 0;
            int.TryParse(context.Request["deptId"], out deptId);
            if (deptId <= 0)
            {
                SendResponse(context, false, "請提供欲刪除之部門編號。");
                return;
            }

            string error;
            bool ok = _userMgr.DeleteDepartment(deptId, out error);
            if (ok)
            {
                SendResponse(context, true, "部門單位已成功刪除！");
            }
            else
            {
                SendResponse(context, false, error);
            }
        }

        private void HandleDeleteUser(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null || !string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                SendResponse(context, false, "權限不足，僅系統管理員可刪除人員帳號。");
                return;
            }

            int userId = 0;
            int.TryParse(context.Request["userId"], out userId);
            if (userId <= 0)
            {
                SendResponse(context, false, "請提供欲刪除之使用者編號。");
                return;
            }

            string error;
            bool ok = _userMgr.DeleteUser(userId, currentUser.UserId, out error);
            if (ok)
            {
                SendResponse(context, true, "人員帳號已成功刪除！");
            }
            else
            {
                SendResponse(context, false, error);
            }
        }

        private void HandleDeleteRoom(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null || !string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                SendResponse(context, false, "權限不足，僅系統管理員可刪除會議室場地。");
                return;
            }

            int roomId = 0;
            int.TryParse(context.Request["roomId"], out roomId);
            if (roomId <= 0)
            {
                SendResponse(context, false, "請提供欲刪除之會議室編號。");
                return;
            }

            string error;
            bool ok = _roomMgr.DeleteRoom(roomId, out error);
            if (ok)
            {
                SendResponse(context, true, "會議室場地已成功刪除！");
            }
            else
            {
                SendResponse(context, false, error);
            }
        }

        private void HandleDeleteEquipment(HttpContext context)
        {
            var currentUser = AuthHelper.GetCurrentUser();
            if (currentUser == null || !string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                SendResponse(context, false, "權限不足，僅系統管理員可刪除設備資產。");
                return;
            }

            int equipmentId = 0;
            int.TryParse(context.Request["equipmentId"], out equipmentId);
            if (equipmentId <= 0)
            {
                SendResponse(context, false, "請提供欲刪除之設備資產編號。");
                return;
            }

            string error;
            bool ok = _equipmentMgr.DeleteEquipment(equipmentId, out error);
            if (ok)
            {
                SendResponse(context, true, "設備資產已成功刪除！");
            }
            else
            {
                SendResponse(context, false, error);
            }
        }

        private void SendResponse(HttpContext context, bool success, string message, object data = null)
        {
            var resp = new ApiResponse {
                Success = success,
                Message = message,
                Data = data
            };
            context.Response.Write(_json.Serialize(resp));
        }

        public bool IsReusable
        {
            get { return false; }
        }

        private class CreateBookingPayload
        {
            public int roomId { get; set; }
            public string subject { get; set; }
            public string startTime { get; set; }
            public string endTime { get; set; }
            public bool isRecurring { get; set; }
            public string recurringEndDate { get; set; }
            public List<EquipmentPayloadItem> equipments { get; set; }
        }

        private class EquipmentPayloadItem
        {
            public int equipmentId { get; set; }
            public int quantity { get; set; }
        }

        private class CreateEquipmentBookingPayload
        {
            public int equipmentId { get; set; }
            public string subject { get; set; }
            public string startTime { get; set; }
            public string endTime { get; set; }
            public int quantity { get; set; }
        }
    }
}
