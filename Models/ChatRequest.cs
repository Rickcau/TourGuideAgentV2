namespace TourGuideAgentV2.Models
{
    public class ChatRequest
    {
        public required string ClientId { get; set; }
        public required string VehicleId { get; set; }
        public required string Prompt { get; set; }
    }
}