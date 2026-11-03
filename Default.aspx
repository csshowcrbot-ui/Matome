<%@ Page Title="週預約行事曆" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Matome.Default" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .calendar-controls-wrapper {
            display: flex;
            align-items: center;
            justify-content: space-between;
            flex-wrap: wrap;
            gap: 16px;
            margin-bottom: 20px;
        }
        .room-meta-pill {
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
    <!-- 上方會議室切換與圖例工具列 -->
    <div class="calendar-toolbar-box">
        <div class="filter-left-section">
            <div style="font-weight: 600; font-size: 15px; color: var(--color-text-primary); display: flex; align-items: center; gap: 6px;">
                <span>🏢</span> 會議場地：
            </div>
            <div class="room-select-wrapper">
                <asp:DropDownList ID="ddlRooms" runat="server" CssClass="form-control" ClientIDMode="Static">
                </asp:DropDownList>
            </div>
            <div id="roomMetaInfo" class="room-meta-pill">
                <span id="roomCapacityBadge">容納：--人</span>
                <span style="color: var(--color-border);">|</span>
                <span id="roomLocationBadge">位置：--</span>
                <span style="color: var(--color-border);">|</span>
                <span id="roomApprovalBadge" class="badge">--</span>
            </div>
        </div>

        <div style="display: flex; align-items: center; gap: 16px; flex-wrap: wrap;">
            <div class="calendar-legend-bar">
                <div class="legend-item">
                    <div class="legend-color-box leg-available"></div>
                    <span>可借用時段 (點擊預約)</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color-box leg-approved"></div>
                    <span>已核准借用</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color-box leg-pending"></div>
                    <span>審核中</span>
                </div>
                <div class="legend-item">
                    <div class="legend-color-box leg-mine"></div>
                    <span>我登記的時段</span>
                </div>
            </div>

            <button type="button" class="btn btn-primary btn-sm" onclick="openManualBookingDrawer();">
                <span>➕</span> 快速預約
            </button>
        </div>
    </div>

    <!-- FullCalendar 週時段視圖容器 -->
    <div id="weeklyCalendar"></div>

    <!-- 預約側邊抽屜 (Booking Drawer) -->
    <div id="drawerOverlay" class="drawer-overlay" onclick="closeBookingDrawer();"></div>
    <div id="drawerPanel" class="drawer-panel">
        <div class="drawer-header">
            <div class="drawer-title">
                <span>📝</span> 預約登記時段
            </div>
            <button type="button" class="btn-drawer-close" onclick="closeBookingDrawer();">&times;</button>
        </div>

        <div class="drawer-body">
            <!-- 當在「全部會議室」總覽模式時顯示會議室挑選下拉選單 -->
            <div class="form-group" id="drawerRoomSelectGroup" style="display: none;">
                <label class="form-label">選擇欲借用會議室 <span class="required">*</span></label>
                <select id="ddlDrawerRoom" class="form-control" onchange="onDrawerRoomChanged();">
                </select>
            </div>

            <!-- 場地說明卡 -->
            <div class="room-badge-card" id="drawerRoomBadgeCard">
                <div class="room-badge-name" id="drawerRoomName">會議室名稱</div>
                <div class="room-badge-desc">
                    <span id="drawerRoomLocation">地點：-</span>
                    <span id="drawerRoomCapacity">容納：-人</span>
                </div>
                <div style="margin-top: 8px;">
                    <span id="drawerRoomApproval" class="badge">免審核</span>
                    <div id="drawerMailNoticeHint" style="font-size: 12px; color: var(--color-brand-primary); margin-top: 6px; display: none;">
                        📧 確認送出後，系統將以 Email 自動通知審核主管進行審批
                    </div>
                </div>
            </div>

            <div class="form-group">
                <label class="form-label">預約日期 <span class="required">*</span></label>
                <input type="date" id="txtBookingDate" class="form-control" onchange="onBookingDateChanged();" />
            </div>

            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
                <label class="form-label" style="margin-bottom: 0;">預約時段 (30分鐘單位) <span class="required">*</span></label>
                <span style="font-size: 11.5px; color: var(--color-brand-primary); font-weight: 500;">⏰ 開放時間：06:00 ~ 22:00</span>
            </div>
            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                <div class="form-group">
                    <label class="form-label" style="font-size: 13px;">開始時間</label>
                    <select id="ddlStartTime" class="form-control" onchange="onStartTimeChanged();">
                        <!-- 30分鐘單位選項 -->
                    </select>
                </div>
                <div class="form-group">
                    <label class="form-label" style="font-size: 13px;">結束時間</label>
                    <select id="ddlEndTime" class="form-control" onchange="calculateRecurringSummary();">
                        <!-- 30分鐘單位選項 -->
                    </select>
                </div>
            </div>

            <!-- 每週定期預約區間設定 (和風極簡風格) -->
            <div class="recurring-setting-card" style="margin-bottom: 16px; padding: 12px 14px; background: #FAF9F6; border: 1px solid var(--color-border); border-radius: var(--border-radius-sm);">
                <label style="display: flex; align-items: center; cursor: pointer; user-select: none; font-weight: 500; color: var(--color-text-primary); margin-bottom: 0;">
                    <input type="checkbox" id="chkIsRecurring" onchange="toggleRecurringOptions();" style="width: 16px; height: 16px; accent-color: var(--color-brand-primary); margin-right: 8px; cursor: pointer;" />
                    <span>🔁 設定為每週定期會議 (跨週區間預約)</span>
                </label>
                <div id="recurringOptionsBox" style="display: none; margin-top: 12px; padding-top: 12px; border-top: 1px dashed var(--color-border);">
                    <div class="form-group" style="margin-bottom: 8px;">
                        <label class="form-label" style="font-size: 13px;">定期預約迄止日期 <span class="required">*</span></label>
                        <input type="date" id="txtRecurringEndDate" class="form-control" onchange="calculateRecurringSummary();" />
                        <div style="font-size: 12px; color: var(--color-text-secondary); margin-top: 4px;">
                            系統將在選取區間內，每週同一天及同一時段自動登記各場次
                        </div>
                    </div>
                    <div id="recurringSummaryAlert" style="font-size: 12.5px; padding: 8px 10px; background: #FFF; border: 1px solid #E2DED7; border-radius: 4px; color: var(--color-brand-primary); line-height: 1.5;">
                        <span id="recurringSummaryText">請設定迄止日期</span>
                    </div>
                </div>
            </div>

            <div class="form-group">
                <label class="form-label">會議事由 / 專案主題 <span class="required">*</span></label>
                <input type="text" id="txtSubject" class="form-control" placeholder="例如：Q4 產品策劃週會" />
            </div>
        </div>

        <div class="drawer-footer">
            <button type="button" class="btn btn-secondary" onclick="closeBookingDrawer();">取消關閉</button>
            <button type="button" id="btnSubmitBooking" class="btn btn-primary" onclick="submitBooking();">
                確認送出預約
            </button>
        </div>
    </div>

    <!-- 事件詳情彈窗 (Event Details Modal) -->
    <div id="eventDetailModal" class="modal-overlay">
        <div class="modal-dialog">
            <div class="modal-header">
                <div class="modal-title">📌 會議預約詳情</div>
                <button type="button" class="btn-drawer-close" onclick="closeEventDetailModal();">&times;</button>
            </div>
            <div class="modal-body" style="font-size: 14px; line-height: 1.8;">
                <div style="font-size: 16px; font-weight: 600; color: var(--color-brand-primary); margin-bottom: 12px;" id="mdlSubject">
                    會議事由
                </div>
                <div style="display: grid; grid-template-columns: 80px 1fr; gap: 8px;">
                    <div style="color: var(--color-text-secondary);">會議場地：</div>
                    <div id="mdlRoom" style="font-weight: 500;"></div>

                    <div style="color: var(--color-text-secondary);">預約時段：</div>
                    <div id="mdlTime" style="font-weight: 500;"></div>

                    <div style="color: var(--color-text-secondary);">申 請 人：</div>
                    <div id="mdlApplicant"></div>

                    <div style="color: var(--color-text-secondary);">審核狀態：</div>
                    <div id="mdlStatus"></div>

                    <div style="color: var(--color-text-secondary);">附帶設備：</div>
                    <div id="mdlEquipments"></div>
                </div>
            </div>
            <div class="modal-footer" id="mdlFooter">
                <button type="button" class="btn btn-secondary" onclick="closeEventDetailModal();">關閉</button>
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        var calendarInstance = null;
        var roomsData = <%= RoomsJson %>;

        // 初始化 30 分鐘時間選單 (上午 06:00 至 晚上 22:00)
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
            updateRoomMetaInfo();

            var calendarEl = document.getElementById('weeklyCalendar');
            calendarInstance = new FullCalendar.Calendar(calendarEl, {
                initialView: 'timeGridWeek',
                slotDuration: '00:30:00', // 30 分鐘一刻度
                slotMinTime: '06:00:00',
                slotMaxTime: '22:00:00',
                allDaySlot: false,
                locale: 'zh-tw',
                firstDay: 1, // 週一開始
                headerToolbar: {
                    left: 'prev,next today',
                    center: 'title',
                    right: 'dayGridMonth,timeGridWeek,timeGridDay'
                },
                buttonText: {
                    today: '今天',
                    month: '整月份',
                    week: '整週時段',
                    day: '單日明細'
                },
                dayMaxEvents: 4,
                selectable: true,
                selectMirror: true,
                selectOverlap: false,
                dateClick: function (info) {
                    if (calendarInstance.view.type === 'dayGridMonth') {
                        var clickedDate = new Date(info.dateStr + 'T09:00:00');
                        var endDate = new Date(info.dateStr + 'T10:00:00');
                        openBookingDrawerWithRange(clickedDate, endDate);
                    }
                },
                select: function (info) {
                    openBookingDrawerWithRange(info.start, info.end);
                },
                events: function (fetchInfo, successCallback, failureCallback) {
                    var currentRoomId = $('#ddlRooms').val();
                    $.ajax({
                        url: '<%= ResolveUrl("~/Services/BookingService.ashx") %>',
                        type: 'GET',
                        data: {
                            action: 'get_events',
                            roomId: currentRoomId,
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
                    openEventDetailModal(info.event);
                }
            });

            calendarInstance.render();

            $('#ddlRooms').on('change', function () {
                updateRoomMetaInfo();
                calendarInstance.refetchEvents();
            });
        });

        function updateRoomMetaInfo() {
            var selectedId = parseInt($('#ddlRooms').val());
            if (selectedId === 0) {
                $('#roomCapacityBadge').text('模式：所有會議室總覽');
                $('#roomLocationBadge').text('地點：全區場地借用動態');
                $('#roomApprovalBadge').attr('class', 'badge badge-approved').text('即時全覽');
                return;
            }
            var room = roomsData.find(function (r) { return r.RoomId === selectedId; });
            if (room) {
                $('#roomCapacityBadge').text('容納：' + room.Capacity + ' 人');
                $('#roomLocationBadge').text('位置：' + (room.Location || '未指定'));
                if (room.IsApprovalRequired) {
                    $('#roomApprovalBadge').attr('class', 'badge badge-pending').text('需審核 (' + room.ApprovalDeptName + ')');
                } else {
                    $('#roomApprovalBadge').attr('class', 'badge badge-approved').text('免審核·即選即用');
                }
            }
        }

        function updateDrawerRoomCard(room) {
            if (!room) return;
            $('#drawerRoomName').text(room.RoomName);
            $('#drawerRoomLocation').text('地點：' + (room.Location || '未指定'));
            $('#drawerRoomCapacity').text('容納：' + room.Capacity + ' 人');
            if (room.IsApprovalRequired) {
                $('#drawerRoomApproval').attr('class', 'badge badge-pending').text('此場地預約需經【' + room.ApprovalDeptName + '】審核');
                $('#drawerMailNoticeHint').show().html('📧 確認送出後，系統將以 Email 自動通知【' + room.ApprovalDeptName + '】審核主管進行審批');
            } else {
                $('#drawerRoomApproval').attr('class', 'badge badge-approved').text('免審核 · 預約完成即確認');
                $('#drawerMailNoticeHint').hide();
            }
        }

        function onDrawerRoomChanged() {
            var roomId = parseInt($('#ddlDrawerRoom').val());
            var room = roomsData.find(function (r) { return r.RoomId === roomId; });
            if (room) {
                updateDrawerRoomCard(room);
            }
        }

        function openBookingDrawerWithRange(startDate, endDate) {
            var selectedId = parseInt($('#ddlRooms').val());
            var ddlDrawerRoom = $('#ddlDrawerRoom');
            ddlDrawerRoom.empty();

            var activeRooms = roomsData.filter(function (r) { return r.RoomId > 0; });
            activeRooms.forEach(function (r) {
                ddlDrawerRoom.append(new Option(r.RoomName + ' (' + r.Capacity + '人 / ' + (r.Location || '') + ')', r.RoomId));
            });

            if (selectedId === 0) {
                $('#drawerRoomSelectGroup').show();
                if (activeRooms.length > 0) {
                    ddlDrawerRoom.val(activeRooms[0].RoomId);
                    updateDrawerRoomCard(activeRooms[0]);
                }
            } else {
                $('#drawerRoomSelectGroup').hide();
                var room = roomsData.find(function (r) { return r.RoomId === selectedId; });
                if (!room) return;
                updateDrawerRoomCard(room);
            }

            var dateStr = formatDate(startDate);
            $('#txtBookingDate').val(dateStr);

            var sTime = formatTime(startDate);
            var eTime = formatTime(endDate);
            $('#ddlStartTime').val(sTime);
            $('#ddlEndTime').val(eTime);

            $('#txtSubject').val('');

            // 重設每週定期預約開關
            $('#chkIsRecurring').prop('checked', false);
            $('#recurringOptionsBox').hide();
            $('#txtRecurringEndDate').val('');

            $('#drawerOverlay').addClass('active');
            $('#drawerPanel').addClass('active');
        }

        function openManualBookingDrawer() {
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
            openBookingDrawerWithRange(now, end);
        }

        function closeBookingDrawer() {
            $('#drawerOverlay').removeClass('active');
            $('#drawerPanel').removeClass('active');
        }

        function toggleRecurringOptions() {
            var isChecked = $('#chkIsRecurring').is(':checked');
            if (isChecked) {
                $('#recurringOptionsBox').slideDown(150);
                var startDateStr = $('#txtBookingDate').val();
                if (startDateStr) {
                    var sDate = new Date(startDateStr + 'T00:00:00');
                    sDate.setDate(sDate.getDate() + 28); // 預設 4 週後
                    var endStr = formatDate(sDate);
                    $('#txtRecurringEndDate').val(endStr);
                    $('#txtRecurringEndDate').attr('min', startDateStr);
                }
                calculateRecurringSummary();
            } else {
                $('#recurringOptionsBox').slideUp(150);
            }
        }

        function onBookingDateChanged() {
            var startDateStr = $('#txtBookingDate').val();
            if (startDateStr) {
                $('#txtRecurringEndDate').attr('min', startDateStr);
                var endDateStr = $('#txtRecurringEndDate').val();
                if (endDateStr && endDateStr < startDateStr) {
                    var sDate = new Date(startDateStr + 'T00:00:00');
                    sDate.setDate(sDate.getDate() + 28);
                    $('#txtRecurringEndDate').val(formatDate(sDate));
                }
            }
            if ($('#chkIsRecurring').is(':checked')) {
                calculateRecurringSummary();
            }
        }

        function calculateRecurringSummary() {
            var startDateStr = $('#txtBookingDate').val();
            var endDateStr = $('#txtRecurringEndDate').val();
            var sTime = $('#ddlStartTime').val();
            var eTime = $('#ddlEndTime').val();

            if (!startDateStr || !endDateStr) {
                $('#recurringSummaryText').text('請設定迄止日期');
                return;
            }

            var sDate = new Date(startDateStr + 'T00:00:00');
            var eDate = new Date(endDateStr + 'T00:00:00');

            if (eDate < sDate) {
                $('#recurringSummaryText').html('<span style="color:#B85D56;">⚠️ 迄止日期不能早於開始預約日期</span>');
                return;
            }

            var dayOfWeekNames = ['週日', '週一', '週二', '週三', '週四', '週五', '週六'];
            var dayName = dayOfWeekNames[sDate.getDay()];

            var count = 0;
            var cur = new Date(sDate.getTime());
            while (cur <= eDate) {
                count++;
                cur.setDate(cur.getDate() + 7);
            }

            $('#recurringSummaryText').html('📌 將於<strong>每' + dayName + ' ' + sTime + ' ~ ' + eTime + '</strong>預約，區間內預計共 <strong>' + count + '</strong> 場會議。');
        }

        function onStartTimeChanged() {
            var sVal = $('#ddlStartTime').val();
            // 自動調整結束時間為開始後 1 小時
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

            if ($('#chkIsRecurring').is(':checked')) {
                calculateRecurringSummary();
            }
        }

        function submitBooking() {
            var roomId = parseInt($('#ddlRooms').val());
            if (roomId <= 0) {
                roomId = parseInt($('#ddlDrawerRoom').val());
            }
            if (!roomId || roomId <= 0) {
                Swal.fire({ icon: 'warning', title: '請選擇欲借用的會議室' });
                return;
            }
            var date = $('#txtBookingDate').val();
            var sTime = $('#ddlStartTime').val();
            var eTime = $('#ddlEndTime').val();
            var subject = $.trim($('#txtSubject').val());

            if (!date) {
                Swal.fire({ icon: 'warning', title: '請選擇預約日期' });
                return;
            }
            if (sTime >= eTime) {
                Swal.fire({ icon: 'warning', title: '預約時段不正確', text: '結束時間必須晚於開始時間。' });
                return;
            }
            if (sTime < '06:00' || eTime > '22:00') {
                Swal.fire({ icon: 'warning', title: '超出開放借用時間', text: '會議場地開放借用時間為每日上午 06:00 至下午 10:00 (22:00)。' });
                return;
            }
            if (!subject) {
                Swal.fire({ icon: 'warning', title: '請填寫會議事由 / 專案主題' });
                return;
            }

            var isRecurring = $('#chkIsRecurring').is(':checked');
            var recurringEndDate = isRecurring ? $('#txtRecurringEndDate').val() : '';

            if (isRecurring && !recurringEndDate) {
                Swal.fire({ icon: 'warning', title: '請選擇每週定期預約的迄止日期' });
                return;
            }
            if (isRecurring && recurringEndDate < date) {
                Swal.fire({ icon: 'warning', title: '定期預約迄止日期不可早於預約開始日期' });
                return;
            }

            var startStr = date + ' ' + sTime;
            var endStr = date + ' ' + eTime;

            var payload = {
                roomId: roomId,
                subject: subject,
                startTime: startStr,
                endTime: endStr,
                isRecurring: isRecurring,
                recurringEndDate: recurringEndDate,
                equipments: []
            };

            $('#btnSubmitBooking').prop('disabled', true).text('提交中...');

            $.ajax({
                url: '<%= ResolveUrl("~/Services/BookingService.ashx?action=create_booking") %>',
                type: 'POST',
                data: JSON.stringify(payload),
                contentType: 'application/json',
                dataType: 'json',
                success: function (res) {
                    $('#btnSubmitBooking').prop('disabled', false).text('確認送出預約');
                    if (res.Success) {
                        closeBookingDrawer();
                        Swal.fire({
                            icon: 'success',
                            title: (res.Data && res.Data.status === 'Pending') ? '申請已送出' : '預約成功',
                            text: res.Message,
                            confirmButtonColor: '#6B4E3D'
                        }).then(function () {
                            calendarInstance.refetchEvents();
                        });
                    } else {
                        Swal.fire({
                            icon: 'error',
                            title: '預約未完成',
                            text: res.Message,
                            confirmButtonColor: '#6B4E3D'
                        });
                    }
                },
                error: function (xhr) {
                    $('#btnSubmitBooking').prop('disabled', false).text('確認送出預約');
                    Swal.fire({
                        icon: 'error',
                        title: '通訊失敗',
                        text: '無法連線至伺服器，請稍後重試。',
                        confirmButtonColor: '#6B4E3D'
                    });
                }
            });
        }

        function openEventDetailModal(event) {
            var props = event.extendedProps || {};
            $('#mdlSubject').text(props.subject || event.title);
            $('#mdlRoom').text(props.roomName || '未指定');
            $('#mdlTime').text(formatEventTimeRange(event.start, event.end));
            $('#mdlApplicant').text((props.userName || '同仁') + ' (' + (props.deptName || '未指定部門') + ')');

            var statusHtml = '';
            if (props.status === 'Approved') {
                statusHtml = '<span class="badge badge-approved">已核准借用</span>';
            } else if (props.status === 'Pending') {
                statusHtml = '<span class="badge badge-pending">審核中</span>';
            } else {
                statusHtml = '<span class="badge badge-cancelled">' + props.status + '</span>';
            }
            if (props.isMine) {
                statusHtml += ' <span class="badge badge-mine">👤 我的預約</span>';
            }
            $('#mdlStatus').html(statusHtml);

            $('#mdlEquipments').text(props.equipments || '無借用附加設備');

            var footerHtml = '<button type="button" class="btn btn-secondary" onclick="closeEventDetailModal();">關閉</button>';
            if (props.isMine && (props.status === 'Pending' || props.status === 'Approved')) {
                footerHtml = `
                    <button type="button" class="btn btn-araigaki" onclick="cancelBookingFromModal(${props.bookingId});">
                        <span>🗑️</span> 取消借用申請
                    </button>
                    ${footerHtml}
                `;
            }
            $('#mdlFooter').html(footerHtml);

            $('#eventDetailModal').addClass('active');
        }

        function closeEventDetailModal() {
            $('#eventDetailModal').removeClass('active');
        }

        function cancelBookingFromModal(bookingId) {
            Swal.fire({
                title: '確認釋出此時段？',
                text: '取消後將自動釋出會議室與佔用的附加設備庫存。',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#B85D56',
                cancelButtonColor: '#70706E',
                confirmButtonText: '確定取消',
                cancelButtonText: '保留時段'
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
                                closeEventDetailModal();
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

        function escapeHtml(text) {
            if (!text) return '';
            return $('<div>').text(text).html();
        }
    </script>
</asp:Content>
