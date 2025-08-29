using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI.Services
{
    public interface IDocumentVerificationService
    {
        Task<DocumentVerificationResult> VerifyDocumentAsync(Stream documentStream, string expectedDocumentType, string fileName);
        Task<DocumentVerificationResult> VerifyDocumentAsync(Stream documentStream, string expectedDocumentType, string fileName, Guid? formId);
        Task<bool> IsServiceAvailableAsync();
        Task<DocumentVerificationResult> GetVerificationStatusAsync(Guid documentId);
        Task<bool> ConfirmDocumentAsync(Guid documentId);
        Task<bool> DeleteDocumentAsync(Guid documentId);
        Task<DocumentVerificationResult> RetryVerificationAsync(Guid documentId);
        string[] GetSupportedDocumentTypes();
    }
}