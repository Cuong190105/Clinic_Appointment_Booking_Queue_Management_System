using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Thông tin nghiệp vụ của nhân viên (Doctor/Receptionist/Admin).
    /// </summary>
    public class Employee : BaseEntity
    {
        private readonly TimeSpan VietnamTimeOffset = TimeSpan.FromHours(7); // Giờ Việt Nam (UTC+7)
        /// <summary>FK, UNIQUE.</summary>
        public Guid PersonId { get; protected set; }
        public Person Person { get; protected set; } = null!;

        public EmployeeStatus Status { get; protected set; } = EmployeeStatus.Active;

        public DateOnly HireDate { get; init; }

        /// <summary>Tự tham chiếu, optional - quản lý cấp trên (nếu nhóm có dùng tính năng này).</summary>
        public Guid? ManagerId { get; protected set; }
        public Employee? Manager { get; protected set; }

        /// <summary>0..1 - chỉ có giá trị nếu nhân viên này là bác sĩ.</summary>
        public Doctor? Doctor { get; protected set; }

        public Employee(Person person, DateOnly hireDate, Employee? manager = null, EmployeeStatus status = EmployeeStatus.Active)
        {
            var localToday = new TimeConverter().Today;
            if (person == null) throw new ArgumentNullException(nameof(person));
            if (hireDate > localToday.AddDays(30)) throw new ArgumentException("Hire date cannot be more than one month later from today.", nameof(hireDate));
            Person = person;
            PersonId = person.Id;
            HireDate = hireDate;
            Status = status;
            Manager = manager;
            ManagerId = manager?.Id;
        }

        public Employee(Guid id, Guid personId, DateOnly hireDate, EmployeeStatus status, Guid? managerId, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            PersonId = personId;
            ManagerId = managerId;
            HireDate = hireDate;
            Status = status;
        }

        public void AssignDoctor(Doctor doctor)
        {
            if (Person.User!.UserRoles.All(ur => ur.Role.Name != "Doctor"))
            {
                throw new InvalidOperationException("Cannot assign a doctor to an employee who is not a doctor.");
            }
            Doctor = doctor;
        }

        public void AssignManager(Employee? manager)
        {
            if (manager != null && manager.Id == Id)
            {
                throw new InvalidOperationException("An employee cannot be their own manager.");
            }
            Manager = manager;
            ManagerId = manager?.Id;
            MarkUpdated();
        }

        public void UpdateStatus(EmployeeStatus newStatus)
        {
            if (Status != newStatus)
            {
                Status = newStatus;
                MarkUpdated();
            }
        }

        public void UpdateManager(Employee? newManager)
        {
            if (ManagerId != newManager?.Id)
            {
                Manager = newManager;
                ManagerId = newManager?.Id;
                MarkUpdated();
            }
        }

        public void Delete()
        {
            if (!IsDeleted)
            {
                IsDeleted = true;
                MarkUpdated();
            }
        }
    }
}
