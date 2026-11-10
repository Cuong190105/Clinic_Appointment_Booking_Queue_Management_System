using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// B? d?m s? th? t? hàng d?i, theo t?ng bác si và t?ng ngày.
    /// Dùng d? sinh QueueNumber tang d?n m?t cách atomic b?ng câu l?nh
    /// MERGE trong QueueTicketRepository.GetNextQueueNumberAsync, tránh
    /// trùng s? khi nhi?u b?nh nhân check-in d?ng th?i cho cùng 1 bác si/ngày.
    /// Khóa chính composite (DoctorId, Date) - m?i bác si/ngày ch? có dúng 1 dòng.
    /// </summary>
    public class QueueCounter
    {
        public Guid DoctorId { get; set; }

        public DateOnly Date { get; set; }

        /// <summary>S? th? t? l?n nh?t dã c?p cho bác si này trong ngày này.</summary>
        public int CurrentNumber { get; set; }
    }
}