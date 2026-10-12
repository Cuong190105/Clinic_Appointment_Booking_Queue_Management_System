namespace Clinic.Application.Contracts
{
    public class BookingDoctorDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Qualification { get; set; }
        public int ExperienceYears { get; set; }
        //public string AvatarUrl { get; set; }
    }
}
