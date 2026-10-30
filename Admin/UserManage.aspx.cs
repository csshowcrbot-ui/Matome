using System;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;
using Matome.BLL;
using Matome.Models;

namespace Matome.Admin
{
    public partial class UserManage : Page
    {
        private readonly UserManager _userMgr = new UserManager();

        public List<User> UsersList { get; set; }
        public string AlertMessage { get; set; }
        public bool IsAlertError { get; set; }
        public int CurrentUserId { get; set; }

        public UserManage()
        {
            UsersList = new List<User>();
            AlertMessage = string.Empty;
            IsAlertError = false;
            CurrentUserId = 0;
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

            CurrentUserId = user.UserId;

            if (!IsPostBack)
            {
                string toggleIdStr = Request.QueryString["toggle"];
                int toggleId;
                if (int.TryParse(toggleIdStr, out toggleId))
                {
                    var target = _userMgr.GetUserById(toggleId);
                    if (target != null)
                    {
                        _userMgr.ToggleUserStatus(toggleId, !target.IsActive);
                        Response.Redirect("~/Admin/UserManage.aspx");
                        return;
                    }
                }

                string deleteIdStr = Request.QueryString["delete"];
                int deleteId;
                if (int.TryParse(deleteIdStr, out deleteId))
                {
                    string delErr;
                    if (_userMgr.DeleteUser(deleteId, user.UserId, out delErr))
                    {
                        Response.Redirect("~/Admin/UserManage.aspx?msg=" + Server.UrlEncode("人員帳號已成功刪除！"));
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
                LoadUsers();
            }
            else
            {
                UsersList = _userMgr.GetAllUsers();
            }
        }

        private void LoadDepartments()
        {
            ddlDept.Items.Clear();
            ddlDept.Items.Add(new ListItem("未指定", ""));

            var depts = _userMgr.GetDepartments();
            foreach (var d in depts)
            {
                ddlDept.Items.Add(new ListItem(d.DeptName, d.DeptId.ToString()));
            }
        }

        private void LoadUsers()
        {
            UsersList = _userMgr.GetAllUsers();
        }

        protected void btnSaveUser_Click(object sender, EventArgs e)
        {
            int userId;
            int.TryParse(hfUserId.Value, out userId);

            string account = txtAccount.Text.Trim();
            string fullName = txtFullName.Text.Trim();
            string email = txtEmail.Text.Trim();
            int? deptId = null;
            if (!string.IsNullOrEmpty(ddlDept.SelectedValue))
            {
                deptId = int.Parse(ddlDept.SelectedValue);
            }
            string role = ddlRole.SelectedValue;
            string password = txtPassword.Text;
            bool isActive = chkIsActive.Checked;

            if (userId == 0 && string.IsNullOrEmpty(password))
            {
                AlertMessage = "新增帳號時密碼為必填項目。";
                IsAlertError = true;
                return;
            }

            var targetUser = new User {
                UserId = userId,
                Account = account,
                FullName = fullName,
                Email = email,
                DeptId = deptId,
                Role = role,
                IsActive = isActive
            };

            string error;
            bool success;
            if (userId == 0)
            {
                success = _userMgr.AddUser(targetUser, password, out error);
            }
            else
            {
                success = _userMgr.UpdateUser(targetUser, string.IsNullOrEmpty(password) ? null : password, out error);
            }

            if (success)
            {
                AlertMessage = userId == 0 ? "使用者帳號已成功建立！" : "使用者帳號資料已成功更新！";
                IsAlertError = false;
                LoadUsers();
            }
            else
            {
                AlertMessage = error;
                IsAlertError = true;
            }
        }
    }
}
