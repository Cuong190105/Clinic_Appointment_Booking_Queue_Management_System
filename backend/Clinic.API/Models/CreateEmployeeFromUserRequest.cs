namespace Clinic.API.Models
{
    public class CreateEmployeeFromUserRequest
    {
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public DateOnly HireDate { get; set; }
        public List<string> Roles { get; set; }
    }
}
