using System;
using System.Collections.Generic;

namespace Matome.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Account { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public int? DeptId { get; set; }
        public string DeptName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; } // 'Admin', 'Approver', 'User'
        public bool IsActive { get; set; }
    }

    public class Department
    {
        public int DeptId { get; set; }
        public string DeptName { get; set; }
    }

    public class Room
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int Capacity { get; set; }
        public string Location { get; set; }
        public bool IsApprovalRequired { get; set; }
        public int? ApprovalDeptId { get; set; }
        public string ApprovalDeptName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public class Equipment
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public int TotalQuantity { get; set; }
        public bool IsActive { get; set; }
    }

    public class Booking
    {
        public int BookingId { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserDeptName { get; set; }
        public string Subject { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } // 'Pending', 'Approved', 'Rejected', 'Cancelled'
        public int? ApprovedBy { get; set; }
        public string ApproverName { get; set; }
        public DateTime? ApprovedTime { get; set; }
        public string RejectReason { get; set; }
        public DateTime CreateTime { get; set; }
        public List<BookingEquipment> Equipments { get; set; }
        public bool HasConflict { get; set; }

        public Booking()
        {
            Equipments = new List<BookingEquipment>();
        }
    }

    public class BookingEquipment
    {
        public int DetailId { get; set; }
        public int BookingId { get; set; }
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public int Quantity { get; set; }
    }

    public class EquipmentItem
    {
        public int EquipmentId { get; set; }
        public int Quantity { get; set; }
    }

    public class EquipmentBooking
    {
        public int BookingId { get; set; }
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserDeptName { get; set; }
        public string Subject { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } // 'Pending', 'Approved', 'Rejected', 'Cancelled'
        public int? ApprovedBy { get; set; }
        public string ApproverName { get; set; }
        public DateTime? ApprovedTime { get; set; }
        public string RejectReason { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public class EquipmentAvailabilityDto
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public int TotalQuantity { get; set; }
        public int UsedQuantity { get; set; }
        public int AvailableQuantity { get; set; }
    }

    public class CalendarEventDto
    {
        public string id { get; set; }
        public string title { get; set; }
        public string start { get; set; }
        public string end { get; set; }
        public string className { get; set; }
        public Dictionary<string, object> extendedProps { get; set; }

        public CalendarEventDto()
        {
            extendedProps = new Dictionary<string, object>();
        }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
