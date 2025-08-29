using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI.Services
{
    public interface IAzureAIFoundryService
    {
        Task<PromptResponse> VerifyDocumentAuthenticityAsync(string formFirstName, string formLastName, string extractedText);
        Task<PromptResponse> VerifyDocumentAuthenticityAsync(DocumentAuthenticityRequest request);
        Task<bool> IsServiceAvailableAsync();
    }
}