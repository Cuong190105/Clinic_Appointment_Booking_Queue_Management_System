using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System.Text.RegularExpressions;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Thông tin cá nhân dùng chung cho mọi actor.
    /// Không bắt buộc phải có User -> cho phép lưu hồ sơ khách vãng lai (walk-in)
    /// không cần tài khoản đăng nhập.
    /// </summary>
    public class Person : BaseEntity
    {
        public string FullName { get; set; } = string.Empty;

        /// <summary>UNIQUE. Dùng để tra cứu tránh tạo trùng hồ sơ khi khách vãng lai.</summary>
        public string? PhoneNumber { get; set; } = string.Empty;

        public string? Email { get; set; }

        public DateOnly DateOfBirth { get; set; }

        public Gender Gender { get; set; }

        public string? Address { get; set; }

        // Navigation - quan hệ 0..1: không phải Person nào cũng có tài khoản/là nhân viên/là bệnh nhân
        public User? User { get; set; }

        public Employee? Employee { get; set; }

        public Patient? Patient { get; set; }

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        /// <summary>
        /// Constructor for creating a new Person instance. Validates the input parameters.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public Person(string fullName, string? phoneNumber, string? email, DateOnly dateOfBirth, Gender gender, string address)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Full name cannot be null or empty.", nameof(fullName));
            if (!string.IsNullOrWhiteSpace(phoneNumber) &&
                !Regex.IsMatch(phoneNumber, @"^\+?[0-9]+$"))
                throw new ArgumentException("Invalid phone number");
            if (dateOfBirth > new TimeConverter().Today)
                throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));
            FullName = fullName;
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber;
            Email = string.IsNullOrWhiteSpace(email) ? null : email;
            DateOfBirth = dateOfBirth;
            Gender = gender;
            Address = address;
        }

        /// <summary>
        /// Constructor for reconstructing a Person instance from the database. Used by Entity Framework Core.
        /// </summary>
        public Person(Guid id, string fullName, string? phoneNumber, string? email, DateOnly dateOfBirth, Gender gender, string address, bool isDeleted, DateTime createdAt, DateTime? updatedAt)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            FullName = fullName;
            PhoneNumber = phoneNumber;
            Email = email;
            DateOfBirth = dateOfBirth;
            Gender = gender;
            Address = address;
            IsDeleted = isDeleted;
        }

        /// <summary>
        /// Updates the details of the person. Limited to email, gender, and address. Apply for patient only.
        /// For employee, use UpdateAdvancedDetails to update full name and phone number as well.
        /// </summary>
        public void UpdateDetails(string? email, Gender gender, string? address)
        {
            Email = email;
            Gender = gender;
            Address = address;
            MarkUpdated();
        }

        /// <summary>
        /// Fully updates the details of the person, including full name, phone number, and address. Apply for employee only.
        /// Requires correct format for phone number and email. Date of birth cannot be in the future.
        /// For patient, use UpdateDetails to update nullable email, gender, and address only.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void UpdateAdvancedDetails(string fullName, string? phoneNumber, string? email, Gender gender, DateOnly dateOfBirth, string address)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Full name cannot be null or empty.", nameof(fullName));
            if (string.IsNullOrWhiteSpace(phoneNumber) ||
                !Regex.IsMatch(phoneNumber, @"^\+?[0-9]+$"))
                throw new ArgumentException("Invalid phone number");
            if (dateOfBirth > new TimeConverter().Today)
                throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));
            if (string.IsNullOrWhiteSpace(email)) // RegEx for email validation too complex lol
                throw new ArgumentException("Email cannot be null or empty.", nameof(email));
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Address cannot be null or empty.", nameof(address));
            Email = email;
            FullName = fullName;
            PhoneNumber = phoneNumber;
            DateOfBirth = dateOfBirth;
            Gender = gender;
            Address = address;
            MarkUpdated();
        }

        /// <summary>
        /// Marks the person as deleted. This is a soft delete, meaning the record is not removed from the database but marked as deleted.
        /// </summary>
        public void Delete()
        {
            IsDeleted = true;
            MarkUpdated();
        }
    }
}
