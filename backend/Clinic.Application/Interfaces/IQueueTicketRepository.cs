using Clinic.Domain.Entities;
using System;

namespace Clinic.Application.Interfaces
{
    public interface IQueueTicketRepository
    {
        /// <summary>
        /// Tính s? th? t? hàng d?i ti?p theo cho 1 bác si trong 1 ngày c? th? m?t cách
        /// ATOMIC (dùng MERGE + HOLDLOCK trên b?ng QueueCounters ? t?ng Infrastructure),
        /// d?m b?o không phát sinh s? trùng khi nhi?u check-in x?y ra d?ng th?i.
        /// Tr? v? 1 n?u bác si chua có b?nh nhân nào check-in trong ngày.
        /// </summary>
        Task<int> GetNextQueueNumberAsync(Guid doctorId, DateTime date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Thêm m?i 1 s? th? t? hàng d?i, sinh ra khi b?nh nhân check-in
        /// (t?i qu?y ho?c online).
        /// </summary>
        Task AddAsync(QueueTicket queueTicket);

        /// <summary>
        /// L?y 1 QueueTicket theo Id, kèm Appointment -> WorkSchedule (Doctor) và Appointment -> Patient -> Person
        /// d? ph?c v? authorization theo scope và hi?n th? tên b?nh nhân.
        /// </summary>
        Task<QueueTicket?> GetByIdAsync(Guid id);

        /// <summary>
        /// Lấy 1 QueueTicket theo AppointmentId.
        /// </summary>
        Task<QueueTicket?> GetByAppointmentIdAsync(Guid appointmentId);

        /// <summary>
        /// L?y toàn b? hàng d?i c?a 1 bác si trong 1 ngày c? th?, s?p theo Priority (uu tiên tru?c)
        /// r?i theo QueueNumber tang d?n - dùng cho dashboard xem hàng d?i (queue.view/doctor.queue.view).
        /// </summary>
        Task<List<QueueTicket>> GetByDoctorAsync(Guid doctorId, DateTime date);

        /// <summary>
        /// L?y vé dang ch? (Waiting) ti?p theo c?n g?i c?a 1 bác si trong ngày, uu tiên vé Priority = true,
        /// sau dó theo QueueNumber tang d?n. Tr? v? null n?u không còn ai dang ch?.
        /// </summary>
        Task<QueueTicket?> GetNextWaitingAsync(Guid doctorId, DateTime date);

        /// <summary>
        /// C?p nh?t 1 QueueTicket dã t?n t?i (g?i s?, b? qua lu?t, d?i uu tiên).
        /// </summary>
        Task UpdateAsync(QueueTicket queueTicket);

        /// <summary>
        /// L?y TOÀN B? vé hàng d?i dang "s?ng" (Waiting/Called/InProgress) c?a 1 b?nh nhân trong
        /// 1 ngày c? th?, kèm Appointment -> WorkSchedule -> Doctor -> Employee -> Person, s?p theo
        /// CheckInTime tang d?n. Tr? v? danh sách r?ng n?u b?nh nhân chua check-in ngày hôm dó ho?c
        /// dã du?c khám xong/b? qua. Dùng cho "theo dõi v? trí hàng d?i" c?a Patient.
        ///
        /// LUU Ý: 1 b?nh nhân có th? có nhi?u Appointment (khác bác si ho?c khác khung gi?) du?c
        /// Confirm và check-in trong cùng 1 ngày -> có th? có NHI?U vé active song song. Không du?c
        /// rút g?n v? 1 vé (ki?u FirstOrDefault theo CheckInTime m?i nh?t) vì s? làm "bi?n m?t" các
        /// vé còn l?i trong khi chúng v?n dang chi?m ch? th?t trong hàng d?i c?a bác si tuong ?ng.
        /// </summary>
        Task<List<QueueTicket>> GetActiveTicketsByPatientAsync(Guid patientId, DateTime date);

        /// <summary>
        /// L?y vé dang "active" (Called ho?c InProgress) c?a 1 bác si trong ngày - dùng d?
        /// ràng bu?c m?i bác si ch? có t?i da 1 vé dang du?c g?i/khám t?i m?t th?i di?m.
        /// Tr? v? null n?u bác si hi?n không có vé nào dang active (dang r?nh, s?n sàng g?i ti?p).
        /// </summary>
        Task<QueueTicket?> GetActiveTicketAsync(Guid doctorId, DateTime date);
    }
}