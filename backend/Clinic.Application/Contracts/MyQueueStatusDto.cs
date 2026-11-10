using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    public class MyQueueStatusDto
    {
        [JsonPropertyName("queueTicketId")]
        public Guid QueueTicketId { get; set; }

        [JsonPropertyName("queueNumber")]
        public int QueueNumber { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("doctorName")]
        public string DoctorName { get; set; } = string.Empty;

        /// <summary>V? trí c?a b?nh nhân trong hàng d?i (1 = s?p t?i lu?t).</summary>
        [JsonPropertyName("positionInQueue")]
        public int PositionInQueue { get; set; }

        /// <summary>Th?i gian ch? u?c tính (phút), tính don gi?n theo s? ngu?i dang ch? phía tru?c.</summary>
        [JsonPropertyName("estimatedWaitMinutes")]
        public int EstimatedWaitMinutes { get; set; }
    }
}