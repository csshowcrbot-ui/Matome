using System;
using System.Collections.Generic;
using System.Web.UI;
using Matome.BLL;
using Matome.Models;

namespace Matome.Admin
{
    public partial class EquipmentManage : Page
    {
        private readonly EquipmentManager _equipmentMgr = new EquipmentManager();

        public List<Equipment> EquipmentsList { get; set; }
        public string AlertMessage { get; set; }
        public bool IsAlertError { get; set; }

        public EquipmentManage()
        {
            EquipmentsList = new List<Equipment>();
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
                    var target = _equipmentMgr.GetEquipmentById(toggleId);
                    if (target != null)
                    {
                        _equipmentMgr.ToggleEquipmentStatus(toggleId, !target.IsActive);
                        Response.Redirect("~/Admin/EquipmentManage.aspx");
                        return;
                    }
                }

                string deleteIdStr = Request.QueryString["delete"];
                int deleteId;
                if (int.TryParse(deleteIdStr, out deleteId))
                {
                    string delErr;
                    if (_equipmentMgr.DeleteEquipment(deleteId, out delErr))
                    {
                        Response.Redirect("~/Admin/EquipmentManage.aspx?msg=" + Server.UrlEncode("設備資產已成功刪除！"));
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

                LoadEquipments();
            }
            else
            {
                EquipmentsList = _equipmentMgr.GetAllEquipments(false);
            }
        }

        private void LoadEquipments()
        {
            EquipmentsList = _equipmentMgr.GetAllEquipments(false);
        }

        protected void btnSaveEquipment_Click(object sender, EventArgs e)
        {
            int eqId;
            int.TryParse(hfEquipmentId.Value, out eqId);

            string name = txtEquipmentName.Text.Trim();
            int qty;
            if (!int.TryParse(txtTotalQuantity.Text.Trim(), out qty) || qty < 0)
            {
                AlertMessage = "總庫存額度不可為負數。";
                IsAlertError = true;
                return;
            }

            bool isActive = chkIsActive.Checked;

            var eq = new Equipment {
                EquipmentId = eqId,
                EquipmentName = name,
                TotalQuantity = qty,
                IsActive = isActive
            };

            string error;
            bool success;
            if (eqId == 0)
            {
                success = _equipmentMgr.AddEquipment(eq, out error);
            }
            else
            {
                success = _equipmentMgr.UpdateEquipment(eq, out error);
            }

            if (success)
            {
                AlertMessage = eqId == 0 ? "設備資產已成功新增！" : "設備資產已成功更新！";
                IsAlertError = false;
                LoadEquipments();
            }
            else
            {
                AlertMessage = error;
                IsAlertError = true;
            }
        }
    }
}
