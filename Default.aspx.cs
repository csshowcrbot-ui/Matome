using System;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Script.Serialization;
using Matome.BLL;
using Matome.Models;

namespace Matome
{
    public partial class Default : Page
    {
        private readonly RoomManager _roomMgr = new RoomManager();
        public string RoomsJson { get; set; }

        public Default()
        {
            RoomsJson = "[]";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var user = AuthHelper.GetCurrentUser();
            if (user == null)
            {
                Response.Redirect("~/Login.aspx");
                return;
            }

            if (!IsPostBack)
            {
                LoadRooms();
            }
        }

        private void LoadRooms()
        {
            var rooms = _roomMgr.GetAllRooms(true);
            ddlRooms.Items.Clear();

            foreach (var r in rooms)
            {
                string note = r.IsApprovalRequired ? string.Format(" (需審核-{0})", r.ApprovalDeptName) : " (免審核)";
                var item = new ListItem(string.Format("{0} [{1}人]{2}", r.RoomName, r.Capacity, note), r.RoomId.ToString());
                ddlRooms.Items.Add(item);
            }

            // 加入總覽全部場地借用選項
            ddlRooms.Items.Insert(0, new ListItem("🏢 全部會議室 (總覽所有借用)", "0"));

            var serializer = new JavaScriptSerializer();
            RoomsJson = serializer.Serialize(rooms);
        }
    }
}
