using System;
using System.Text.Json.Serialization;

namespace Clinic.API.Models
{
    public class ReceptionistWalkInResult
    {
        [JsonPropertyName("appointmentId")]
        public Guid AppointmentId { get; set; }

        [JsonPropertyName("patientId")]
        public Guid PatientId { get; set; }

        [JsonPropertyName("patientName")]
        public string PatientName { get; set; } = string.Empty;

        [JsonPropertyName("doctorId")]
        public Guid DoctorId { get; set; }

        [JsonPropertyName("doctorName")]
        public string DoctorName { get; set; } = string.Empty;

        [JsonPropertyName("specialtyName")]
        public string SpecialtyName { get; set; } = string.Empty;

        [JsonPropertyName("queueTicketId")]
        public Guid? QueueTicketId { get; set; }

        [JsonPropertyName("queueNumber")]
        public int? QueueNumber { get; set; }

        [JsonPropertyName("isUrgent")]
        public bool IsUrgent { get; set; }

        [JsonPropertyName("checkInTime")]
        public DateTime CheckInTime { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("queueTicketPending")]
        public bool QueueTicketPending { get; set; }
    }
}
