namespace DocumentVerificationAPI.Services
{
    public interface IFileStorageService
    {
        Task<string> StoreFileAsync(Stream file, Guid formId, string documentType, string originalFileName);
        Task<Stream?> GetFileAsync(string filePath);
        Task<bool> DeleteFileAsync(string filePath);
        Task<bool> FileExistsAsync(string filePath);
        string GetFullPath(string relativePath);
    }
}