<%@ Page Title="待審核工作台" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PendingList.aspx.cs" Inherits="Matome.Approval.PendingList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .approval-card-item {
            background-color: var(--color-surface);
            border: 1px solid var(--color-border);
            border-radius: var(--radius-md);
            padding: 20px 24px;
            margin-bottom: 16px;
            box-shadow: var(--shadow-soft);
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 20px;
            transition: all 0.2s ease;
        }
        .approval-card-item:hover {
            box-shadow: var(--shadow-hover);
            border-color: var(--color-border-dark);
        }
        .approval-card-item.has-conflict {
            border-left: 4px solid var(--color-accent-araigaki);
            background-color: #FFFDFD;
        }
        .approval-meta-row {
            display: flex;
            align-items: center;
            gap: 16px;
            margin-top: 8px;
            font-size: 13px;
            color: var(--color-text-secondary);
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div class="card-header-bar">
        <div>
            <h1 class="card-title">
                <span>✍️</span> 會議借用審核工作台
            </h1>
            <div class="card-subtitle">
                審核您管轄範疇內的會議室申請案。系統已自動啟用衝突智慧檢測，為您標記重疊搶借之預約案。
            </div>
        </div>

        <div>
            <span class="badge badge-pending" style="font-size: 13px; padding: 6px 14px;">
                待審核申請：<%= PendingListItems.Count %> 筆
            </span>
        </div>
    </div>

    <% if (PendingListItems == null || PendingListItems.Count == 0) { %>
        <div class="muji-card" style="text-align: center; padding: 60px 20px;">
            <div style="font-size: 40px; margin-bottom: 12px;">🍵</div>
            <div style="font-size: 16px; font-weight: 600; color: var(--color-brand-primary);">
                太棒了！目前沒有待您審核的借用申請
            </div>
            <div style="font-size: 13px; color: var(--color-text-secondary); margin-top: 6px;">
                所有申請案皆已完成處理，您可以隨時點選上方選單檢視週行事曆。
            </div>
        </div>
    <% } else { %>
        <div style="display: flex; flex-direction: column;">
            <% foreach (var item in PendingListItems) { %>
                <div class="approval-card-item <%= item.HasConflict ? "has-conflict" : "" %>">
                    <div style="flex: 1;">
                        <% if (item.HasConflict) { %>
                            <div class="conflict-alert-box" style="margin-bottom: 8px; padding: 6px 12px; font-size: 12px;">
                                <span>⚠️</span> <strong>注意：</strong> 此時段存在多筆競爭申請或重疊時段，請核定最優先者。
                            </div>
                        <% } %>

                        <div style="display: flex; align-items: center; gap: 12px;">
                            <span style="font-weight: 600; font-size: 16px; color: var(--color-text-primary);">
                                <%= item.Subject %>
                            </span>
                            <span class="badge badge-pending">待審核</span>
                        </div>

                        <div class="approval-meta-row">
                            <div>
                                <span>🏢 會議室：</span>
                                <strong style="color: var(--color-brand-primary);"><%= item.RoomName %></strong>
                            </div>
                            <div>
                                <span>📅 借用時段：</span>
                                <strong><%= item.StartTime.ToString("yyyy-MM-dd HH:mm") %> ~ <%= item.EndTime.ToString("HH:mm") %></strong>
                            </div>
                            <div>
                                <span>👤 申請人：</span>
                                <span><%= item.UserName %> (<%= item.UserDeptName %>)</span>
                            </div>
                        </div>

                        <div style="margin-top: 10px; display: flex; align-items: center; gap: 8px; font-size: 12px;">
                            <span style="color: var(--color-text-secondary);">附帶設備：</span>
                            <% if (item.Equipments != null && item.Equipments.Count > 0) { %>
                                <% foreach (var eq in item.Equipments) { %>
                                    <span class="badge" style="background: var(--color-surface-soft); border: 1px solid var(--color-border);">
                                        <%= eq.EquipmentName %> x<%= eq.Quantity %>
                                    </span>
                                <% } %>
                            <% } else { %>
                                <span style="color: var(--color-text-secondary);">無</span>
                            <% } %>
                        </div>
                    </div>

                    <!-- 操作按鈕組 -->
                    <div style="display: flex; align-items: center; gap: 10px; flex-shrink: 0;">
                        <button type="button" class="btn btn-matcha" onclick="approveBooking(<%= item.BookingId %>, '<%= item.Subject %>');">
                            <span>✓</span> 核准借用
                        </button>
                        <button type="button" class="btn btn-araigaki" onclick="openRejectModal(<%= item.BookingId %>, '<%= item.Subject %>');">
                            <span>✕</span> 駁回申請
                        </button>
                    </div>
                </div>
            <% } %>
        </div>
    <% } %>

    <!-- 駁回理由輸入彈窗 (Rejection Modal) -->
    <div id="rejectModal" class="modal-overlay">
        <div class="modal-dialog">
            <div class="modal-header">
                <div class="modal-title">✍️ 輸入駁回申請原因</div>
                <button type="button" class="btn-drawer-close" onclick="closeRejectModal();">&times;</button>
            </div>
            <div class="modal-body">
                <div style="margin-bottom: 12px; font-size: 13px; color: var(--color-text-secondary);">
                    預約案：<strong id="rejectModalSubject" style="color: var(--color-text-primary);"></strong>
                </div>
                <div class="form-group">
                    <label class="form-label">駁回原因說明 <span class="required">* (必填)</span></label>
                    <textarea id="txtRejectReason" class="form-control" rows="4" placeholder="請詳細說明駁回原因，例如：該時段已保留給年度董事會使用、請協調更換至三樓會議室..."></textarea>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" onclick="closeRejectModal();">取消</button>
                <button type="button" class="btn btn-araigaki" style="background-color: var(--color-accent-araigaki); color: #fff;" onclick="submitReject();">
                    確認送出駁回
                </button>
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        var currentRejectBookingId = 0;

        function approveBooking(bookingId, subject) {
            Swal.fire({
                title: '核准此筆預約借用？',
                html: '確定核准【<strong>' + subject + '</strong>】之預約申請？<br><span style="font-size: 12px; color: #70706E;">核准後將自動為其保留會議室與附加設備。</span>',
                icon: 'question',
                showCancelButton: true,
                confirmButtonColor: '#5A6B5C',
                cancelButtonColor: '#70706E',
                confirmButtonText: '確定核准',
                cancelButtonText: '取消'
            }).then(function (result) {
                if (result.isConfirmed) {
                    $.ajax({
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx") %>',
                        type: 'POST',
                        data: {
                            action: 'approve_booking',
                            bookingId: bookingId
                        },
                        dataType: 'json',
                        success: function (res) {
                            if (res.Success) {
                                Swal.fire({
                                    icon: 'success',
                                    title: '已核准',
                                    text: res.Message,
                                    confirmButtonColor: '#6B4E3D'
                                }).then(function () {
                                    window.location.reload();
                                });
                            } else {
                                Swal.fire({ icon: 'error', title: '核准失敗', text: res.Message });
                            }
                        }
                    });
                }
            });
        }

        function openRejectModal(bookingId, subject) {
            currentRejectBookingId = bookingId;
            $('#rejectModalSubject').text(subject);
            $('#txtRejectReason').val('');
            $('#rejectModal').addClass('active');
        }

        function closeRejectModal() {
            $('#rejectModal').removeClass('active');
        }

        function submitReject() {
            var reason = $.trim($('#txtRejectReason').val());
            if (!reason) {
                Swal.fire({ icon: 'warning', title: '請填寫駁回原因' });
                return;
            }

            $.ajax({
                url: '<%= ResolveUrl("~/Services/BookingService.ashx") %>',
                type: 'POST',
                data: {
                    action: 'reject_booking',
                    bookingId: currentRejectBookingId,
                    rejectReason: reason
                },
                dataType: 'json',
                success: function (res) {
                    closeRejectModal();
                    if (res.Success) {
                        Swal.fire({
                            icon: 'success',
                            title: '已完成駁回',
                            text: res.Message,
                            confirmButtonColor: '#6B4E3D'
                        }).then(function () {
                            window.location.reload();
                        });
                    } else {
                        Swal.fire({ icon: 'error', title: '駁回處理失敗', text: res.Message });
                    }
                }
            });
        }
    </script>
</asp:Content>
