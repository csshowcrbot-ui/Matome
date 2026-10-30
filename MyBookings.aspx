<%@ Page Title="我的預約紀錄" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyBookings.aspx.cs" Inherits="Matome.MyBookings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .booking-metric-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 16px;
            margin-bottom: 24px;
        }
        .metric-card {
            background-color: var(--color-surface);
            border: 1px solid var(--color-border);
            border-radius: var(--radius-md);
            padding: 16px 20px;
            box-shadow: var(--shadow-soft);
        }
        .metric-num {
            font-size: 26px;
            font-weight: 600;
            color: var(--color-brand-primary);
            margin-top: 4px;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div class="card-header-bar">
        <div>
            <h1 class="card-title">
                <span>📋</span> 我的預約登記紀錄
            </h1>
            <div class="card-subtitle">
                檢視您目前所有的會議室與附加設備預約紀錄，並可針對尚未開始的會議進行時段釋出。
            </div>
        </div>

        <a href="<%= ResolveUrl("~/Default.aspx") %>" class="btn btn-primary">
            <span>📅</span> 前往行事曆新增借用
        </a>
    </div>

    <!-- 概覽指標小卡 -->
    <div class="booking-metric-grid">
        <div class="metric-card">
            <div style="color: var(--color-text-secondary); font-size: 13px;">總借用次數</div>
            <div class="metric-num"><%= TotalCount %></div>
        </div>
        <div class="metric-card">
            <div style="color: var(--color-text-secondary); font-size: 13px;">即將進行</div>
            <div class="metric-num" style="color: var(--color-accent-matcha);"><%= UpcomingCount %></div>
        </div>
        <div class="metric-card">
            <div style="color: var(--color-text-secondary); font-size: 13px;">待審核中</div>
            <div class="metric-num" style="color: var(--color-accent-kohaku);"><%= PendingCount %></div>
        </div>
        <div class="metric-card">
            <div style="color: var(--color-text-secondary); font-size: 13px;">已取消/駁回</div>
            <div class="metric-num" style="color: var(--color-text-secondary);"><%= CancelledCount %></div>
        </div>
    </div>

    <!-- 篩選分頁 Tab -->
    <div class="tab-filter-nav">
        <a href="MyBookings.aspx?filter=all" class="tab-filter-btn <%= CurrentFilter == "all" ? "active" : "" %>">
            全部紀錄 (<%= TotalCount %>)
        </a>
        <a href="MyBookings.aspx?filter=upcoming" class="tab-filter-btn <%= CurrentFilter == "upcoming" ? "active" : "" %>">
            即將到來 (<%= UpcomingCount %>)
        </a>
        <a href="MyBookings.aspx?filter=history" class="tab-filter-btn <%= CurrentFilter == "history" ? "active" : "" %>">
            歷史紀錄
        </a>
    </div>

    <!-- 會議室預約紀錄資料表格 -->
    <div style="font-weight: 600; font-size: 16px; margin: 24px 0 12px 0; display: flex; align-items: center; gap: 8px;">
        <span>🏢</span> 會議場地預約紀錄
    </div>
    <div class="muji-card" style="padding: 0; overflow: hidden; margin-bottom: 32px;">
        <table class="muji-table">
            <thead>
                <tr>
                    <th style="width: 70px;">編號</th>
                    <th>會議主題 / 事由</th>
                    <th style="width: 170px;">會議室場地</th>
                    <th style="width: 240px;">借用預約時段</th>
                    <th style="width: 120px;">當前狀態</th>
                    <th style="width: 130px; text-align: center;">操作</th>
                </tr>
            </thead>
            <tbody>
                <% if (UserBookings == null || UserBookings.Count == 0) { %>
                    <tr>
                        <td colspan="6" style="text-align: center; padding: 36px; color: var(--color-text-secondary);">
                            目前尚無符合此篩選條件的會議室預約紀錄。
                        </td>
                    </tr>
                <% } else { %>
                    <% foreach (var item in UserBookings) { %>
                        <tr>
                            <td style="color: var(--color-text-secondary); font-family: monospace;">#<%= item.BookingId %></td>
                            <td>
                                <div style="font-weight: 600; color: var(--color-text-primary);"><%= item.Subject %></div>
                                <div style="font-size: 12px; color: var(--color-text-secondary); margin-top: 2px;">
                                    登記時間：<%= item.CreateTime.ToString("yyyy-MM-dd HH:mm") %>
                                </div>
                                <% if (item.Status == "Rejected" && !string.IsNullOrEmpty(item.RejectReason)) { %>
                                    <div style="font-size: 12px; color: var(--color-accent-araigaki); margin-top: 4px; background: var(--color-accent-araigaki-light); padding: 4px 8px; border-radius: 4px;">
                                        ⚠️ 駁回原因：<%= item.RejectReason %>
                                    </div>
                                <% } %>
                            </td>
                            <td>
                                <div style="font-weight: 500;"><%= item.RoomName %></div>
                            </td>
                            <td>
                                <div><%= item.StartTime.ToString("yyyy-MM-dd (ddd)") %></div>
                                <div style="font-weight: 600; color: var(--color-brand-primary);">
                                    <%= item.StartTime.ToString("HH:mm") %> ~ <%= item.EndTime.ToString("HH:mm") %>
                                </div>
                            </td>
                            <td>
                                <% if (item.Status == "Approved") { %>
                                    <span class="badge badge-approved">✓ 已核准借用</span>
                                <% } else if (item.Status == "Pending") { %>
                                    <span class="badge badge-pending">◷ 審核中</span>
                                <% } else if (item.Status == "Rejected") { %>
                                    <span class="badge badge-rejected">✕ 已駁回</span>
                                <% } else { %>
                                    <span class="badge badge-cancelled">已取消</span>
                                <% } %>
                            </td>
                            <td style="text-align: center;">
                                <% if ((item.Status == "Pending" || item.Status == "Approved") && item.StartTime > DateTime.Now) { %>
                                    <button type="button" class="btn btn-araigaki btn-sm" onclick="cancelBooking(<%= item.BookingId %>);">
                                        取消預約
                                    </button>
                                <% } else { %>
                                    <span style="color: var(--color-border-dark); font-size: 12px;">-</span>
                                <% } %>
                            </td>
                        </tr>
                    <% } %>
                <% } %>
            </tbody>
        </table>
    </div>

    <!-- 獨立設備借用紀錄表格 -->
    <div style="font-weight: 600; font-size: 16px; margin: 24px 0 12px 0; display: flex; align-items: center; gap: 8px;">
        <span>💻</span> 獨立設備借用紀錄
    </div>
    <div class="muji-card" style="padding: 0; overflow: hidden;">
        <table class="muji-table">
            <thead>
                <tr>
                    <th style="width: 70px;">編號</th>
                    <th>借用事由 / 專案主題</th>
                    <th style="width: 200px;">設備項目</th>
                    <th style="width: 100px;">借用數量</th>
                    <th style="width: 240px;">借用時段</th>
                    <th style="width: 120px;">當前狀態</th>
                    <th style="width: 130px; text-align: center;">操作</th>
                </tr>
            </thead>
            <tbody>
                <% if (UserEquipmentBookings == null || UserEquipmentBookings.Count == 0) { %>
                    <tr>
                        <td colspan="7" style="text-align: center; padding: 36px; color: var(--color-text-secondary);">
                            目前尚無符合此篩選條件的設備借用紀錄。
                        </td>
                    </tr>
                <% } else { %>
                    <% foreach (var eqItem in UserEquipmentBookings) { %>
                        <tr>
                            <td style="color: var(--color-text-secondary); font-family: monospace;">#<%= eqItem.BookingId %></td>
                            <td>
                                <div style="font-weight: 600; color: var(--color-text-primary);"><%= eqItem.Subject %></div>
                                <div style="font-size: 12px; color: var(--color-text-secondary); margin-top: 2px;">
                                    登記時間：<%= eqItem.CreateTime.ToString("yyyy-MM-dd HH:mm") %>
                                </div>
                            </td>
                            <td>
                                <strong style="color: var(--color-brand-primary);"><%= eqItem.EquipmentName %></strong>
                            </td>
                            <td>
                                <span style="font-weight: 600; font-size: 14px;"><%= eqItem.Quantity %></span> 組/套
                            </td>
                            <td>
                                <div><%= eqItem.StartTime.ToString("yyyy-MM-dd (ddd)") %></div>
                                <div style="font-weight: 600; color: var(--color-brand-primary);">
                                    <%= eqItem.StartTime.ToString("HH:mm") %> ~ <%= eqItem.EndTime.ToString("HH:mm") %>
                                </div>
                            </td>
                            <td>
                                <% if (eqItem.Status == "Approved") { %>
                                    <span class="badge badge-approved">✓ 借用已確認</span>
                                <% } else if (eqItem.Status == "Cancelled") { %>
                                    <span class="badge badge-cancelled">已取消</span>
                                <% } else { %>
                                    <span class="badge badge-pending"><%= eqItem.Status %></span>
                                <% } %>
                            </td>
                            <td style="text-align: center;">
                                <% if (eqItem.Status == "Approved" && eqItem.StartTime > DateTime.Now) { %>
                                    <button type="button" class="btn btn-araigaki btn-sm" onclick="cancelEquipmentBooking(<%= eqItem.BookingId %>);">
                                        取消借用
                                    </button>
                                <% } else { %>
                                    <span style="color: var(--color-border-dark); font-size: 12px;">-</span>
                                <% } %>
                            </td>
                        </tr>
                    <% } %>
                <% } %>
            </tbody>
        </table>
    </div>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        function cancelBooking(bookingId) {
            Swal.fire({
                title: '確認取消此預約？',
                text: '取消後時段將立即開放供他人預約，並自動釋放借用的設備。',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#B85D56',
                cancelButtonColor: '#70706E',
                confirmButtonText: '確認取消',
                cancelButtonText: '保留預約'
            }).then(function (result) {
                if (result.isConfirmed) {
                    $.ajax({
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx") %>',
                        type: 'POST',
                        data: {
                            action: 'cancel_booking',
                            bookingId: bookingId
                        },
                        dataType: 'json',
                        success: function (res) {
                            if (res.Success) {
                                Swal.fire({
                                    icon: 'success',
                                    title: '取消成功',
                                    text: res.Message,
                                    confirmButtonColor: '#6B4E3D'
                                }).then(function () {
                                    window.location.reload();
                                });
                            } else {
                                Swal.fire({ icon: 'error', title: '取消失敗', text: res.Message });
                            }
                        }
                    });
                }
            });
        }

        function cancelEquipmentBooking(bookingId) {
            Swal.fire({
                title: '確認取消此設備借用？',
                text: '取消後額度將即時釋出供其他同仁登記借用。',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#B85D56',
                cancelButtonColor: '#70706E',
                confirmButtonText: '確認取消',
                cancelButtonText: '保留借用'
            }).then(function (result) {
                if (result.isConfirmed) {
                    $.ajax({
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx") %>',
                        type: 'POST',
                        data: {
                            action: 'cancel_equipment_booking',
                            bookingId: bookingId
                        },
                        dataType: 'json',
                        success: function (res) {
                            if (res.Success) {
                                Swal.fire({
                                    icon: 'success',
                                    title: '取消成功',
                                    text: res.Message,
                                    confirmButtonColor: '#6B4E3D'
                                }).then(function () {
                                    window.location.reload();
                                });
                            } else {
                                Swal.fire({ icon: 'error', title: '取消失敗', text: res.Message });
                            }
                        }
                    });
                }
            });
        }
    </script>
</asp:Content>
