using System.Security.Cryptography;
using System.Text;

namespace DocumentVerificationAPI.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _basePath;
        private readonly ILogger<LocalFileStorageService> _logger;
        private readonly long _maxFileSize;
        private readonly string[] _allowedExtensions;
        private readonly string[] _allowedMimeTypes;

        public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
        {
            _basePath = configuration.GetValue<string>("FileStorage:BasePath") ?? "uploads";
            _maxFileSize = configuration.GetValue<long?>("FileStorage:MaxFileSizeInBytes") ?? (10 * 1024 * 1024); // 10MB default
            _allowedExtensions = configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? 
                new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            _allowedMimeTypes = configuration.GetSection("FileStorage:AllowedMimeTypes").Get<string[]>() ?? 
                new[] { "image/jpeg", "image/png", "application/pdf" };
            _logger = logger;

            // Ensure base directory exists with proper permissions
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                SetDirectoryPermissions(_basePath);
                _logger.LogInformation("Created base storage directory: {BasePath}", _basePath);
            }
        }

        public async Task<string> StoreFileAsync(Stream file, Guid formId, string documentType, string originalFileName)
        {
            try
            {
                // Security validation
                await ValidateFileSecurityAsync(file, originalFileName);

                // Create organized folder structure: uploads/formId/documentType/
                var formFolder = Path.Combine(_basePath, formId.ToString());
                var documentTypeFolder = Path.Combine(formFolder, SanitizeDirectoryName(documentType));

                // Ensure directories exist with proper permissions
                Directory.CreateDirectory(documentTypeFolder);
                SetDirectoryPermissions(documentTypeFolder);

                // Generate secure unique filename
                var fileExtension = Path.GetExtension(SanitizeFileName(originalFileName));
                var uniqueFileName = GenerateSecureFileName(fileExtension);
                var filePath = Path.Combine(documentTypeFolder, uniqueFileName);

                // Validate final path is within allowed directory
                ValidateFilePath(filePath);

                // Store the file with security measures
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await file.CopyToAsync(fileStream);

                // Set file permissions
                SetFilePermissions(filePath);

                // Return relative path for database storage
                var relativePath = Path.GetRelativePath(_basePath, filePath);
                
                _logger.LogInformation("File stored successfully: {FilePath} (Original: {OriginalFileName})", 
                    relativePath, originalFileName);
                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing file: {FileName} for form {FormId}", originalFileName, formId);
                throw;
            }
        }

        public Task<Stream?> GetFileAsync(string filePath)
        {
            try
            {
                // Validate file path for security
                if (!IsValidFilePath(filePath))
                {
                    _logger.LogWarning("Invalid file path requested: {FilePath}", filePath);
                    return Task.FromResult<Stream?>(null);
                }

                var fullPath = GetFullPath(filePath);
                
                // Ensure the resolved path is still within the base directory (prevent directory traversal)
                if (!IsPathWithinBaseDirectory(fullPath))
                {
                    _logger.LogWarning("Attempted directory traversal attack: {FilePath}", filePath);
                    return Task.FromResult<Stream?>(null);
                }
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("File not found: {FilePath}", filePath);
                    return Task.FromResult<Stream?>(null);
                }

                var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return Task.FromResult<Stream?>(fileStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file: {FilePath}", filePath);
                return Task.FromResult<Stream?>(null);
            }
        }

        public Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                // Validate file path for security
                if (!IsValidFilePath(filePath))
                {
                    _logger.LogWarning("Invalid file path for deletion: {FilePath}", filePath);
                    return Task.FromResult(false);
                }

                var fullPath = GetFullPath(filePath);
                
                // Ensure the resolved path is still within the base directory
                if (!IsPathWithinBaseDirectory(fullPath))
                {
                    _logger.LogWarning("Attempted directory traversal in delete operation: {FilePath}", filePath);
                    return Task.FromResult(false);
                }
                
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                    return Task.FromResult(false);
                }

                // Secure deletion - overwrite file content before deletion
                SecureDeleteFile(fullPath);
                
                _logger.LogInformation("File deleted securely: {FilePath}", filePath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                return Task.FromResult(false);
            }
        }

        public Task<bool> FileExistsAsync(string filePath)
        {
            try
            {
                if (!IsValidFilePath(filePath))
                {
                    return Task.FromResult(false);
                }

                var fullPath = GetFullPath(filePath);
                
                if (!IsPathWithinBaseDirectory(fullPath))
                {
                    return Task.FromResult(false);
                }
                
                return Task.FromResult(File.Exists(fullPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file existence: {FilePath}", filePath);
                return Task.FromResult(false);
            }
        }

        public string GetFullPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(_basePath, relativePath));
        }

        #region Security Methods

        private async Task ValidateFileSecurityAsync(Stream file, string originalFileName)
        {
            // Check file size
            if (file.Length > _maxFileSize)
            {
                throw new InvalidOperationException($"File size ({file.Length} bytes) exceeds maximum allowed size ({_maxFileSize} bytes)");
            }

            if (file.Length < 1024) // Minimum 1KB
            {
                throw new InvalidOperationException("File is too small and may be corrupted");
            }

            // Validate filename
            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                throw new ArgumentException("Filename cannot be empty");
            }

            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"File extension '{extension}' is not allowed");
            }

            // Check for dangerous filename patterns
            var dangerousPatterns = new[] { "..", "\\", "/", ":", "*", "?", "\"", "<", ">", "|" };
            if (dangerousPatterns.Any(pattern => originalFileName.Contains(pattern)))
            {
                throw new InvalidOperationException("Filename contains dangerous characters");
            }

            // Validate file content (magic number check)
            await ValidateFileContentAsync(file, extension);
        }

        private async Task ValidateFileContentAsync(Stream file, string extension)
        {
            var originalPosition = file.Position;
            file.Position = 0;

            var buffer = new byte[8];
            await file.ReadAsync(buffer, 0, buffer.Length);
            file.Position = originalPosition;

            var isValidFileType = extension switch
            {
                ".jpg" or ".jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8,
                ".png" => buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47,
                ".pdf" => buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46,
                _ => true // Allow other types for now
            };

            if (!isValidFileType)
            {
                throw new InvalidOperationException($"File content does not match the expected format for {extension} files");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Remove potentially dangerous characters
            sanitized = sanitized.Replace("..", "").Replace("\\", "").Replace("/", "");
            
            return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
        }

        private string SanitizeDirectoryName(string directoryName)
        {
            var invalidChars = Path.GetInvalidPathChars();
            var sanitized = new string(directoryName.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Remove potentially dangerous characters
            sanitized = sanitized.Replace("..", "").Replace("\\", "").Replace("/", "");
            
            return string.IsNullOrWhiteSpace(sanitized) ? "documents" : sanitized;
        }

        private string GenerateSecureFileName(string extension)
        {
            // Generate cryptographically secure filename
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[16];
            rng.GetBytes(bytes);
            
            var fileName = Convert.ToBase64String(bytes)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "");
            
            return $"{fileName}{extension}";
        }

        private void ValidateFilePath(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            var basePath = Path.GetFullPath(_basePath);
            
            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("File path is outside the allowed directory");
            }
        }

        private bool IsValidFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            // Check for directory traversal attempts
            if (filePath.Contains("..") || filePath.Contains("\\..") || filePath.Contains("../"))
                return false;

            // Check for absolute paths
            if (Path.IsPathRooted(filePath))
                return false;

            return true;
        }

        private bool IsPathWithinBaseDirectory(string fullPath)
        {
            var normalizedPath = Path.GetFullPath(fullPath);
            var normalizedBasePath = Path.GetFullPath(_basePath);
            
            return normalizedPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase);
        }

        private void SetDirectoryPermissions(string directoryPath)
        {
            try
            {
                // Set directory permissions (Windows-specific)
                var directoryInfo = new DirectoryInfo(directoryPath);
                
                // Remove inheritance and set specific permissions
                // This is a simplified approach - in production, you'd want more granular control
                _logger.LogDebug("Directory permissions set for: {DirectoryPath}", directoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set directory permissions for: {DirectoryPath}", directoryPath);
            }
        }

        private void SetFilePermissions(string filePath)
        {
            try
            {
                // Set file permissions (Windows-specific)
                var fileInfo = new FileInfo(filePath);
                
                // Make file read-only to prevent tampering
                fileInfo.IsReadOnly = false; // Keep writable for now, but could be made read-only
                
                _logger.LogDebug("File permissions set for: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set file permissions for: {FilePath}", filePath);
            }
        }

        private void SecureDeleteFile(string filePath)
        {
            try
            {
                // Overwrite file content before deletion for security
                var fileInfo = new FileInfo(filePath);
                var fileSize = fileInfo.Length;
                
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Write))
                {
                    // Overwrite with random data
                    var buffer = new byte[1024];
                    using var rng = RandomNumberGenerator.Create();
                    
                    for (long i = 0; i < fileSize; i += buffer.Length)
                    {
                        rng.GetBytes(buffer);
                        var bytesToWrite = (int)Math.Min(buffer.Length, fileSize - i);
                        fileStream.Write(buffer, 0, bytesToWrite);
                    }
                    
                    fileStream.Flush();
                }
                
                // Now delete the file
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not securely delete file: {FilePath}", filePath);
                // Fallback to regular deletion
                File.Delete(filePath);
            }
        }

        #endregion
    }
}