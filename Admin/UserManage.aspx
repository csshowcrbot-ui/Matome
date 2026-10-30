<%@ Page Title="帳號權限管理" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="UserManage.aspx.cs" Inherits="Matome.Admin.UserManage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .swal2-container {
            z-index: 999999 !important;
            position: fixed !important;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div class="card-header-bar">
        <div>
            <h1 class="card-title">
                <span>👥</span> 審核人員與使用者權限維護
            </h1>
            <div class="card-subtitle">
                指派主管人員所屬審核單位（DeptId）與系統角色（Admin、Approver、User），實現精確審核路由。
            </div>
        </div>

        <div style="display: flex; gap: 8px;">
            <button type="button" class="btn btn-secondary" onclick="openDeptManageModal();">
                <span>🏢</span> 管理部門單位
            </button>
            <button type="button" class="btn btn-primary" onclick="openUserModal();">
                <span>➕</span> 新增人員帳號
            </button>
        </div>
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
                    <th style="width: 70px;">編號</th>
                    <th style="width: 140px;">登入帳號</th>
                    <th>人員姓名</th>
                    <th style="width: 160px;">所屬部門單位</th>
                    <th style="width: 130px;">系統角色</th>
                    <th>電子信箱</th>
                    <th style="width: 110px;">狀態</th>
                    <th style="width: 200px; text-align: center;">操作</th>
                </tr>
            </thead>
            <tbody>
                <% foreach (var u in UsersList) { %>
                    <tr>
                        <td style="color: var(--color-text-secondary); font-family: monospace;">#<%= u.UserId %></td>
                        <td>
                            <strong style="color: var(--color-brand-primary); font-family: monospace;"><%= u.Account %></strong>
                        </td>
                        <td>
                            <div style="font-weight: 500;"><%= u.FullName %></div>
                        </td>
                        <td><%= u.DeptName %></td>
                        <td>
                            <% if (u.Role == "Admin") { %>
                                <span class="badge badge-mine">系統管理員</span>
                            <% } else if (u.Role == "Approver") { %>
                                <span class="badge badge-pending">單位審核主管</span>
                            <% } else { %>
                                <span class="badge badge-approved">一般借用同仁</span>
                            <% } %>
                        </td>
                        <td style="color: var(--color-text-secondary); font-size: 13px;"><%= u.Email %></td>
                        <td>
                            <% if (u.IsActive) { %>
                                <span class="badge badge-approved">正常</span>
                            <% } else { %>
                                <span class="badge badge-cancelled">停用</span>
                            <% } %>
                        </td>
                        <td style="text-align: center; white-space: nowrap;">
                            <button type="button" class="btn btn-secondary btn-sm" 
                                    onclick="editUser(<%= u.UserId %>, '<%= u.Account %>', '<%= u.FullName %>', '<%= u.DeptId.HasValue ? u.DeptId.Value.ToString() : "" %>', '<%= u.Role %>', '<%= u.Email %>', <%= u.IsActive ? "true" : "false" %>);">
                                編輯
                            </button>

                            <a href="UserManage.aspx?toggle=<%= u.UserId %>" class="btn btn-secondary btn-sm" onclick="return confirm('確定切換此使用者之狀態？');">
                                <%= u.IsActive ? "停用" : "啟用" %>
                            </a>

                            <% if (u.UserId == CurrentUserId) { %>
                                <button type="button" class="btn btn-secondary btn-sm" style="opacity: 0.45; cursor: not-allowed;" disabled title="無法刪除目前登入之帳號">
                                    刪除
                                </button>
                            <% } else { %>
                                <button type="button" class="btn btn-secondary btn-sm" style="color: var(--color-accent-araigaki); border-color: #E2B9B5;" 
                                        onclick="confirmDeleteUser(<%= u.UserId %>, '<%= HttpUtility.JavaScriptStringEncode(u.Account) %>', '<%= HttpUtility.JavaScriptStringEncode(u.FullName) %>');">
                                    刪除
                                </button>
                            <% } %>
                        </td>
                    </tr>
                <% } %>
            </tbody>
        </table>
    </div>

    <!-- 新增/編輯使用者彈窗 -->
    <div id="userModal" class="modal-overlay">
        <div class="modal-dialog">
            <div class="modal-header">
                <div class="modal-title" id="userModalTitle">👥 新增人員帳號</div>
                <button type="button" class="btn-drawer-close" onclick="closeUserModal();">&times;</button>
            </div>
            <div class="modal-body">
                <asp:HiddenField ID="hfUserId" runat="server" Value="0" />

                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                    <div class="form-group">
                        <label class="form-label">登入帳號 <span class="required">*</span></label>
                        <asp:TextBox ID="txtAccount" runat="server" CssClass="form-control" placeholder="例如：user_wang"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label class="form-label">人員全名 <span class="required">*</span></label>
                        <asp:TextBox ID="txtFullName" runat="server" CssClass="form-control" placeholder="例如：王小明"></asp:TextBox>
                    </div>
                </div>

                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                    <div class="form-group">
                        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
                            <label class="form-label" style="margin-bottom: 0;">所屬部門單位</label>
                            <button type="button" class="btn btn-secondary btn-sm" style="padding: 2px 8px; font-size: 11px; height: auto;" onclick="openAddDeptPrompt();">
                                ➕ 新增部門
                            </button>
                        </div>
                        <asp:DropDownList ID="ddlDept" runat="server" CssClass="form-control" ClientIDMode="Static">
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label class="form-label">指派角色權限 <span class="required">*</span></label>
                        <asp:DropDownList ID="ddlRole" runat="server" CssClass="form-control">
                            <asp:ListItem Value="User" Text="一般使用者 (User)"></asp:ListItem>
                            <asp:ListItem Value="Approver" Text="單位審核主管 (Approver)"></asp:ListItem>
                            <asp:ListItem Value="Admin" Text="系統管理員 (Admin)"></asp:ListItem>
                        </asp:DropDownList>
                    </div>
                </div>

                <div class="form-group">
                    <label class="form-label">電子信箱 (Email) <span class="required">*</span></label>
                    <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" placeholder="user@corp.local"></asp:TextBox>
                </div>

                <div class="form-group">
                    <label class="form-label">密碼設定 <span id="pwdRequiredStar" class="required">*</span></label>
                    <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="輸入設定密碼"></asp:TextBox>
                    <div id="pwdHelpText" style="font-size: 12px; color: var(--color-text-secondary); margin-top: 4px; display: none;">
                        編輯現有帳號時，若保留原密碼不修改請留空即可。
                    </div>
                </div>

                <div class="form-group">
                    <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                        <asp:CheckBox ID="chkIsActive" runat="server" Checked="true" />
                        <span>啟用此帳號登入權限</span>
                    </label>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" onclick="closeUserModal();">取消</button>
                <asp:Button ID="btnSaveUser" runat="server" CssClass="btn btn-primary" Text="儲存帳號" OnClick="btnSaveUser_Click" />
            </div>
        </div>
    </div>

    <!-- 管理部門單位彈窗 -->
    <div id="deptManageModal" class="modal-overlay">
        <div class="modal-dialog" style="max-width: 520px;">
            <div class="modal-header">
                <div class="modal-title">🏢 部門單位維護</div>
                <button type="button" class="btn-drawer-close" onclick="closeDeptManageModal();">&times;</button>
            </div>
            <div class="modal-body">
                <div style="font-size: 13px; color: var(--color-text-secondary); margin-bottom: 12px;">
                    維護系統各部門單位清單，可供人員帳號指派及會議室管轄審核單位關聯使用。
                </div>
                <div style="display: flex; gap: 8px; margin-bottom: 16px;">
                    <input type="text" id="txtNewDeptName" class="form-control" placeholder="輸入新部門名稱 (例如：護理部、企劃室)" />
                    <button type="button" class="btn btn-primary" style="white-space: nowrap;" onclick="submitNewDept();">
                        確定新增
                    </button>
                </div>
                <div style="max-height: 280px; overflow-y: auto; border: 1px solid var(--color-border); border-radius: var(--radius-sm);">
                    <table class="muji-table" style="margin: 0;">
                        <thead>
                            <tr>
                                <th style="width: 70px;">編號</th>
                                <th>部門單位名稱</th>
                                <th style="width: 80px; text-align: center;">操作</th>
                            </tr>
                        </thead>
                        <tbody id="deptTableBody">
                            <!-- 動態載入 -->
                        </tbody>
                    </table>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" onclick="closeDeptManageModal();">關閉</button>
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        function openUserModal() {
            $('#<%= hfUserId.ClientID %>').val('0');
            $('#userModalTitle').text('👥 新增人員帳號');
            $('#<%= txtAccount.ClientID %>').prop('readonly', false).val('');
            $('#<%= txtFullName.ClientID %>').val('');
            $('#<%= txtEmail.ClientID %>').val('');
            $('#<%= ddlDept.ClientID %>').val('');
            $('#<%= ddlRole.ClientID %>').val('User');
            $('#<%= txtPassword.ClientID %>').val('');
            $('#pwdRequiredStar').show();
            $('#pwdHelpText').hide();
            $('#<%= chkIsActive.ClientID %>').prop('checked', true);
            $('#userModal').addClass('active');
        }

        function editUser(id, account, name, deptId, role, email, isActive) {
            $('#<%= hfUserId.ClientID %>').val(id);
            $('#userModalTitle').text('👥 編輯人員帳號 - ' + account);
            $('#<%= txtAccount.ClientID %>').prop('readonly', true).val(account);
            $('#<%= txtFullName.ClientID %>').val(name);
            $('#<%= txtEmail.ClientID %>').val(email);
            $('#<%= ddlDept.ClientID %>').val(deptId || '');
            $('#<%= ddlRole.ClientID %>').val(role);
            $('#<%= txtPassword.ClientID %>').val('');
            $('#pwdRequiredStar').hide();
            $('#pwdHelpText').show();
            $('#<%= chkIsActive.ClientID %>').prop('checked', isActive);
            $('#userModal').addClass('active');
        }

        function closeUserModal() {
            $('#userModal').removeClass('active');
        }

        // 新增/管理部門功能
        function openAddDeptPrompt() {
            Swal.fire({
                title: '➕ 新增部門單位',
                input: 'text',
                inputLabel: '請輸入新的部門單位名稱',
                inputPlaceholder: '例如：護理部、醫學企劃室...',
                showCancelButton: true,
                confirmButtonText: '確定新增',
                cancelButtonText: '取消',
                confirmButtonColor: '#6B4E3D',
                inputValidator: function (value) {
                    if (!value || !$.trim(value)) {
                        return '部門名稱不可為空！';
                    }
                }
            }).then(function (result) {
                if (result.isConfirmed) {
                    var deptName = $.trim(result.value);
                    $.ajax({
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=add_department") %>',
                        type: 'POST',
                        data: { deptName: deptName },
                        dataType: 'json',
                        success: function (res) {
                            if (res.Success) {
                                Swal.fire({
                                    icon: 'success',
                                    title: '新增成功',
                                    text: '部門【' + res.Data.deptName + '】已成功建立！',
                                    confirmButtonColor: '#6B4E3D',
                                    timer: 1500,
                                    showConfirmButton: false
                                });
                                // 將新部門新增至人員視窗的下拉選單並自動選取
                                var newOption = new Option(res.Data.deptName, res.Data.deptId.toString(), true, true);
                                $('#<%= ddlDept.ClientID %>').append(newOption);
                            } else {
                                Swal.fire({
                                    icon: 'error',
                                    title: '新增失敗',
                                    text: res.Message,
                                    confirmButtonColor: '#6B4E3D'
                                });
                            }
                        },
                        error: function () {
                            Swal.fire({ icon: 'error', title: '系統通訊錯誤' });
                        }
                    });
                }
            });
        }

        function confirmDeleteUser(userId, account, fullName) {
            Swal.fire({
                title: '確定刪除此帳號？',
                html: '<div style="text-align: left; font-size: 14px; line-height: 1.6; color: var(--color-text-secondary);">'
                    + '<p style="margin-bottom: 8px;">即將刪除帳號：<strong style="color: var(--color-brand-primary); font-family: monospace;">' + account + '</strong> (' + fullName + ')</p>'
                    + '<p style="color: var(--color-accent-araigaki); font-size: 13px;">⚠️ 注意：刪除後該人員的所有歷史預約與設備借用記錄將一併移除，且無法復原。是否確定繼續？</p>'
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
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=delete_user") %>',
                        type: 'POST',
                        data: { userId: userId },
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

        function openDeptManageModal() {
            loadDeptList();
            $('#deptManageModal').addClass('active');
        }

        function closeDeptManageModal() {
            $('#deptManageModal').removeClass('active');
        }

        function loadDeptList() {
            $.ajax({
                url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=get_departments") %>',
                type: 'GET',
                dataType: 'json',
                success: function (res) {
                    if (res.Success) {
                        renderDeptTable(res.Data);
                    }
                }
            });
        }

        function renderDeptTable(depts) {
            var tbody = $('#deptTableBody');
            tbody.empty();
            if (!depts || depts.length === 0) {
                tbody.append('<tr><td colspan="3" style="text-align: center; color: var(--color-text-secondary); padding: 20px;">尚無部門資料</td></tr>');
                return;
            }
            depts.forEach(function (d) {
                var tr = $('<tr></tr>');
                tr.append('<td style="font-family: monospace; color: var(--color-text-secondary);">#' + d.DeptId + '</td>');
                tr.append('<td><strong>' + $('<div>').text(d.DeptName).html() + '</strong></td>');
                var btn = $('<button type="button" class="btn btn-secondary btn-sm" style="color: var(--color-accent-araigaki); border-color: #E2B9B5; padding: 2px 8px; font-size: 11px;">刪除</button>');
                btn.on('click', function () {
                    deleteDept(d.DeptId, d.DeptName);
                });
                tr.append($('<td style="text-align: center;"></td>').append(btn));
                tbody.append(tr);
            });
        }

        function submitNewDept() {
            var deptName = $.trim($('#txtNewDeptName').val());
            if (!deptName) {
                Swal.fire({ icon: 'warning', title: '請輸入部門單位名稱' });
                return;
            }
            $.ajax({
                url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=add_department") %>',
                type: 'POST',
                data: { deptName: deptName },
                dataType: 'json',
                success: function (res) {
                    if (res.Success) {
                        $('#txtNewDeptName').val('');
                        loadDeptList();
                        // 同步更新人員彈窗的下拉選單
                        $('#<%= ddlDept.ClientID %>').append(new Option(res.Data.deptName, res.Data.deptId.toString()));
                        Swal.fire({
                            icon: 'success',
                            title: '新增成功',
                            text: '部門【' + res.Data.deptName + '】已成功建立！',
                            confirmButtonColor: '#6B4E3D',
                            timer: 1500,
                            showConfirmButton: false
                        });
                    } else {
                        Swal.fire({ icon: 'error', title: '新增失敗', text: res.Message, confirmButtonColor: '#6B4E3D' });
                    }
                }
            });
        }

        function deleteDept(deptId, deptName) {
            if (typeof Swal === 'undefined') {
                if (confirm('即將刪除部門【' + deptName + '】，若已有同仁或會議室關聯將無法刪除。\n\n確定繼續刪除？')) {
                    executeDeleteDept(deptId);
                }
                return;
            }

            Swal.fire({
                title: '確認刪除部門？',
                text: '即將刪除部門【' + deptName + '】，若已有同仁或會議室關聯將無法刪除。',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: '確定刪除',
                cancelButtonText: '取消',
                confirmButtonColor: '#B85D56',
                cancelButtonColor: '#6B4E3D'
            }).then(function (result) {
                if (result.isConfirmed) {
                    executeDeleteDept(deptId);
                }
            });
        }

        function executeDeleteDept(deptId) {
            $.ajax({
                url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=delete_department") %>',
                type: 'POST',
                data: { deptId: deptId },
                dataType: 'json',
                success: function (res) {
                    if (res.Success) {
                        loadDeptList();
                        $('#<%= ddlDept.ClientID %> option[value="' + deptId + '"]').remove();
                        if (typeof Swal !== 'undefined') {
                            Swal.fire({ icon: 'success', title: '已刪除', text: res.Message, confirmButtonColor: '#6B4E3D', timer: 1500, showConfirmButton: false });
                        } else {
                            alert(res.Message);
                        }
                    } else {
                        if (typeof Swal !== 'undefined') {
                            Swal.fire({ icon: 'error', title: '無法刪除', text: res.Message, confirmButtonColor: '#6B4E3D' });
                        } else {
                            alert(res.Message);
                        }
                    }
                },
                error: function () {
                    if (typeof Swal !== 'undefined') {
                        Swal.fire({ icon: 'error', title: '刪除失敗', text: '系統通訊錯誤，請稍後再試。', confirmButtonColor: '#6B4E3D' });
                    } else {
                        alert('系統通訊錯誤，請稍後再試。');
                    }
                }
            });
        }
    </script>
</asp:Content>
