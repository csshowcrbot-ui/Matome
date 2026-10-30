using System;
using System.Collections.Generic;
using System.Web.UI;
using Matome.BLL;
using Matome.Models;

namespace Matome.Approval
{
    public partial class PendingList : Page
    {
        private readonly ApprovalManager _approvalMgr = new ApprovalManager();
        public List<Booking> PendingListItems { get; set; }

        public PendingList()
        {
            PendingListItems = new List<Booking>();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var user = AuthHelper.GetCurrentUser();
            if (user == null)
            {
                Response.Redirect("~/Login.aspx");
                return;
            }

            if (!string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) && 
                !string.Equals(user.Role, "Approver", StringComparison.OrdinalIgnoreCase))
            {
                Response.Redirect("~/Default.aspx");
                return;
            }

            PendingListItems = _approvalMgr.GetPendingBookingsForUser(user);
        }
    }
}
