using Clinic.Domain.Enums;

namespace Clinic.Application.Contracts
{
    public class DoctorSummaryDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public Gender Gender { get; set; }
        public string LicenseNumber { get; set; }
        public string Qualification { get; set; }
        public string CurrentSpecialty { get; set; }
        public int ExperienceYears { get; set; }
        public DoctorStatus Status { get; set; }
    }
}
