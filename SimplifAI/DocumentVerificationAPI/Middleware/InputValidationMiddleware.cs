using DocumentVerificationAPI.Services;
using System.Text;
using System.Text.Json;

namespace DocumentVerificationAPI.Middleware
{
    public class InputValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<InputValidationMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;

        public InputValidationMiddleware(RequestDelegate next, ILogger<InputValidationMiddleware> logger, IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            using var scope = _serviceProvider.CreateScope();
            var securityService = scope.ServiceProvider.GetRequiredService<ISecurityService>();

            // Validate request source
            if (!securityService.ValidateRequestSource(context.Request))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid request");
                return;
            }

            // Validate and sanitize request body for POST/PUT requests
            if (context.Request.Method == "POST" || context.Request.Method == "PUT")
            {
                await ValidateAndSanitizeRequestBodyAsync(context, securityService);
            }

            // Validate query parameters
            ValidateQueryParameters(context, securityService);

            await _next(context);
        }

        private async Task ValidateAndSanitizeRequestBodyAsync(HttpContext context, ISecurityService securityService)
        {
            if (context.Request.ContentType?.Contains("application/json") == true)
            {
                context.Request.EnableBuffering();
                
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                // Check for suspicious patterns in JSON
                if (ContainsSuspiciousPatterns(body))
                {
                    securityService.LogSecurityEvent(SecurityEventType.SuspiciousInput, 
                        "Suspicious patterns detected in request body", context.Request);
                    
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Invalid request content");
                    return;
                }

                // Validate JSON structure
                try
                {
                    JsonDocument.Parse(body);
                }
                catch (JsonException)
                {
                    securityService.LogSecurityEvent(SecurityEventType.SuspiciousInput, 
                        "Invalid JSON in request body", context.Request);
                    
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Invalid JSON format");
                    return;
                }
            }
        }

        private void ValidateQueryParameters(HttpContext context, ISecurityService securityService)
        {
            foreach (var param in context.Request.Query)
            {
                var key = param.Key;
                var values = param.Value;

                foreach (var value in values)
                {
                    if (string.IsNullOrEmpty(value))
                        continue;

                    // Check for suspicious patterns
                    if (ContainsSuspiciousPatterns(value))
                    {
                        securityService.LogSecurityEvent(SecurityEventType.SuspiciousInput, 
                            $"Suspicious pattern in query parameter '{key}': {value}", context.Request);
                        
                        context.Response.StatusCode = 400;
                        context.Response.WriteAsync("Invalid query parameters").Wait();
                        return;
                    }

                    // Check for excessively long parameters
                    if (value.Length > 1000)
                    {
                        securityService.LogSecurityEvent(SecurityEventType.SuspiciousInput, 
                            $"Excessively long query parameter '{key}'", context.Request);
                        
                        context.Response.StatusCode = 400;
                        context.Response.WriteAsync("Query parameter too long").Wait();
                        return;
                    }
                }
            }
        }

        private bool ContainsSuspiciousPatterns(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var suspiciousPatterns = new[]
            {
                // SQL Injection patterns
                @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|UNION)\b)",
                @"(\b(OR|AND)\s+\d+\s*=\s*\d+)",
                @"('|\"")\s*(OR|AND)\s*('|\"")?\s*\d+\s*=\s*\d+",
                @"(--|#|/\*|\*/)",
                
                // XSS patterns
                @"<\s*script[^>]*>",
                @"javascript\s*:",
                @"vbscript\s*:",
                @"on\w+\s*=",
                @"<\s*iframe[^>]*>",
                @"<\s*object[^>]*>",
                @"<\s*embed[^>]*>",
                
                // Directory traversal
                @"\.\./",
                @"\.\.\\",
                
                // Command injection
                @"(\b(cmd|powershell|bash|sh)\b)",
                @"(\||&|;|\$\(|\`)",
                
                // File inclusion
                @"(file://|ftp://|data:)",
                
                // Null bytes and control characters
                @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]"
            };

            return suspiciousPatterns.Any(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(input, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        }
    }

    public static class InputValidationMiddlewareExtensions
    {
        public static IApplicationBuilder UseInputValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<InputValidationMiddleware>();
        }
    }
}