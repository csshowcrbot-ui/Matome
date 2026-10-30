using System;
using System.Web.UI;
using Matome.BLL;
using Matome.Models;

namespace Matome
{
    public partial class SiteMaster : MasterPage
    {
        public User CurrentUser { get; private set; }
        public bool IsApproverOrAdmin { get; private set; }
        public bool IsAdmin { get; private set; }
        public int PendingCount { get; private set; }

        protected void Page_Init(object sender, EventArgs e)
        {
            CurrentUser = AuthHelper.GetCurrentUser();

            // 若尚未登入，一律強制導向 Login.aspx 登入頁面
            if (CurrentUser == null)
            {
                Response.Redirect("~/Login.aspx", true);
                return;
            }

            IsAdmin = string.Equals(CurrentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            IsApproverOrAdmin = IsAdmin || string.Equals(CurrentUser.Role, "Approver", StringComparison.OrdinalIgnoreCase);

            if (IsApproverOrAdmin)
            {
                var approvalMgr = new ApprovalManager();
                var pendings = approvalMgr.GetPendingBookingsForUser(CurrentUser);
                PendingCount = pendings != null ? pendings.Count : 0;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected bool IsCurrentPage(string pageUrl)
        {
            string currentPath = Request.AppRelativeCurrentExecutionFilePath;
            return currentPath.IndexOf(pageUrl, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected void btnLogout_Click(object sender, EventArgs e)
        {
            AuthHelper.SignOut();
            Response.Redirect("~/Login.aspx");
        }
    }
}

