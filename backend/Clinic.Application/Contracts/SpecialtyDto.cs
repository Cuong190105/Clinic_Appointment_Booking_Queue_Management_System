using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    public class SpecialtyDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("establishedDate")]
        public DateOnly EstablishedDate { get; set; }
    }
}
