using Microsoft.AspNetCore.Mvc;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IFormService _formService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IDocumentVerificationService _documentVerificationService;
        private readonly IAsyncDocumentVerificationService _asyncDocumentVerificationService;
        private readonly ISecurityService _securityService;
        private readonly IPerformanceMonitoringService _performanceMonitoring;
        private readonly ILogger<DocumentController> _logger;

        // Allowed file types and maximum file size (10MB)
        private readonly string[] _allowedContentTypes = { "image/jpeg", "image/jpg", "image/png", "application/pdf" };
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB

        public DocumentController(
            IFormService formService,
            IFileStorageService fileStorageService,
            IDocumentVerificationService documentVerificationService,
            IAsyncDocumentVerificationService asyncDocumentVerificationService,
            ISecurityService securityService,
            IPerformanceMonitoringService performanceMonitoring,
            ILogger<DocumentController> logger)
        {
            _formService = formService;
            _fileStorageService = fileStorageService;
            _documentVerificationService = documentVerificationService;
            _asyncDocumentVerificationService = asyncDocumentVerificationService;
            _securityService = securityService;
            _performanceMonitoring = performanceMonitoring;
            _logger = logger;
        }

        /// <summary>
        /// Uploads a document and performs verification
        /// </summary>
        /// <param name="request">Document upload request</param>
        /// <returns>Document verification result</returns>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(DocumentVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentVerificationResponse>> UploadDocument([FromForm] Models.DTOs.DocumentUploadRequest request)
        {
            try
            {
                _logger.LogInformation("Uploading document for form: {FormId}, Type: {DocumentType}", 
                    request.FormId, request.DocumentType);

                // Enhanced security validation
                var fileSecurityValidation = await _securityService.ValidateFileSecurityAsync(request.File);
                if (!fileSecurityValidation.IsValid)
                {
                    _securityService.LogSecurityEvent(SecurityEventType.InvalidFileUpload, 
                        $"File security validation failed: {string.Join(", ", fileSecurityValidation.Errors)}", 
                        Request);

                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "FileSecurityValidationFailed",
                        Message = "File failed security validation",
                        ValidationErrors = new Dictionary<string, string[]>
                        {
                            { "File", fileSecurityValidation.Errors.ToArray() }
                        },
                        TraceId = HttpContext.TraceIdentifier,
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Validate form exists and is not submitted
                var form = await _formService.GetFormByIdAsync(request.FormId);
                if (form == null)
                {
                    _logger.LogWarning("Form not found: {FormId}", request.FormId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Form not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                if (form.SubmittedAt.HasValue)
                {
                    _logger.LogWarning("Attempt to upload document to submitted form: {FormId}", request.FormId);
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = "Cannot upload documents to a submitted form",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Validate file
                var fileValidationResult = ValidateFile(request.File);
                if (!fileValidationResult.IsValid)
                {
                    _logger.LogWarning("File validation failed: {ValidationError}", fileValidationResult.ErrorMessage);
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = fileValidationResult.ErrorMessage,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Store the file
                string filePath;
                using (var fileStream = request.File.OpenReadStream())
                {
                    filePath = await _fileStorageService.StoreFileAsync(
                        fileStream, 
                        request.FormId, 
                        request.DocumentType, 
                        request.File.FileName);
                }

                // Create document record
                var document = new Document
                {
                    FormId = request.FormId,
                    DocumentType = request.DocumentType,
                    FileName = request.File.FileName,
                    FilePath = filePath,
                    FileSize = request.File.Length,
                    ContentType = request.File.ContentType,
                    VerificationStatus = "Pending"
                };

                var savedDocument = await _formService.AddDocumentAsync(document);

                // Perform document verification
                DocumentVerificationResult verificationResult;
                try
                {
                    using var fileStream = await _fileStorageService.GetFileAsync(filePath);
                    if (fileStream == null)
                    {
                        throw new InvalidOperationException("Failed to retrieve uploaded file for verification");
                    }

                    verificationResult = await _documentVerificationService.VerifyDocumentAsync(
                        fileStream, 
                        request.DocumentType, 
                        request.File.FileName,
                        request.FormId);

                    _logger.LogInformation("Document verification completed for document: {DocumentId}, Confidence: {ConfidenceScore}", 
                        savedDocument.Id, verificationResult.ConfidenceScore);
                }
                catch (Exception verificationEx)
                {
                    _logger.LogError(verificationEx, "Document verification failed for document: {DocumentId}", savedDocument.Id);
                    
                    // Create a fallback verification result
                    verificationResult = new DocumentVerificationResult
                    {
                        ConfidenceScore = 0,
                        IsBlurred = false,
                        IsCorrectType = true,
                        VerificationStatus = "Failed",
                        StatusColor = "Red",
                        Message = "Document verification service is currently unavailable. Manual review required.",
                        RequiresUserConfirmation = true,
                        VerificationDetails = $"Verification error: {verificationEx.Message}"
                    };
                }

                // Update document with verification results
                var updatedDocument = await _formService.UpdateDocumentVerificationAsync(savedDocument.Id, verificationResult);

                var response = MapToDocumentVerificationResponse(updatedDocument);

                if (verificationResult.promptResponse?.isAuthentic == false)
                {
                    response.Message = verificationResult.promptResponse.reason ?? "Document authenticity verification failed";
                }

                _logger.LogInformation("Document uploaded and verified successfully: {DocumentId}", updatedDocument.Id);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for form: {FormId}", request.FormId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while uploading the document",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets the verification status of a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>Document verification status</returns>
        [HttpGet("{documentId:guid}/status")]
        [ProducesResponseType(typeof(DocumentVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentVerificationResponse>> GetDocumentStatus(Guid documentId)
        {
            try
            {
                _logger.LogInformation("Getting document status: {DocumentId}", documentId);

                // For now, we'll use a simple approach to find the document
                // In a production system, you'd want a dedicated GetDocumentByIdAsync method
                var document = await GetDocumentByIdAsync(documentId);

                if (document == null)
                {
                    _logger.LogWarning("Document not found: {DocumentId}", documentId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Document not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var response = MapToDocumentVerificationResponse(document);

                _logger.LogInformation("Document status retrieved successfully: {DocumentId}", documentId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document status: {DocumentId}", documentId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving document status",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Downloads a document file
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>The document file</returns>
        [HttpGet("{documentId:guid}/download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadDocument(Guid documentId)
        {
            try
            {
                _logger.LogInformation("Downloading document: {DocumentId}", documentId);

                var document = await GetDocumentByIdAsync(documentId);

                if (document == null)
                {
                    _logger.LogWarning("Document not found: {DocumentId}", documentId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Document not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var fileStream = await _fileStorageService.GetFileAsync(document.FilePath);
                if (fileStream == null)
                {
                    _logger.LogWarning("Document file not found: {DocumentId}, Path: {FilePath}", documentId, document.FilePath);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Document file not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                _logger.LogInformation("Document downloaded successfully: {DocumentId}", documentId);

                return File(fileStream, document.ContentType, document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document: {DocumentId}", documentId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while downloading the document",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Confirms a document with low confidence score
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>Updated document verification status</returns>
        [HttpPost("{documentId:guid}/confirm")]
        [ProducesResponseType(typeof(DocumentVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentVerificationResponse>> ConfirmDocument(Guid documentId)
        {
            try
            {
                _logger.LogInformation("Confirming document: {DocumentId}", documentId);

                var document = await GetDocumentByIdAsync(documentId);

                if (document == null)
                {
                    _logger.LogWarning("Document not found: {DocumentId}", documentId);
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Document not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Check if document requires confirmation
                if (document.ConfidenceScore >= 85 || document.IsBlurred || !document.IsCorrectType)
                {
                    _logger.LogWarning("Document does not require confirmation: {DocumentId}", documentId);
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = "Document does not require user confirmation",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Update verification status to confirmed
                var verificationResult = new DocumentVerificationResult
                {
                    ConfidenceScore = document.ConfidenceScore ?? 0,
                    IsBlurred = document.IsBlurred,
                    IsCorrectType = document.IsCorrectType,
                    VerificationStatus = "Verified",
                    StatusColor = document.StatusColor ?? "Yellow",
                    Message = "Document confirmed by user",
                    RequiresUserConfirmation = false,
                    VerificationDetails = document.VerificationDetails
                };

                var updatedDocument = await _formService.UpdateDocumentVerificationAsync(documentId, verificationResult);

                var response = MapToDocumentVerificationResponse(updatedDocument);

                _logger.LogInformation("Document confirmed successfully: {DocumentId}", documentId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming document: {DocumentId}", documentId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while confirming the document",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets supported document types
        /// </summary>
        /// <returns>List of supported document types</returns>
        [HttpGet("supported-types")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<string>> GetSupportedDocumentTypes()
        {
            try
            {
                var supportedTypes = _documentVerificationService.GetSupportedDocumentTypes();
                return Ok(supportedTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported document types");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving supported document types",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        #region Private Helper Methods

        private async Task<Document?> GetDocumentByIdAsync(Guid documentId)
        {
            // Use the proper FormService method to get document by ID
            return await _formService.GetDocumentByIdAsync(documentId);
        }

        private (bool IsValid, string ErrorMessage) ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "No file provided");
            }

            if (file.Length > _maxFileSize)
            {
                return (false, $"File size exceeds maximum allowed size of {_maxFileSize / (1024 * 1024)}MB");
            }

            if (!_allowedContentTypes.Contains(file.ContentType.ToLower()))
            {
                return (false, $"File type '{file.ContentType}' is not supported. Allowed types: {string.Join(", ", _allowedContentTypes)}");
            }

            // Additional file name validation
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                return (false, "File name is required");
            }

            // Check for potentially dangerous file extensions
            var extension = Path.GetExtension(file.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            if (!allowedExtensions.Contains(extension))
            {
                return (false, $"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", allowedExtensions)}");
            }

            return (true, string.Empty);
        }

        private DocumentVerificationResponse MapToDocumentVerificationResponse(Document document)
        {
            return new DocumentVerificationResponse
            {
                DocumentId = document.Id,
                VerificationStatus = document.VerificationStatus,
                ConfidenceScore = document.ConfidenceScore,
                IsBlurred = document.IsBlurred,
                IsCorrectType = document.IsCorrectType,
                StatusColor = document.StatusColor ?? "Gray",
                Message = !string.IsNullOrEmpty(document.VerificationDetails) ? document.VerificationDetails : string.Empty,
                RequiresUserConfirmation = document.ConfidenceScore < 85 && !document.IsBlurred && document.IsCorrectType,
                FileName = document.FileName,
                DocumentType = document.DocumentType,
                UploadedAt = document.UploadedAt
            };
        }

        private string GetDocumentStatusMessage(Document document)
        {
            if (document.IsBlurred)
                return "Document appears blurred. Please upload a clear image.";
            
            if (!document.IsCorrectType)
                return $"Document type mismatch. Expected {document.DocumentType}.";
            
            if (document.ConfidenceScore >= 85)
                return "Document verified successfully.";
            
            if (document.ConfidenceScore >= 50)
                return "Document verification completed with medium confidence. Please confirm if this is correct.";
            
            return "Document verification completed with low confidence. Please confirm if this is correct.";
        }

        /// <summary>
        /// Starts async document verification and returns a job ID
        /// </summary>
        /// <param name="request">Document upload request for async processing</param>
        /// <returns>Async verification job information</returns>
        [HttpPost("upload-async")]
        [ProducesResponseType(typeof(AsyncVerificationResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AsyncVerificationResponse>> UploadDocumentAsync([FromForm] Models.DTOs.DocumentUploadRequest request)
        {
            using var timer = _performanceMonitoring.StartTimer("document_upload_async", new Dictionary<string, object>
            {
                ["DocumentType"] = request.DocumentType,
                ["FileSize"] = request.File?.Length ?? 0
            });

            try
            {
                _logger.LogInformation("Starting async document upload for form: {FormId}, type: {DocumentType}", 
                    request.FormId, request.DocumentType);

                // Validate request
                if (request.File == null)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = "No file provided",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Validate form exists and is not submitted
                var form = await _formService.GetFormByIdAsync(request.FormId);
                if (form == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Form not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                if (form.SubmittedAt.HasValue)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = "Cannot upload documents to a submitted form",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Validate file
                var (isValid, errorMessage) = ValidateFile(request.File);
                if (!isValid)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "BadRequest",
                        Message = errorMessage,
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Store the file first
                var filePath = await _fileStorageService.StoreFileAsync(
                    request.File.OpenReadStream(),
                    request.FormId,
                    request.DocumentType,
                    request.File.FileName);

                // Create document record
                var document = new Document
                {
                    Id = Guid.NewGuid(),
                    FormId = request.FormId,
                    DocumentType = request.DocumentType,
                    FileName = request.File.FileName,
                    FilePath = filePath,
                    FileSize = request.File.Length,
                    ContentType = request.File.ContentType,
                    UploadedAt = DateTime.UtcNow,
                    VerificationStatus = "Processing"
                };

                var savedDocument = await _formService.AddDocumentAsync(document);

                // Start async verification
                var fileStream = await _fileStorageService.GetFileAsync(filePath);
                var jobId = await _asyncDocumentVerificationService.StartVerificationAsync(
                    savedDocument.Id, fileStream, request.DocumentType, request.File.FileName);

                var response = new AsyncVerificationResponse
                {
                    JobId = jobId,
                    DocumentId = savedDocument.Id,
                    Status = "Processing",
                    Message = "Document uploaded successfully. Verification is in progress.",
                    EstimatedCompletionTime = DateTime.UtcNow.AddMinutes(2)
                };

                _logger.LogInformation("Async document verification started: JobId={JobId}, DocumentId={DocumentId}", 
                    jobId, savedDocument.Id);

                return Accepted(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting async document upload for form: {FormId}", request.FormId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while processing the document upload",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets the status of an async verification job
        /// </summary>
        /// <param name="jobId">The verification job ID</param>
        /// <returns>Job status information</returns>
        [HttpGet("verification-status/{jobId}")]
        [ProducesResponseType(typeof(DocumentVerificationJobStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentVerificationJobStatus>> GetVerificationJobStatus(string jobId)
        {
            try
            {
                _logger.LogInformation("Getting verification job status: {JobId}", jobId);

                var status = await _asyncDocumentVerificationService.GetVerificationStatusAsync(jobId);
                
                if (status.Status == "NotFound")
                {
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Verification job not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting verification job status: {JobId}", jobId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving job status",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets the result of a completed verification job
        /// </summary>
        /// <param name="jobId">The verification job ID</param>
        /// <returns>Verification result</returns>
        [HttpGet("verification-result/{jobId}")]
        [ProducesResponseType(typeof(DocumentVerificationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentVerificationResult>> GetVerificationJobResult(string jobId)
        {
            try
            {
                _logger.LogInformation("Getting verification job result: {JobId}", jobId);

                var result = await _asyncDocumentVerificationService.GetVerificationResultAsync(jobId);
                
                if (result == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Verification result not found or job not completed",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting verification job result: {JobId}", jobId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving verification result",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Gets the verification status of a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>Document verification status</returns>
        [HttpGet("{documentId}/status")]
        [ProducesResponseType(typeof(DocumentVerificationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DocumentVerificationResult>> GetVerificationStatus(Guid documentId)
        {
            try
            {
                var result = await _documentVerificationService.GetVerificationStatusAsync(documentId);
                if (result == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Document not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting verification status for document: {DocumentId}", documentId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrieving verification status",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Deletes a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{documentId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteDocument(Guid documentId)
        {
            try
            {
                var success = await _documentVerificationService.DeleteDocumentAsync(documentId);
                if (!success)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        Error = "NotFound",
                        Message = "Document not found",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while deleting the document",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Retries verification for a document
        /// </summary>
        /// <param name="documentId">The document ID</param>
        /// <returns>Updated verification result</returns>
        [HttpPost("{documentId}/retry")]
        [ProducesResponseType(typeof(DocumentVerificationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DocumentVerificationResult>> RetryVerification(Guid documentId)
        {
            try
            {
                var result = await _documentVerificationService.RetryVerificationAsync(documentId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrying verification for document: {DocumentId}", documentId);
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "An error occurred while retrying verification",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
        }

        /// <summary>
        /// Checks if a file type is valid for upload
        /// </summary>
        /// <param name="contentType">The content type to check</param>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValidFileType(string contentType)
        {
            var allowedTypes = new[]
            {
                "image/jpeg",
                "image/jpg", 
                "image/png",
                "image/gif",
                "application/pdf"
            };

            return allowedTypes.Contains(contentType?.ToLowerInvariant());
        }

        /// <summary>
        /// Gets the maximum file size in bytes
        /// </summary>
        /// <returns>Maximum file size in bytes</returns>
        public long GetMaxFileSizeInBytes()
        {
            return 10 * 1024 * 1024; // 10 MB
        }

        #endregion
    }

    public class AsyncVerificationResponse
    {
        public string JobId { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime EstimatedCompletionTime { get; set; }
    }
}