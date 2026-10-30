using System;
using System.Collections.Generic;
using System.Web.UI;
using Matome.BLL;
using Matome.Models;

namespace Matome
{
    public partial class MyBookings : Page
    {
        private readonly BookingManager _bookingMgr = new BookingManager();
        private readonly EquipmentManager _equipmentMgr = new EquipmentManager();

        public List<Booking> UserBookings { get; set; }
        public List<EquipmentBooking> UserEquipmentBookings { get; set; }
        public string CurrentFilter { get; set; }

        public int TotalCount { get; set; }
        public int UpcomingCount { get; set; }
        public int PendingCount { get; set; }
        public int CancelledCount { get; set; }

        public MyBookings()
        {
            UserBookings = new List<Booking>();
            UserEquipmentBookings = new List<EquipmentBooking>();
            CurrentFilter = "all";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var user = AuthHelper.GetCurrentUser();
            if (user == null)
            {
                Response.Redirect("~/Login.aspx");
                return;
            }

            CurrentFilter = Request.QueryString["filter"] ?? "all";

            var all = _bookingMgr.GetUserBookings(user.UserId, "all");
            var allEquip = _equipmentMgr.GetUserEquipmentBookings(user.UserId, "all");

            TotalCount = all.Count + allEquip.Count;
            UpcomingCount = all.FindAll(b => b.EndTime >= DateTime.Now && (b.Status == "Approved" || b.Status == "Pending")).Count +
                            allEquip.FindAll(eq => eq.EndTime >= DateTime.Now && (eq.Status == "Approved" || eq.Status == "Pending")).Count;
            PendingCount = all.FindAll(b => b.Status == "Pending").Count +
                           allEquip.FindAll(eq => eq.Status == "Pending").Count;
            CancelledCount = all.FindAll(b => b.Status == "Cancelled" || b.Status == "Rejected").Count +
                             allEquip.FindAll(eq => eq.Status == "Cancelled" || eq.Status == "Rejected").Count;

            UserBookings = _bookingMgr.GetUserBookings(user.UserId, CurrentFilter);
            UserEquipmentBookings = _equipmentMgr.GetUserEquipmentBookings(user.UserId, CurrentFilter);
        }
    }
}
