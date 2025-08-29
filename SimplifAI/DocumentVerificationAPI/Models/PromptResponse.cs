namespace DocumentVerificationAPI.Models
{
    public class PromptResponse
    {
        public bool isAuthentic { get; set; }
        public string? reason { get; set; }
    }
}
