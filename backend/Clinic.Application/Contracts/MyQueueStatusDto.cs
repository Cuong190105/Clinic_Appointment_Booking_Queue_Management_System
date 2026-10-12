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

        /// <summary>Vị trí của bệnh nhân trong hàng đợi (1 = sắp tới lượt).</summary>
        [JsonPropertyName("positionInQueue")]
        public int PositionInQueue { get; set; }

        /// <summary>Thời gian chờ ước tính (phút), tính đơn giản theo số người đang chờ phía trước.</summary>
        [JsonPropertyName("estimatedWaitMinutes")]
        public int EstimatedWaitMinutes { get; set; }
    }
}