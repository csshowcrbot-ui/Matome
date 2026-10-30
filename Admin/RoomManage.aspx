<%@ Page Title="會議室維護管理" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RoomManage.aspx.cs" Inherits="Matome.Admin.RoomManage" %>

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
                <span>🏢</span> 會議室場地維護管理
            </h1>
            <div class="card-subtitle">
                維護公司所有會議空間、容納人數限制、審核審批規則與指定審核權責單位。
            </div>
        </div>

        <div style="display: flex; gap: 10px;">
            <button type="button" class="btn btn-secondary" onclick="openDeptManageModal();">
                <span class="btn-icon">🏛️</span> 管理部門單位
            </button>
            <button type="button" class="btn btn-primary" onclick="openRoomModal();">
                <span>➕</span> 新增會議室
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
                    <th>會議室名稱</th>
                    <th style="width: 120px;">容納人數</th>
                    <th style="width: 160px;">位置 / 樓層</th>
                    <th style="width: 150px;">審核機制</th>
                    <th style="width: 160px;">指定審核單位</th>
                    <th style="width: 110px;">目前狀態</th>
                    <th style="width: 190px; text-align: center;">操作</th>
                </tr>
            </thead>
            <tbody>
                <% foreach (var r in RoomsList) { %>
                    <tr>
                        <td style="color: var(--color-text-secondary); font-family: monospace;">#<%= r.RoomId %></td>
                        <td>
                            <strong style="color: var(--color-text-primary); font-size: 14px;"><%= r.RoomName %></strong>
                        </td>
                        <td><%= r.Capacity %> 人</td>
                        <td><%= string.IsNullOrEmpty(r.Location) ? "-" : r.Location %></td>
                        <td>
                            <% if (r.IsApprovalRequired) { %>
                                <span class="badge badge-pending">需主管審核</span>
                            <% } else { %>
                                <span class="badge badge-approved">免審核即開</span>
                            <% } %>
                        </td>
                        <td>
                            <%= r.IsApprovalRequired ? (string.IsNullOrEmpty(r.ApprovalDeptName) ? "全域主管皆可" : r.ApprovalDeptName) : "-" %>
                        </td>
                        <td>
                            <% if (r.IsActive) { %>
                                <span class="badge badge-approved">啟用中</span>
                            <% } else { %>
                                <span class="badge badge-cancelled">已停用</span>
                            <% } %>
                        </td>
                        <td style="text-align: center; white-space: nowrap;">
                            <button type="button" class="btn btn-secondary btn-sm" 
                                    onclick="editRoom(<%= r.RoomId %>, '<%= HttpUtility.JavaScriptStringEncode(r.RoomName) %>', <%= r.Capacity %>, '<%= HttpUtility.JavaScriptStringEncode(r.Location ?? "") %>', <%= r.IsApprovalRequired ? "true" : "false" %>, '<%= r.ApprovalDeptId.HasValue ? r.ApprovalDeptId.Value.ToString() : "" %>', <%= r.IsActive ? "true" : "false" %>);">
                                編輯
                            </button>

                            <a href="RoomManage.aspx?toggle=<%= r.RoomId %>" class="btn btn-secondary btn-sm" onclick="return confirm('確定切換此會議室的啟用/停用狀態？');">
                                <%= r.IsActive ? "停用" : "啟用" %>
                            </a>

                            <button type="button" class="btn btn-secondary btn-sm" style="color: var(--color-accent-araigaki); border-color: #E2B9B5;" 
                                    onclick="confirmDeleteRoom(<%= r.RoomId %>, '<%= HttpUtility.JavaScriptStringEncode(r.RoomName) %>');">
                                刪除
                            </button>
                        </td>
                    </tr>
                <% } %>
            </tbody>
        </table>
    </div>

    <!-- 新增/編輯會議室彈窗 -->
    <div id="roomModal" class="modal-overlay">
        <div class="modal-dialog">
            <div class="modal-header">
                <div class="modal-title" id="roomModalTitle">🏢 新增會議室</div>
                <button type="button" class="btn-drawer-close" onclick="closeRoomModal();">&times;</button>
            </div>
            <div class="modal-body">
                <asp:HiddenField ID="hfRoomId" runat="server" Value="0" />

                <div class="form-group">
                    <label class="form-label">會議室名稱 <span class="required">*</span></label>
                    <asp:TextBox ID="txtRoomName" runat="server" CssClass="form-control" placeholder="例如：檜木大會議室"></asp:TextBox>
                </div>

                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                    <div class="form-group">
                        <label class="form-label">容納人數上限 <span class="required">*</span></label>
                        <asp:TextBox ID="txtCapacity" runat="server" CssClass="form-control" TextMode="Number" Text="12"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label class="form-label">所在位置 / 樓層</label>
                        <asp:TextBox ID="txtLocation" runat="server" CssClass="form-control" placeholder="例如：A棟 3F"></asp:TextBox>
                    </div>
                </div>

                <div class="form-group" style="margin-top: 8px;">
                    <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                        <asp:CheckBox ID="chkIsApprovalRequired" runat="server" onclick="toggleDeptDropdown();" />
                        <span style="font-weight: 500;">借用此會議室需要主管審核</span>
                    </label>
                </div>

                <div class="form-group" id="deptSelectGroup" style="display: none; padding-left: 24px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
                        <label class="form-label" style="margin-bottom: 0;">指定管轄審核單位</label>
                        <button type="button" class="btn btn-secondary btn-sm" style="padding: 2px 8px; font-size: 11px; height: auto;" onclick="openAddDeptPrompt();">
                            ➕ 新增部門
                        </button>
                    </div>
                    <asp:DropDownList ID="ddlApprovalDept" runat="server" CssClass="form-control" ClientIDMode="Static">
                        <asp:ListItem Value="" Text="不指定 (全域主管或管理員皆可審核)"></asp:ListItem>
                    </asp:DropDownList>
                    <div style="font-size: 12px; color: var(--color-text-secondary); margin-top: 4px;">
                        ※ 審核權限以「權限維護」中設定之主管所屬部門單位為主；選定後僅該部門審核主管（Approver）或系統管理員可審核。
                    </div>
                </div>

                <div class="form-group">
                    <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                        <asp:CheckBox ID="chkIsActive" runat="server" Checked="true" />
                        <span>啟用此會議室供人員借用</span>
                    </label>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" onclick="closeRoomModal();">取消</button>
                <asp:Button ID="btnSaveRoom" runat="server" CssClass="btn btn-primary" Text="儲存會議室" OnClick="btnSaveRoom_Click" />
            </div>
        </div>
    </div>

    <!-- 部門單位管理彈窗 (與權限維護完全連動) -->
    <div id="deptManageModal" class="modal-overlay">
        <div class="modal-dialog" style="max-width: 520px;">
            <div class="modal-header">
                <div class="modal-title">🏛️ 部門單位管理（權限維護連動）</div>
                <button type="button" class="btn-drawer-close" onclick="closeDeptManageModal();">&times;</button>
            </div>
            <div class="modal-body">
                <div style="display: flex; gap: 8px; margin-bottom: 16px;">
                    <input type="text" id="txtNewDeptName" class="form-control" placeholder="輸入新增部門名稱 (例如：護理部、檢驗處)" style="flex: 1;" />
                    <button type="button" class="btn btn-primary" onclick="submitNewDept();" style="white-space: nowrap;">
                        新增部門
                    </button>
                </div>

                <div style="font-size: 12px; color: var(--color-text-secondary); margin-bottom: 8px;">
                    💡 此處維護之部門單位即為「權限維護」與「場地管理」共同之權威清單。
                </div>

                <table class="muji-table" style="font-size: 13px;">
                    <thead>
                        <tr>
                            <th style="width: 70px;">編號</th>
                            <th>部門單位名稱</th>
                            <th style="width: 80px; text-align: center;">操作</th>
                        </tr>
                    </thead>
                    <tbody id="deptTableBody">
                    </tbody>
                </table>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" onclick="closeDeptManageModal();">關閉</button>
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        function openRoomModal() {
            $('#<%= hfRoomId.ClientID %>').val('0');
            $('#roomModalTitle').text('🏢 新增會議室');
            $('#<%= txtRoomName.ClientID %>').val('');
            $('#<%= txtCapacity.ClientID %>').val('12');
            $('#<%= txtLocation.ClientID %>').val('');
            $('#<%= chkIsApprovalRequired.ClientID %>').prop('checked', false);
            $('#<%= ddlApprovalDept.ClientID %>').val('');
            $('#<%= chkIsActive.ClientID %>').prop('checked', true);
            toggleDeptDropdown();
            $('#roomModal').addClass('active');
        }

        function editRoom(id, name, cap, loc, needApprove, deptId, isActive) {
            $('#<%= hfRoomId.ClientID %>').val(id);
            $('#roomModalTitle').text('🏢 編輯會議室 - #' + id);
            $('#<%= txtRoomName.ClientID %>').val(name);
            $('#<%= txtCapacity.ClientID %>').val(cap);
            $('#<%= txtLocation.ClientID %>').val(loc);
            $('#<%= chkIsApprovalRequired.ClientID %>').prop('checked', needApprove);
            $('#<%= ddlApprovalDept.ClientID %>').val(deptId || '');
            $('#<%= chkIsActive.ClientID %>').prop('checked', isActive);
            toggleDeptDropdown();
            $('#roomModal').addClass('active');
        }

        function closeRoomModal() {
            $('#roomModal').removeClass('active');
        }

        function toggleDeptDropdown() {
            var isChecked = $('#<%= chkIsApprovalRequired.ClientID %>').is(':checked');
            if (isChecked) {
                $('#deptSelectGroup').slideDown(150);
            } else {
                $('#deptSelectGroup').slideUp(150);
            }
        }

        function confirmDeleteRoom(roomId, roomName) {
            Swal.fire({
                title: '確定刪除此會議室？',
                html: '<div style="text-align: left; font-size: 14px; line-height: 1.6; color: var(--color-text-secondary);">'
                    + '<p style="margin-bottom: 8px;">即將刪除場地：<strong style="color: var(--color-brand-primary);">' + roomName + '</strong> (#' + roomId + ')</p>'
                    + '<p style="color: var(--color-accent-araigaki); font-size: 13px;">⚠️ 注意：刪除後此會議室之所有歷史預約記錄與附加設備明細將一併移除，且無法復原。是否確定繼續？</p>'
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
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=delete_room") %>',
                        type: 'POST',
                        data: { roomId: roomId },
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

        // 部門單位連動管理功能 (與權限維護同步)
        function openAddDeptPrompt() {
            Swal.fire({
                title: '➕ 新增部門單位',
                input: 'text',
                inputLabel: '請輸入新的部門單位名稱',
                inputPlaceholder: '例如：護理部、檢驗處、醫療企劃室...',
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
                                    text: '部門【' + res.Data.deptName + '】已成功建立並同步至權限維護！',
                                    confirmButtonColor: '#6B4E3D',
                                    timer: 1500,
                                    showConfirmButton: false
                                });
                                // 同步加入本頁下拉選單並選取
                                var newOption = new Option(res.Data.deptName, res.Data.deptId.toString(), true, true);
                                $('#<%= ddlApprovalDept.ClientID %>').append(newOption);
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
                        // 同步更新會議室彈窗的下拉選單
                        $('#<%= ddlApprovalDept.ClientID %>').append(new Option(res.Data.deptName, res.Data.deptId.toString()));
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
                        $('#<%= ddlApprovalDept.ClientID %> option[value="' + deptId + '"]').remove();
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
