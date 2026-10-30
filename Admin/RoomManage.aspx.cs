using System;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;
using Matome.BLL;
using Matome.Models;

namespace Matome.Admin
{
    public partial class RoomManage : Page
    {
        private readonly RoomManager _roomMgr = new RoomManager();
        private readonly UserManager _userMgr = new UserManager();

        public List<Room> RoomsList { get; set; }
        public string AlertMessage { get; set; }
        public bool IsAlertError { get; set; }

        public RoomManage()
        {
            RoomsList = new List<Room>();
            AlertMessage = string.Empty;
            IsAlertError = false;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var user = AuthHelper.GetCurrentUser();
            if (user == null)
            {
                Response.Redirect("~/Login.aspx");
                return;
            }
            if (!string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                Response.Redirect("~/Default.aspx");
                return;
            }

            if (!IsPostBack)
            {
                string toggleIdStr = Request.QueryString["toggle"];
                int toggleId;
                if (int.TryParse(toggleIdStr, out toggleId))
                {
                    var target = _roomMgr.GetRoomById(toggleId);
                    if (target != null)
                    {
                        _roomMgr.ToggleRoomStatus(toggleId, !target.IsActive);
                        Response.Redirect("~/Admin/RoomManage.aspx");
                        return;
                    }
                }

                string deleteIdStr = Request.QueryString["delete"];
                int deleteId;
                if (int.TryParse(deleteIdStr, out deleteId))
                {
                    string delErr;
                    if (_roomMgr.DeleteRoom(deleteId, out delErr))
                    {
                        Response.Redirect("~/Admin/RoomManage.aspx?msg=" + Server.UrlEncode("會議室場地已成功刪除！"));
                        return;
                    }
                    else
                    {
                        AlertMessage = delErr;
                        IsAlertError = true;
                    }
                }

                string msg = Request.QueryString["msg"];
                if (!string.IsNullOrEmpty(msg))
                {
                    AlertMessage = msg;
                    IsAlertError = false;
                }

                LoadDepartments();
                LoadRooms();
            }
            else
            {
                RoomsList = _roomMgr.GetAllRooms(false);
            }
        }

        private void LoadDepartments()
        {
            ddlApprovalDept.Items.Clear();
            ddlApprovalDept.Items.Add(new ListItem("不指定 (全域主管或管理員皆可審核)", ""));

            var depts = _userMgr.GetDepartments();
            foreach (var d in depts)
            {
                ddlApprovalDept.Items.Add(new ListItem(d.DeptName, d.DeptId.ToString()));
            }
        }

        private void LoadRooms()
        {
            RoomsList = _roomMgr.GetAllRooms(false);
        }

        protected void btnSaveRoom_Click(object sender, EventArgs e)
        {
            int roomId;
            int.TryParse(hfRoomId.Value, out roomId);

            string name = txtRoomName.Text.Trim();
            int capacity;
            if (!int.TryParse(txtCapacity.Text.Trim(), out capacity) || capacity <= 0)
            {
                AlertMessage = "容納人數必須為大於 0 的整數。";
                IsAlertError = true;
                return;
            }

            string location = txtLocation.Text.Trim();
            bool needApprove = chkIsApprovalRequired.Checked;
            int? deptId = null;
            string selectedDept = Request.Form[ddlApprovalDept.UniqueID] ?? ddlApprovalDept.SelectedValue;
            if (needApprove && !string.IsNullOrEmpty(selectedDept))
            {
                int pId;
                if (int.TryParse(selectedDept, out pId))
                {
                    deptId = pId;
                }
            }
            bool isActive = chkIsActive.Checked;

            var room = new Room {
                RoomId = roomId,
                RoomName = name,
                Capacity = capacity,
                Location = location,
                IsApprovalRequired = needApprove,
                ApprovalDeptId = deptId,
                IsActive = isActive
            };

            string error;
            bool success;
            if (roomId == 0)
            {
                success = _roomMgr.AddRoom(room, out error);
            }
            else
            {
                success = _roomMgr.UpdateRoom(room, out error);
            }

            if (success)
            {
                AlertMessage = roomId == 0 ? "會議室已成功新增！" : "會議室資料已成功更新！";
                IsAlertError = false;
                LoadDepartments();
                LoadRooms();
            }
            else
            {
                AlertMessage = error;
                IsAlertError = true;
            }
        }
    }
}
