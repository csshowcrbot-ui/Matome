using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Script.Serialization;
using Matome.BLL;

namespace Matome
{
    public partial class EquipmentBookingPage : Page
    {
        private readonly EquipmentManager _equipmentMgr = new EquipmentManager();
        public string EquipmentsJson { get; set; }

        public EquipmentBookingPage()
        {
            EquipmentsJson = "[]";
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
                LoadEquipments();
            }
        }

        private void LoadEquipments()
        {
            var equipments = _equipmentMgr.GetAllEquipments(true);
            ddlEquipments.Items.Clear();

            foreach (var eq in equipments)
            {
                var item = new ListItem(string.Format("{0} [總庫存: {1}]", eq.EquipmentName, eq.TotalQuantity), eq.EquipmentId.ToString());
                ddlEquipments.Items.Add(item);
            }

            var serializer = new JavaScriptSerializer();
            EquipmentsJson = serializer.Serialize(equipments);
        }
    }
}

