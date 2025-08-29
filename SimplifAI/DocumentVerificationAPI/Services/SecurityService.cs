using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DocumentVerificationAPI.Services
{
    public class SecurityService : ISecurityService
    {
        private readonly ILogger<SecurityService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string[] _allowedFileExtensions;
        private readonly string[] _allowedMimeTypes;
        private readonly long _maxFileSize;
        private readonly HashSet<string> _dangerousExtensions;
        private readonly HashSet<string> _suspiciousPatterns;

        public SecurityService(ILogger<SecurityService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            _allowedFileExtensions = configuration.GetSection("Security:AllowedFileExtensions").Get<string[]>() ?? 
                new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            _allowedMimeTypes = configuration.GetSection("Security:AllowedMimeTypes").Get<string[]>() ?? 
                new[] { "image/jpeg", "image/png", "application/pdf" };
            _maxFileSize = configuration.GetValue<long?>("Security:MaxFileSizeInBytes") ?? (10 * 1024 * 1024);

            _dangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".exe", ".bat", ".cmd", ".scr", ".com", ".pif", ".vbs", ".js", ".jar",
                ".app", ".deb", ".pkg", ".dmg", ".msi", ".run", ".sh", ".ps1", ".php",
                ".asp", ".aspx", ".jsp", ".py", ".rb", ".pl", ".cgi"
            };

            _suspiciousPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "<script", "javascript:", "vbscript:", "onload=", "onerror=", "onclick=",
                "eval(", "exec(", "system(", "shell_exec", "passthru", "file_get_contents",
                "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
                "UNION", "OR 1=1", "' OR '1'='1", "admin'--", "'; DROP TABLE"
            };
        }

        public bool ValidateRequestSource(HttpRequest request)
        {
            try
            {
                // Check for required headers
                if (!request.Headers.ContainsKey("User-Agent"))
                {
                    LogSecurityEvent(SecurityEventType.SuspiciousInput, "Missing User-Agent header", request);
                    return false;
                }

                // Check for suspicious user agents
                var userAgent = request.Headers["User-Agent"].ToString();
                var suspiciousUserAgents = new[] { "sqlmap", "nikto", "nmap", "masscan", "curl", "wget" };
                
                if (suspiciousUserAgents.Any(ua => userAgent.Contains(ua, StringComparison.OrdinalIgnoreCase)))
                {
                    LogSecurityEvent(SecurityEventType.SuspiciousInput, $"Suspicious User-Agent: {userAgent}", request);
                    return false;
                }

                // Validate Content-Type for POST requests
                if (request.Method == "POST" && request.HasFormContentType)
                {
                    var contentType = request.ContentType?.ToLowerInvariant();
                    if (contentType != null && !contentType.StartsWith("multipart/form-data") && 
                        !contentType.StartsWith("application/x-www-form-urlencoded") &&
                        !contentType.StartsWith("application/json"))
                    {
                        LogSecurityEvent(SecurityEventType.SuspiciousInput, $"Suspicious Content-Type: {contentType}", request);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating request source");
                return false;
            }
        }

        public string SanitizeInput(string input, int maxLength = 1000)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var sanitized = input.Trim();

            // Remove HTML tags
            sanitized = Regex.Replace(sanitized, @"<[^>]*>", "", RegexOptions.IgnoreCase);

            // Remove script content
            sanitized = Regex.Replace(sanitized, @"<script[^>]*>.*?</script>", "", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Remove dangerous JavaScript events
            sanitized = Regex.Replace(sanitized, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

            // Remove SQL injection patterns
            foreach (var pattern in _suspiciousPatterns)
            {
                sanitized = sanitized.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
            }

            // Remove null bytes and control characters
            sanitized = Regex.Replace(sanitized, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            // Encode remaining special characters
            sanitized = System.Net.WebUtility.HtmlEncode(sanitized);

            // Truncate to max length
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            return sanitized;
        }

        public async Task<FileSecurityValidationResult> ValidateFileSecurityAsync(IFormFile file)
        {
            var result = new FileSecurityValidationResult { IsValid = true };

            try
            {
                // Basic file validation
                if (file == null || file.Length == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("No file provided or file is empty");
                    return result;
                }

                // File size validation
                if (file.Length > _maxFileSize)
                {
                    result.IsValid = false;
                    result.Errors.Add($"File size ({file.Length} bytes) exceeds maximum allowed size ({_maxFileSize} bytes)");
                }

                if (file.Length < 1024) // Minimum 1KB
                {
                    result.IsValid = false;
                    result.Errors.Add("File is too small and may be corrupted");
                }

                // Filename validation
                if (string.IsNullOrWhiteSpace(file.FileName))
                {
                    result.IsValid = false;
                    result.Errors.Add("File must have a valid name");
                    return result;
                }

                // Check for dangerous filename patterns
                if (ContainsDangerousPatterns(file.FileName))
                {
                    result.IsValid = false;
                    result.Errors.Add("Filename contains dangerous patterns");
                    result.ThreatType = "Dangerous filename";
                }

                // Extension validation
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (_dangerousExtensions.Contains(extension))
                {
                    result.IsValid = false;
                    result.Errors.Add($"File extension '{extension}' is not allowed for security reasons");
                    result.ThreatType = "Dangerous file extension";
                }

                if (!_allowedFileExtensions.Contains(extension))
                {
                    result.IsValid = false;
                    result.Errors.Add($"File extension '{extension}' is not allowed");
                }

                // MIME type validation
                if (!_allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
                {
                    result.IsValid = false;
                    result.Errors.Add($"File type '{file.ContentType}' is not supported");
                }

                // File content validation (magic number check)
                await ValidateFileContentAsync(file, extension, result);

                // Check for embedded threats
                await ScanForEmbeddedThreatsAsync(file, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file security validation");
                result.IsValid = false;
                result.Errors.Add("File validation failed due to internal error");
                return result;
            }
        }

        public string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var tokenBytes = new byte[32]; // 256-bit token
            rng.GetBytes(tokenBytes);
            
            return Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        public bool ValidateSecurityToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            // Check token length (should be around 43 characters for 32-byte base64url)
            if (token.Length < 32 || token.Length > 50)
                return false;

            // Check for valid base64url characters
            if (!Regex.IsMatch(token, @"^[A-Za-z0-9_-]+$"))
                return false;

            return true;
        }

        public void LogSecurityEvent(SecurityEventType eventType, string details, HttpRequest request)
        {
            var clientIp = GetClientIpAddress(request);
            var userAgent = request.Headers["User-Agent"].ToString();
            var requestPath = request.Path.ToString();

            _logger.LogWarning("Security Event: {EventType} | IP: {ClientIp} | Path: {RequestPath} | Details: {Details} | UserAgent: {UserAgent}",
                eventType, clientIp, requestPath, details, userAgent);

            // In a production environment, you might want to:
            // 1. Send alerts for critical security events
            // 2. Store events in a security log database
            // 3. Integrate with SIEM systems
            // 4. Implement automatic blocking for repeated violations
        }

        #region Private Methods

        private bool ContainsDangerousPatterns(string filename)
        {
            // Check for directory traversal
            if (filename.Contains("..") || filename.Contains("\\") || filename.Contains("/"))
                return true;

            // Check for null bytes
            if (filename.Contains('\0'))
                return true;

            // Check for Windows reserved names
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename).ToUpperInvariant();
            if (reservedNames.Contains(nameWithoutExtension))
                return true;

            // Check for suspicious patterns
            return _suspiciousPatterns.Any(pattern => filename.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private async Task ValidateFileContentAsync(IFormFile file, string extension, FileSecurityValidationResult result)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var buffer = new byte[8];
                await stream.ReadAsync(buffer, 0, buffer.Length);

                var isValidFileType = extension switch
                {
                    ".jpg" or ".jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8,
                    ".png" => buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47,
                    ".pdf" => buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46,
                    _ => true // Allow other types for now
                };

                if (!isValidFileType)
                {
                    result.IsValid = false;
                    result.Errors.Add($"File content does not match the expected format for {extension} files");
                    result.ThreatType = "File content mismatch";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not validate file content for file: {FileName}", file.FileName);
                result.Warnings.Add("Could not verify file content integrity");
            }
        }

        private async Task ScanForEmbeddedThreatsAsync(IFormFile file, FileSecurityValidationResult result)
        {
            try
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                
                // Read first 1KB to scan for embedded scripts or malicious content
                var buffer = new char[1024];
                var bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                var content = new string(buffer, 0, bytesRead).ToLowerInvariant();

                // Check for embedded JavaScript
                if (content.Contains("<script") || content.Contains("javascript:") || content.Contains("vbscript:"))
                {
                    result.Warnings.Add("File may contain embedded scripts");
                    result.ThreatType = "Embedded scripts detected";
                }

                // Check for suspicious URLs
                if (content.Contains("http://") || content.Contains("https://"))
                {
                    result.Warnings.Add("File contains URLs - verify they are legitimate");
                }

                // Check for executable signatures in non-executable files
                if (file.ContentType.StartsWith("image/") && (content.Contains("mz") || content.Contains("pe")))
                {
                    result.IsValid = false;
                    result.Errors.Add("Image file contains executable content");
                    result.ThreatType = "Executable content in image";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not scan file for embedded threats: {FileName}", file.FileName);
                // Don't fail validation if we can't scan - just log it
            }
        }

        private string GetClientIpAddress(HttpRequest request)
        {
            // Check for forwarded IP first (in case of proxy/load balancer)
            var forwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        #endregion
    }
}