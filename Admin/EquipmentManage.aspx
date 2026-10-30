<%@ Page Title="設備資產管理" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EquipmentManage.aspx.cs" Inherits="Matome.Admin.EquipmentManage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div class="card-header-bar">
        <div>
            <h1 class="card-title">
                <span>💻</span> 會議專用設備資產庫存維護
            </h1>
            <div class="card-subtitle">
                維護各會議室共用之麥克風、投影設備、4K視訊主機總庫存量。系統將於同仁登記時進行時段即時可用量計算。
            </div>
        </div>

        <button type="button" class="btn btn-primary" onclick="openEquipmentModal();">
            <span>➕</span> 新增設備項目
        </button>
    </div>

    <!-- 訊息提示 -->
    <% if (!string.IsNullOrEmpty(AlertMessage)) { %>
        <div style="padding: 12px 16px; margin-bottom: 20px; border-radius: var(--radius-sm); <%= IsAlertError ? "background: var(--color-accent-araigaki-light); color: var(--color-accent-araigaki); border: 1px solid #E6B5B1;" : "background: var(--color-accent-matcha-light); color: var(--color-accent-matcha); border: 1px solid #D1E0D3;" %>">
            <%= AlertMessage %>
        </div>
    <% } %>

    <div class="muji-card" style="padding: 0; overflow: hidden;">
        <table class="muji-table">
            <thead>
                <tr>
                    <th style="width: 70px;">代碼</th>
                    <th>設備資產名稱</th>
                    <th style="width: 140px;">總可用庫存額度</th>
                    <th style="width: 120px;">啟用狀態</th>
                    <th style="width: 190px; text-align: center;">操作</th>
                </tr>
            </thead>
            <tbody>
                <% foreach (var eq in EquipmentsList) { %>
                    <tr>
                        <td style="color: var(--color-text-secondary); font-family: monospace;">#<%= eq.EquipmentId %></td>
                        <td>
                            <strong style="color: var(--color-text-primary); font-size: 14px;"><%= eq.EquipmentName %></strong>
                        </td>
                        <td>
                            <span style="font-weight: 600; color: var(--color-brand-primary); font-size: 15px;"><%= eq.TotalQuantity %></span> 組/套
                        </td>
                        <td>
                            <% if (eq.IsActive) { %>
                                <span class="badge badge-approved">正常可用</span>
                            <% } else { %>
                                <span class="badge badge-cancelled">維護/停用中</span>
                            <% } %>
                        </td>
                        <td style="text-align: center; white-space: nowrap;">
                            <button type="button" class="btn btn-secondary btn-sm" 
                                    onclick="editEquipment(<%= eq.EquipmentId %>, '<%= HttpUtility.JavaScriptStringEncode(eq.EquipmentName) %>', <%= eq.TotalQuantity %>, <%= eq.IsActive ? "true" : "false" %>);">
                                編輯
                            </button>

                            <a href="EquipmentManage.aspx?toggle=<%= eq.EquipmentId %>" class="btn btn-secondary btn-sm" onclick="return confirm('確定切換此設備之可用狀態？');">
                                <%= eq.IsActive ? "停用" : "啟用" %>
                            </a>

                            <button type="button" class="btn btn-secondary btn-sm" style="color: var(--color-accent-araigaki); border-color: #E2B9B5;" 
                                    onclick="confirmDeleteEquipment(<%= eq.EquipmentId %>, '<%= HttpUtility.JavaScriptStringEncode(eq.EquipmentName) %>');">
                                刪除
                            </button>
                        </td>
                    </tr>
                <% } %>
            </tbody>
        </table>
    </div>

    <!-- 新增/編輯設備彈窗 -->
    <div id="equipmentModal" class="modal-overlay">
        <div class="modal-dialog">
            <div class="modal-header">
                <div class="modal-title" id="equipModalTitle">💻 新增設備資產</div>
                <button type="button" class="btn-drawer-close" onclick="closeEquipmentModal();">&times;</button>
            </div>
            <div class="modal-body">
                <asp:HiddenField ID="hfEquipmentId" runat="server" Value="0" />

                <div class="form-group">
                    <label class="form-label">設備名稱 <span class="required">*</span></label>
                    <asp:TextBox ID="txtEquipmentName" runat="server" CssClass="form-control" placeholder="例如：無線手握式麥克風 (組)"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label class="form-label">總庫存額度 (Quota) <span class="required">*</span></label>
                    <asp:TextBox ID="txtTotalQuantity" runat="server" CssClass="form-control" TextMode="Number" Text="4"></asp:TextBox>
                </div>

                <div class="form-group" style="margin-top: 12px;">
                    <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                        <asp:CheckBox ID="chkIsActive" runat="server" Checked="true" />
                        <span>啟用此設備項目供借用登記</span>
                    </label>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" onclick="closeEquipmentModal();">取消</button>
                <asp:Button ID="btnSaveEquipment" runat="server" CssClass="btn btn-primary" Text="儲存設備" OnClick="btnSaveEquipment_Click" />
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        function openEquipmentModal() {
            $('#<%= hfEquipmentId.ClientID %>').val('0');
            $('#equipModalTitle').text('💻 新增設備資產');
            $('#<%= txtEquipmentName.ClientID %>').val('');
            $('#<%= txtTotalQuantity.ClientID %>').val('4');
            $('#<%= chkIsActive.ClientID %>').prop('checked', true);
            $('#equipmentModal').addClass('active');
        }

        function editEquipment(id, name, qty, isActive) {
            $('#<%= hfEquipmentId.ClientID %>').val(id);
            $('#equipModalTitle').text('💻 編輯設備項目 - #' + id);
            $('#<%= txtEquipmentName.ClientID %>').val(name);
            $('#<%= txtTotalQuantity.ClientID %>').val(qty);
            $('#<%= chkIsActive.ClientID %>').prop('checked', isActive);
            $('#equipmentModal').addClass('active');
        }

        function closeEquipmentModal() {
            $('#equipmentModal').removeClass('active');
        }

        function confirmDeleteEquipment(equipmentId, equipmentName) {
            Swal.fire({
                title: '確定刪除此設備資產？',
                html: '<div style="text-align: left; font-size: 14px; line-height: 1.6; color: var(--color-text-secondary);">'
                    + '<p style="margin-bottom: 8px;">即將刪除設備：<strong style="color: var(--color-brand-primary);">' + equipmentName + '</strong> (#' + equipmentId + ')</p>'
                    + '<p style="color: var(--color-accent-araigaki); font-size: 13px;">⚠️ 注意：刪除後此設備項目的所有借用與附加明細記錄將一併移除，且無法復原。是否確定繼續？</p>'
                    + '</div>',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: '確定刪除',
                cancelButtonText: '取消',
                confirmButtonColor: '#B85D56',
                cancelButtonColor: '#6B4E3D'
            }).then(function (result) {
                if (result.isConfirmed) {
                    $.ajax({
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=delete_equipment") %>',
                        type: 'POST',
                        data: { equipmentId: equipmentId },
                        dataType: 'json',
                        success: function (res) {
                            if (res.Success) {
                                Swal.fire({
                                    icon: 'success',
                                    title: '已刪除',
                                    text: res.Message,
                                    confirmButtonColor: '#6B4E3D',
                                    timer: 1500,
                                    showConfirmButton: false
                                }).then(function () {
                                    location.reload();
                                });
                            } else {
                                Swal.fire({
                                    icon: 'error',
                                    title: '無法刪除',
                                    text: res.Message,
                                    confirmButtonColor: '#6B4E3D'
                                });
                            }
                        },
                        error: function () {
                            Swal.fire({
                                icon: 'error',
                                title: '刪除失敗',
                                text: '系統通訊錯誤，請稍後再試。',
                                confirmButtonColor: '#6B4E3D'
                            });
                        }
                    });
                }
            });
        }
    </script>
</asp:Content>
