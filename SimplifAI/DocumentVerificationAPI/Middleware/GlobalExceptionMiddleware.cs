using DocumentVerificationAPI.Models.DTOs;
using System.Net;
using System.Text.Json;

namespace DocumentVerificationAPI.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var response = new ApiErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            switch (exception)
            {
                case ValidationException validationEx:
                    response.Error = "ValidationError";
                    response.Message = validationEx.Message;
                    response.ValidationErrors = validationEx.ValidationErrors;
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                case UnauthorizedAccessException:
                    response.Error = "Unauthorized";
                    response.Message = "You are not authorized to access this resource";
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    break;

                case FileNotFoundException:
                    response.Error = "NotFound";
                    response.Message = "The requested file was not found";
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;

                case DirectoryNotFoundException:
                    response.Error = "NotFound";
                    response.Message = "The requested directory was not found";
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;

                case InvalidOperationException invalidOpEx when invalidOpEx.Message.Contains("Azure"):
                    response.Error = "ServiceUnavailable";
                    response.Message = "Document verification service is temporarily unavailable. Please try again later.";
                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    _logger.LogWarning(exception, "Azure Document Intelligence service error");
                    break;

                case HttpRequestException httpEx:
                    response.Error = "ExternalServiceError";
                    response.Message = "An external service is currently unavailable. Please try again later.";
                    context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                    _logger.LogWarning(exception, "External service error");
                    break;

                case TimeoutException:
                    response.Error = "Timeout";
                    response.Message = "The request timed out. Please try again.";
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    break;

                case ArgumentNullException argNullEx:
                    response.Error = "BadRequest";
                    response.Message = $"Required parameter is missing: {argNullEx.ParamName}";
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                case ArgumentException argEx:
                    response.Error = "BadRequest";
                    response.Message = argEx.Message;
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                case NotSupportedException notSupportedEx:
                    response.Error = "NotSupported";
                    response.Message = notSupportedEx.Message;
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                case InvalidDataException dataEx:
                    response.Error = "InvalidData";
                    response.Message = dataEx.Message;
                    context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                    break;

                case IOException ioEx:
                    response.Error = "FileError";
                    response.Message = "An error occurred while processing the file";
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    _logger.LogError(exception, "File I/O error");
                    break;

                default:
                    response.Error = "InternalServerError";
                    response.Message = "An internal server error occurred";
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    _logger.LogError(exception, "Unhandled exception occurred");
                    break;
            }

            // In development, include more detailed error information
            if (_environment.IsDevelopment())
            {
                response.Message += $" (Details: {exception.Message})";
                
                // Include stack trace in development
                if (exception.StackTrace != null)
                {
                    response.ValidationErrors ??= new Dictionary<string, string[]>();
                    response.ValidationErrors["StackTrace"] = new[] { exception.StackTrace };
                }
            }

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }

    /// <summary>
    /// Custom validation exception for handling validation errors
    /// </summary>
    public class ValidationException : Exception
    {
        public Dictionary<string, string[]> ValidationErrors { get; }

        public ValidationException(string message) : base(message)
        {
            ValidationErrors = new Dictionary<string, string[]>();
        }

        public ValidationException(string message, Dictionary<string, string[]> validationErrors) : base(message)
        {
            ValidationErrors = validationErrors;
        }

        public ValidationException(string field, string error) : base($"Validation failed for {field}")
        {
            ValidationErrors = new Dictionary<string, string[]>
            {
                { field, new[] { error } }
            };
        }
    }

    /// <summary>
    /// Extension method to register the global exception middleware
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}