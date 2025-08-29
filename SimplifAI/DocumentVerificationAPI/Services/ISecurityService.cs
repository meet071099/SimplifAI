namespace DocumentVerificationAPI.Services
{
    public interface ISecurityService
    {
        /// <summary>
        /// Validates if a request is from an authorized source
        /// </summary>
        /// <param name="request">HTTP request context</param>
        /// <returns>True if authorized, false otherwise</returns>
        bool ValidateRequestSource(HttpRequest request);

        /// <summary>
        /// Sanitizes user input to prevent XSS and injection attacks
        /// </summary>
        /// <param name="input">Raw user input</param>
        /// <param name="maxLength">Maximum allowed length</param>
        /// <returns>Sanitized input</returns>
        string SanitizeInput(string input, int maxLength = 1000);

        /// <summary>
        /// Validates file upload security
        /// </summary>
        /// <param name="file">File to validate</param>
        /// <returns>Validation result</returns>
        Task<FileSecurityValidationResult> ValidateFileSecurityAsync(IFormFile file);

        /// <summary>
        /// Generates a secure token for form access
        /// </summary>
        /// <returns>Cryptographically secure token</returns>
        string GenerateSecureToken();

        /// <summary>
        /// Validates a security token
        /// </summary>
        /// <param name="token">Token to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool ValidateSecurityToken(string token);

        /// <summary>
        /// Logs security events for monitoring
        /// </summary>
        /// <param name="eventType">Type of security event</param>
        /// <param name="details">Event details</param>
        /// <param name="request">HTTP request context</param>
        void LogSecurityEvent(SecurityEventType eventType, string details, HttpRequest request);
    }

    public class FileSecurityValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? ThreatType { get; set; }
    }

    public enum SecurityEventType
    {
        InvalidFileUpload,
        DirectoryTraversalAttempt,
        SuspiciousInput,
        UnauthorizedAccess,
        FormExpiredAccess,
        RateLimitExceeded,
        InvalidToken,
        FileContentMismatch
    }
}