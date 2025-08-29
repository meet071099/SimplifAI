using DocumentVerificationAPI.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DocumentVerificationAPI.Filters
{
    /// <summary>
    /// Action filter to handle model validation errors automatically
    /// </summary>
    public class ModelValidationFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var validationErrors = new Dictionary<string, string[]>();

                foreach (var modelError in context.ModelState)
                {
                    var key = modelError.Key;
                    var errors = modelError.Value.Errors.Select(e => e.ErrorMessage).ToArray();
                    
                    if (errors.Length > 0)
                    {
                        validationErrors[key] = errors;
                    }
                }

                var errorResponse = new ApiErrorResponse
                {
                    Error = "ValidationError",
                    Message = "One or more validation errors occurred",
                    ValidationErrors = validationErrors,
                    TraceId = context.HttpContext.TraceIdentifier,
                    Timestamp = DateTime.UtcNow
                };

                context.Result = new BadRequestObjectResult(errorResponse);
            }

            base.OnActionExecuting(context);
        }
    }

    /// <summary>
    /// Custom validation filter for file uploads
    /// </summary>
    public class FileUploadValidationFilter : ActionFilterAttribute
    {
        private readonly long _maxFileSize;
        private readonly string[] _allowedContentTypes;

        public FileUploadValidationFilter(
            int maxFileSizeInMB = 10,
            string allowedContentTypes = "image/jpeg,image/jpg,image/png,application/pdf")
        {
            _maxFileSize = maxFileSizeInMB * 1024 * 1024;
            _allowedContentTypes = allowedContentTypes.Split(',').Select(t => t.Trim().ToLower()).ToArray();
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var validationErrors = new Dictionary<string, string[]>();

            // Check if request contains files
            var files = context.HttpContext.Request.Form.Files;
            
            foreach (var file in files)
            {
                var fileErrors = new List<string>();

                // Validate file size
                if (file.Length > _maxFileSize)
                {
                    fileErrors.Add($"File size ({file.Length / (1024 * 1024)}MB) exceeds maximum allowed size ({_maxFileSize / (1024 * 1024)}MB)");
                }

                if (file.Length < 1024) // Minimum 1KB
                {
                    fileErrors.Add("File is too small. Please ensure the file is not corrupted");
                }

                // Validate content type
                if (!_allowedContentTypes.Contains(file.ContentType.ToLower()))
                {
                    fileErrors.Add($"File type '{file.ContentType}' is not supported");
                }

                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                if (!allowedExtensions.Contains(extension))
                {
                    fileErrors.Add($"File extension '{extension}' is not allowed");
                }

                // Validate filename
                if (string.IsNullOrWhiteSpace(file.FileName))
                {
                    fileErrors.Add("File must have a valid name");
                }

                // Security checks
                var dangerousExtensions = new[] { ".exe", ".bat", ".cmd", ".scr", ".com", ".pif", ".vbs", ".js" };
                if (dangerousExtensions.Any(ext => file.FileName.ToLower().EndsWith(ext)))
                {
                    fileErrors.Add("File type is not allowed for security reasons");
                }

                if (fileErrors.Any())
                {
                    validationErrors[file.Name] = fileErrors.ToArray();
                }
            }

            if (validationErrors.Any())
            {
                var errorResponse = new ApiErrorResponse
                {
                    Error = "FileValidationError",
                    Message = "One or more file validation errors occurred",
                    ValidationErrors = validationErrors,
                    TraceId = context.HttpContext.TraceIdentifier,
                    Timestamp = DateTime.UtcNow
                };

                context.Result = new BadRequestObjectResult(errorResponse);
            }

            base.OnActionExecuting(context);
        }
    }

    /// <summary>
    /// Rate limiting filter to prevent abuse
    /// </summary>
    public class RateLimitFilter : ActionFilterAttribute
    {
        private static readonly Dictionary<string, List<DateTime>> _requestHistory = new();
        private static readonly object _lock = new object();
        
        private readonly int _maxRequests;
        private readonly TimeSpan _timeWindow;

        public RateLimitFilter(int maxRequests = 100, int timeWindowInMinutes = 1)
        {
            _maxRequests = maxRequests;
            _timeWindow = TimeSpan.FromMinutes(timeWindowInMinutes);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var clientId = GetClientIdentifier(context.HttpContext);
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (!_requestHistory.ContainsKey(clientId))
                {
                    _requestHistory[clientId] = new List<DateTime>();
                }

                var requests = _requestHistory[clientId];
                
                // Remove old requests outside the time window
                requests.RemoveAll(r => now - r > _timeWindow);

                // Check if limit exceeded
                if (requests.Count >= _maxRequests)
                {
                    var errorResponse = new ApiErrorResponse
                    {
                        Error = "RateLimitExceeded",
                        Message = $"Rate limit exceeded. Maximum {_maxRequests} requests per {_timeWindow.TotalMinutes} minutes",
                        TraceId = context.HttpContext.TraceIdentifier,
                        Timestamp = DateTime.UtcNow
                    };

                    context.Result = new ObjectResult(errorResponse)
                    {
                        StatusCode = 429 // Too Many Requests
                    };
                    return;
                }

                // Add current request
                requests.Add(now);
            }

            base.OnActionExecuting(context);
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Use IP address as client identifier
            // In production, you might want to use a more sophisticated approach
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }

    /// <summary>
    /// Request size validation filter
    /// </summary>
    public class RequestSizeValidationFilter : ActionFilterAttribute
    {
        private readonly long _maxRequestSize;

        public RequestSizeValidationFilter(int maxRequestSizeInMB = 50)
        {
            _maxRequestSize = maxRequestSizeInMB * 1024 * 1024;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            
            if (request.ContentLength.HasValue && request.ContentLength.Value > _maxRequestSize)
            {
                var errorResponse = new ApiErrorResponse
                {
                    Error = "RequestTooLarge",
                    Message = $"Request size ({request.ContentLength.Value / (1024 * 1024)}MB) exceeds maximum allowed size ({_maxRequestSize / (1024 * 1024)}MB)",
                    TraceId = context.HttpContext.TraceIdentifier,
                    Timestamp = DateTime.UtcNow
                };

                context.Result = new ObjectResult(errorResponse)
                {
                    StatusCode = 413 // Payload Too Large
                };
            }

            base.OnActionExecuting(context);
        }
    }
}