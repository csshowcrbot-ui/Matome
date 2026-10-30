<%@ Page Title="設備借用預約" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EquipmentBooking.aspx.cs" Inherits="Matome.EquipmentBookingPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .equip-meta-pill {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 6px 14px;
            background-color: var(--color-surface);
            border: 1px solid var(--color-border);
            border-radius: var(--radius-sm);
            font-size: 13px;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <!-- 上方設備切換與圖例工具列 -->
    <div class="calendar-toolbar-box">
        <div class="filter-left-section">
            <div style="font-weight: 600; font-size: 15px; color: var(--color-text-primary); display: flex; align-items: center; gap: 6px;">
                <span>💻</span> 借用設備資產：
            </div>
            <div class="room-select-wrapper">
                <asp:DropDownList ID="ddlEquipments" runat="server" CssClass="form-control" ClientIDMode="Static">
                </asp:DropDownList>
            </div>
            <div id="equipMetaInfo" class="equip-meta-pill">
                <span id="equipTotalBadge">總庫存：--</span>
                <span style="color: var(--color-border);">|</span>
                <span id="equipStatusBadge" class="badge badge-approved">正常可用</span>
            </div>
        </div>

        <div style="display: flex; align-items: center; gap: 16px; flex-wrap: wrap;">
            <div class="calendar-legend-bar">
                <div class="legend-item">
                    <div class="legend-color-box leg-available"></div>
                    <span>可借用時段 (點擊登記)</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color-box leg-approved"></div>
                    <span>已被他人借用</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color-box leg-mine"></div>
                    <span>我登記借用</span>
                </div>
            </div>

            <button type="button" class="btn btn-primary btn-sm" onclick="openManualEquipDrawer();">
                <span>➕</span> 快速借用
            </button>
        </div>
    </div>

    <!-- FullCalendar 週時段視圖容器 -->
    <div id="equipmentCalendar"></div>

    <!-- 獨立設備借用抽屜 (Equipment Booking Drawer) -->
    <div id="drawerOverlay" class="drawer-overlay" onclick="closeEquipDrawer();"></div>
    <div id="drawerPanel" class="drawer-panel">
        <div class="drawer-header">
            <div class="drawer-title">
                <span>💻</span> 獨立設備借用登記
            </div>
            <button type="button" class="btn-drawer-close" onclick="closeEquipDrawer();">&times;</button>
        </div>

        <div class="drawer-body">
            <!-- 設備資訊卡 -->
            <div class="room-badge-card">
                <div class="room-badge-name" id="drawerEquipName">設備名稱</div>
                <div class="room-badge-desc">
                    <span id="drawerEquipTotal">總配額：-</span>
                    <span id="drawerSlotAvail" style="color: var(--color-accent-matcha); font-weight: 600;">時段可用量計算中...</span>
                </div>
            </div>

            <div class="form-group">
                <label class="form-label">借用日期 <span class="required">*</span></label>
                <input type="date" id="txtEquipDate" class="form-control" onchange="checkSlotAvailable();" />
            </div>

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                <div class="form-group">
                    <label class="form-label">開始時間 <span class="required">*</span></label>
                    <select id="ddlStartTime" class="form-control" onchange="onStartTimeChanged();">
                    </select>
                </div>
                <div class="form-group">
                    <label class="form-label">結束時間 <span class="required">*</span></label>
                    <select id="ddlEndTime" class="form-control" onchange="checkSlotAvailable();">
                    </select>
                </div>
            </div>

            <div class="form-group">
                <label class="form-label">申請借用數量 <span class="required">*</span></label>
                <div style="display: flex; align-items: center; gap: 10px;">
                    <input type="number" id="txtBorrowQty" class="form-control" style="width: 100px;" value="1" min="1" max="1" />
                    <span id="lblQtyHint" style="font-size: 13px; color: var(--color-text-secondary);"></span>
                </div>
            </div>

            <div class="form-group">
                <label class="form-label">借用用途 / 專案事由 <span class="required">*</span></label>
                <input type="text" id="txtEquipSubject" class="form-control" placeholder="例如：外部研討會展示、客戶拜訪簡報" />
            </div>
        </div>

        <div class="drawer-footer">
            <button type="button" class="btn btn-secondary" onclick="closeEquipDrawer();">取消關閉</button>
            <button type="button" id="btnSubmitEquipBooking" class="btn btn-primary" onclick="submitEquipBooking();">
                確認送出借用
            </button>
        </div>
    </div>

    <!-- 事件詳情彈窗 (Event Detail Modal) -->
    <div id="equipDetailModal" class="modal-overlay">
        <div class="modal-dialog">
            <div class="modal-header">
                <div class="modal-title">📌 設備借用詳情</div>
                <button type="button" class="btn-drawer-close" onclick="closeEquipDetailModal();">&times;</button>
            </div>
            <div class="modal-body" style="font-size: 14px; line-height: 1.8;">
                <div style="font-size: 16px; font-weight: 600; color: var(--color-brand-primary); margin-bottom: 12px;" id="mdlEquipSubject">
                    借用事由
                </div>
                <div style="display: grid; grid-template-columns: 80px 1fr; gap: 8px;">
                    <div style="color: var(--color-text-secondary);">借用設備：</div>
                    <div id="mdlEquipName" style="font-weight: 500;"></div>

                    <div style="color: var(--color-text-secondary);">借用數量：</div>
                    <div id="mdlEquipQty" style="font-weight: 600; color: var(--color-brand-primary);"></div>

                    <div style="color: var(--color-text-secondary);">借用時段：</div>
                    <div id="mdlEquipTime" style="font-weight: 500;"></div>

                    <div style="color: var(--color-text-secondary);">申 請 人：</div>
                    <div id="mdlEquipApplicant"></div>

                    <div style="color: var(--color-text-secondary);">借用狀態：</div>
                    <div id="mdlEquipStatus"></div>
                </div>
            </div>
            <div class="modal-footer" id="mdlEquipFooter">
                <button type="button" class="btn btn-secondary" onclick="closeEquipDetailModal();">關閉</button>
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        var calendarInstance = null;
        var equipmentsData = <%= EquipmentsJson %>;
        var currentSlotAvailableQty = 0;

        function initTimeOptions() {
            var startSelect = $('#ddlStartTime');
            var endSelect = $('#ddlEndTime');
            startSelect.empty();
            endSelect.empty();

            var startHour = 6;
            var endHour = 22;

            for (var h = startHour; h <= endHour; h++) {
                for (var m of [0, 30]) {
                    if (h === endHour && m === 30) continue;
                    var timeStr = (h < 10 ? '0' + h : h) + ':' + (m === 0 ? '00' : '30');
                    if (h < endHour) {
                        startSelect.append(new Option(timeStr, timeStr));
                    }
                    if (h > startHour || (h === startHour && m === 30)) {
                        endSelect.append(new Option(timeStr, timeStr));
                    }
                }
            }
        }

        $(document).ready(function () {
            initTimeOptions();
            updateEquipMetaInfo();

            var calendarEl = document.getElementById('equipmentCalendar');
            calendarInstance = new FullCalendar.Calendar(calendarEl, {
                initialView: 'timeGridWeek',
                slotDuration: '00:30:00',
                slotMinTime: '06:00:00',
                slotMaxTime: '22:00:00',
                allDaySlot: false,
                locale: 'zh-tw',
                firstDay: 1,
                headerToolbar: {
                    left: 'prev,next today',
                    center: 'title',
                    right: ''
                },
                buttonText: {
                    today: '返回本週'
                },
                selectable: true,
                selectMirror: true,
                selectOverlap: true, // 設備有多個庫存，允許重疊借用
                select: function (info) {
                    openEquipDrawerWithRange(info.start, info.end);
                },
                events: function (fetchInfo, successCallback, failureCallback) {
                    var currentEqId = $('#ddlEquipments').val();
                    $.ajax({
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx") %>',
                        type: 'GET',
                        data: {
                            action: 'get_equipment_events',
                            equipmentId: currentEqId,
                            start: fetchInfo.startStr,
                            end: fetchInfo.endStr
                        },
                        dataType: 'json',
                        success: function (data) {
                            successCallback(data);
                        },
                        error: function () {
                            failureCallback();
                        }
                    });
                },
                eventClick: function (info) {
                    openEquipDetailModal(info.event);
                }
            });

            calendarInstance.render();

            $('#ddlEquipments').on('change', function () {
                updateEquipMetaInfo();
                calendarInstance.refetchEvents();
            });
        });

        function updateEquipMetaInfo() {
            var selectedId = parseInt($('#ddlEquipments').val());
            var eq = equipmentsData.find(function (e) { return e.EquipmentId === selectedId; });
            if (eq) {
                $('#equipTotalBadge').text('總庫存額度：' + eq.TotalQuantity + ' 組/套');
                $('#equipStatusBadge').attr('class', 'badge badge-approved').text('正常可用');
            }
        }

        function openEquipDrawerWithRange(startDate, endDate) {
            var selectedId = parseInt($('#ddlEquipments').val());
            var eq = equipmentsData.find(function (e) { return e.EquipmentId === selectedId; });
            if (!eq) return;

            $('#drawerEquipName').text(eq.EquipmentName);
            $('#drawerEquipTotal').text('總配額：' + eq.TotalQuantity + ' 組/套');

            var dateStr = formatDate(startDate);
            $('#txtEquipDate').val(dateStr);

            var sTime = formatTime(startDate);
            var eTime = formatTime(endDate);
            $('#ddlStartTime').val(sTime);
            $('#ddlEndTime').val(eTime);

            $('#txtBorrowQty').val('1');
            $('#txtEquipSubject').val('');

            $('#drawerOverlay').addClass('active');
            $('#drawerPanel').addClass('active');

            checkSlotAvailable();
        }

        function openManualEquipDrawer() {
            var now = new Date();
            if (now.getHours() < 6) {
                now.setHours(6, 0, 0, 0);
            } else if (now.getHours() >= 21) {
                now.setHours(21, 0, 0, 0);
            } else {
                now.setMinutes(now.getMinutes() >= 30 ? 60 : 30, 0, 0);
            }
            var end = new Date(now.getTime() + 60 * 60 * 1000);
            if (end.getHours() > 22 || (end.getHours() === 22 && end.getMinutes() > 0)) {
                end.setHours(22, 0, 0, 0);
            }
            openEquipDrawerWithRange(now, end);
        }

        function closeEquipDrawer() {
            $('#drawerOverlay').removeClass('active');
            $('#drawerPanel').removeClass('active');
        }

        function onStartTimeChanged() {
            var sVal = $('#ddlStartTime').val();
            var parts = sVal.split(':');
            var h = parseInt(parts[0]);
            var m = parseInt(parts[1]);
            var nextH = h + 1;
            var nextM = m;
            if (nextH > 22 || (nextH === 22 && nextM > 0)) {
                nextH = 22;
                nextM = 0;
            }
            var eVal = (nextH < 10 ? '0' + nextH : nextH) + ':' + (nextM === 0 ? '00' : '30');
            $('#ddlEndTime').val(eVal);

            checkSlotAvailable();
        }

        function checkSlotAvailable() {
            var eqId = parseInt($('#ddlEquipments').val());
            var date = $('#txtEquipDate').val();
            var sTime = $('#ddlStartTime').val();
            var eTime = $('#ddlEndTime').val();

            if (!date || !sTime || !eTime) return;

            var startStr = date + ' ' + sTime + ':00';
            var endStr = date + ' ' + eTime + ':00';

            $('#drawerSlotAvail').text('計算可用量中...');

            $.ajax({
                url: '<%= ResolveUrl("~/Services/BookingService.ashx") %>',
                type: 'GET',
                data: {
                    action: 'get_equipment_slot_avail',
                    equipmentId: eqId,
                    startTime: startStr,
                    endTime: endStr
                },
                dataType: 'json',
                success: function (res) {
                    if (res.Success) {
                        currentSlotAvailableQty = res.Data.availableQuantity;
                        $('#drawerSlotAvail').html(`此時段剩餘可用：<strong style="font-size: 14px;">${currentSlotAvailableQty}</strong> 個`);
                        $('#txtBorrowQty').attr('max', currentSlotAvailableQty);
                        $('#lblQtyHint').text(`(最多可借 ${currentSlotAvailableQty} 個)`);

                        if (currentSlotAvailableQty <= 0) {
                            $('#drawerSlotAvail').css('color', '#B85D56');
                            $('#btnSubmitEquipBooking').prop('disabled', true);
                        } else {
                            $('#drawerSlotAvail').css('color', '#5A6B5C');
                            $('#btnSubmitEquipBooking').prop('disabled', false);
                        }
                    }
                }
            });
        }

        function submitEquipBooking() {
            var eqId = parseInt($('#ddlEquipments').val());
            var date = $('#txtEquipDate').val();
            var sTime = $('#ddlStartTime').val();
            var eTime = $('#ddlEndTime').val();
            var qty = parseInt($('#txtBorrowQty').val()) || 1;
            var subject = $.trim($('#txtEquipSubject').val());

            if (!date) {
                Swal.fire({ icon: 'warning', title: '請選擇借用日期' });
                return;
            }
            if (!subject) {
                Swal.fire({ icon: 'warning', title: '請填寫借用用途 / 專案事由' });
                return;
            }
            if (qty <= 0 || qty > currentSlotAvailableQty) {
                Swal.fire({ icon: 'warning', title: '借用數量無效', text: `所選數量不可超過該時段剩餘可用額度 (${currentSlotAvailableQty})。` });
                return;
            }

            var startStr = date + ' ' + sTime;
            var endStr = date + ' ' + eTime;

            var payload = {
                equipmentId: eqId,
                subject: subject,
                startTime: startStr,
                endTime: endStr,
                quantity: qty
            };

            $('#btnSubmitEquipBooking').prop('disabled', true).text('提交中...');

            $.ajax({
                url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=create_equipment_booking") %>',
                type: 'POST',
                data: JSON.stringify(payload),
                contentType: 'application/json',
                dataType: 'json',
                success: function (res) {
                    $('#btnSubmitEquipBooking').prop('disabled', false).text('確認送出借用');
                    if (res.Success) {
                        closeEquipDrawer();
                        Swal.fire({
                            icon: 'success',
                            title: '借用預約成功',
                            text: res.Message,
                            confirmButtonColor: '#6B4E3D'
                        }).then(function () {
                            calendarInstance.refetchEvents();
                        });
                    } else {
                        Swal.fire({ icon: 'error', title: '預約失敗', text: res.Message });
                    }
                },
                error: function () {
                    $('#btnSubmitEquipBooking').prop('disabled', false).text('確認送出借用');
                    Swal.fire({ icon: 'error', title: '通訊失敗', text: '伺服器未回應，請稍後重試。' });
                }
            });
        }

        function openEquipDetailModal(event) {
            var props = event.extendedProps || {};
            $('#mdlEquipSubject').text(props.subject || event.title);
            $('#mdlEquipName').text(props.equipmentName || '未指定設備');
            $('#mdlEquipQty').text(props.quantity + ' 組/套');
            $('#mdlEquipTime').text(formatEventTimeRange(event.start, event.end));
            $('#mdlEquipApplicant').text((props.userName || '同仁') + ' (' + (props.deptName || '未指定部門') + ')');

            var statusHtml = '<span class="badge badge-approved">✓ 借用已確認</span>';
            if (props.isMine) {
                statusHtml += ' <span class="badge badge-mine">👤 我的借用</span>';
            }
            $('#mdlEquipStatus').html(statusHtml);

            var footerHtml = '<button type="button" class="btn btn-secondary" onclick="closeEquipDetailModal();">關閉</button>';
            if (props.isMine && props.status === 'Approved') {
                footerHtml = `
                    <button type="button" class="btn btn-araigaki" onclick="cancelEquipFromModal(${props.bookingId});">
                        <span>🗑️</span> 取消設備借用
                    </button>
                    ${footerHtml}
                `;
            }
            $('#mdlEquipFooter').html(footerHtml);

            $('#equipDetailModal').addClass('active');
        }

        function closeEquipDetailModal() {
            $('#equipDetailModal').removeClass('active');
        }

        function cancelEquipFromModal(bookingId) {
            Swal.fire({
                title: '確認取消此設備借用？',
                text: '取消後額度將即時釋出供其他同仁登記借用。',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#B85D56',
                cancelButtonColor: '#70706E',
                confirmButtonText: '確定取消',
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
                                closeEquipDetailModal();
                                Swal.fire({
                                    icon: 'success',
                                    title: '取消成功',
                                    text: res.Message,
                                    confirmButtonColor: '#6B4E3D'
                                });
                                calendarInstance.refetchEvents();
                            } else {
                                Swal.fire({ icon: 'error', title: '取消失敗', text: res.Message });
                            }
                        }
                    });
                }
            });
        }

        function formatDate(d) {
            var year = d.getFullYear();
            var month = ('0' + (d.getMonth() + 1)).slice(-2);
            var day = ('0' + d.getDate()).slice(-2);
            return year + '-' + month + '-' + day;
        }

        function formatTime(d) {
            var hours = ('0' + d.getHours()).slice(-2);
            var minutes = ('0' + d.getMinutes()).slice(-2);
            return hours + ':' + minutes;
        }

        function formatEventTimeRange(start, end) {
            return formatDate(start) + ' ' + formatTime(start) + ' ~ ' + formatTime(end);
        }
    </script>
</asp:Content>

