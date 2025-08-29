using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace DocumentVerificationAPI.Services
{
    public interface IAsyncDocumentVerificationService
    {
        /// <summary>
        /// Starts async document verification and returns a job ID
        /// </summary>
        Task<string> StartVerificationAsync(Guid documentId, Stream documentStream, string expectedDocumentType, string fileName);
        
        /// <summary>
        /// Gets the status of a verification job
        /// </summary>
        Task<DocumentVerificationJobStatus> GetVerificationStatusAsync(string jobId);
        
        /// <summary>
        /// Gets the result of a completed verification job
        /// </summary>
        Task<DocumentVerificationResult?> GetVerificationResultAsync(string jobId);
    }

    public class DocumentVerificationJobStatus
    {
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Pending, Processing, Completed, Failed
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public int ProgressPercentage { get; set; }
    }

    public class AsyncDocumentVerificationService : IAsyncDocumentVerificationService
    {
        private readonly IDocumentVerificationService _documentVerificationService;
        private readonly ILogger<AsyncDocumentVerificationService> _logger;
        private readonly IPerformanceMonitoringService _performanceMonitoring;
        private readonly ApplicationDbContext _context;
        private readonly ConcurrentDictionary<string, DocumentVerificationJobStatus> _jobStatuses;
        private readonly ConcurrentDictionary<string, DocumentVerificationResult> _jobResults;

        public AsyncDocumentVerificationService(
            IDocumentVerificationService documentVerificationService,
            ILogger<AsyncDocumentVerificationService> logger,
            IPerformanceMonitoringService performanceMonitoring,
            ApplicationDbContext context)
        {
            _documentVerificationService = documentVerificationService;
            _logger = logger;
            _performanceMonitoring = performanceMonitoring;
            _context = context;
            _jobStatuses = new ConcurrentDictionary<string, DocumentVerificationJobStatus>();
            _jobResults = new ConcurrentDictionary<string, DocumentVerificationResult>();
        }

        public async Task<string> StartVerificationAsync(Guid documentId, Stream documentStream, string expectedDocumentType, string fileName)
        {
            var jobId = Guid.NewGuid().ToString();
            var jobStatus = new DocumentVerificationJobStatus
            {
                JobId = jobId,
                Status = "Pending",
                StartedAt = DateTime.UtcNow,
                ProgressPercentage = 0
            };

            _jobStatuses[jobId] = jobStatus;

            _logger.LogInformation("Starting async document verification job {JobId} for document {DocumentId}", jobId, documentId);

            // Start the verification process in the background
            _ = Task.Run(async () => await ProcessVerificationAsync(jobId, documentId, documentStream, expectedDocumentType, fileName));

            return jobId;
        }

        public Task<DocumentVerificationJobStatus> GetVerificationStatusAsync(string jobId)
        {
            _jobStatuses.TryGetValue(jobId, out var status);
            return Task.FromResult(status ?? new DocumentVerificationJobStatus 
            { 
                JobId = jobId, 
                Status = "NotFound",
                ErrorMessage = "Job not found"
            });
        }

        public Task<DocumentVerificationResult?> GetVerificationResultAsync(string jobId)
        {
            _jobResults.TryGetValue(jobId, out var result);
            return Task.FromResult(result);
        }

        private async Task ProcessVerificationAsync(string jobId, Guid documentId, Stream documentStream, string expectedDocumentType, string fileName)
        {
            using var timer = _performanceMonitoring.StartTimer("async_document_verification", new Dictionary<string, object>
            {
                ["JobId"] = jobId,
                ["DocumentId"] = documentId,
                ["DocumentType"] = expectedDocumentType
            });

            try
            {
                // Update status to processing
                if (_jobStatuses.TryGetValue(jobId, out var status))
                {
                    status.Status = "Processing";
                    status.ProgressPercentage = 10;
                }

                _logger.LogInformation("Processing document verification job {JobId} for document {DocumentId}", jobId, documentId);

                // Copy stream to memory to avoid disposal issues
                using var memoryStream = new MemoryStream();
                documentStream.Position = 0;
                await documentStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Update progress
                if (status != null)
                {
                    status.ProgressPercentage = 30;
                }

                // Get the document to retrieve formId
                var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
                var formId = document?.FormId;

                // Perform the actual verification
                var result = await _documentVerificationService.VerifyDocumentAsync(memoryStream, expectedDocumentType, fileName, formId);

                // Update progress
                if (status != null)
                {
                    status.ProgressPercentage = 80;
                }

                // Update the document in the database
                await UpdateDocumentInDatabaseAsync(documentId, result);

                // Store the result
                _jobResults[jobId] = result;

                // Update status to completed
                if (status != null)
                {
                    status.Status = "Completed";
                    status.CompletedAt = DateTime.UtcNow;
                    status.ProgressPercentage = 100;
                }

                _logger.LogInformation("Completed document verification job {JobId} for document {DocumentId} with status {Status}", 
                    jobId, documentId, result.VerificationStatus);

                _performanceMonitoring.RecordCounter("async_document_verification_completed", 1, new Dictionary<string, object>
                {
                    ["Status"] = result.VerificationStatus,
                    ["ConfidenceScore"] = result.ConfidenceScore
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document verification job {JobId} for document {DocumentId}", jobId, documentId);

                // Update status to failed
                if (_jobStatuses.TryGetValue(jobId, out var status))
                {
                    status.Status = "Failed";
                    status.CompletedAt = DateTime.UtcNow;
                    status.ErrorMessage = ex.Message;
                    status.ProgressPercentage = 0;
                }

                _performanceMonitoring.RecordCounter("async_document_verification_failed", 1, new Dictionary<string, object>
                {
                    ["ErrorType"] = ex.GetType().Name
                });
            }
            finally
            {
                // Clean up old job statuses (keep for 1 hour)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    CleanupOldJobs();
                });
            }
        }

        private async Task UpdateDocumentInDatabaseAsync(Guid documentId, DocumentVerificationResult result)
        {
            try
            {
                var document = await _context.Documents.FindAsync(documentId);
                if (document != null)
                {
                    document.VerificationStatus = result.VerificationStatus;
                    document.ConfidenceScore = result.ConfidenceScore;
                    document.IsBlurred = result.IsBlurred;
                    document.IsCorrectType = result.IsCorrectType;
                    document.StatusColor = result.StatusColor;
                    document.VerificationDetails = result.VerificationDetails;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Updated document {DocumentId} with verification results", documentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document {DocumentId} with verification results", documentId);
            }
        }

        private void CleanupOldJobs()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-1);
                var oldJobs = _jobStatuses
                    .Where(kvp => kvp.Value.StartedAt < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var jobId in oldJobs)
                {
                    _jobStatuses.TryRemove(jobId, out _);
                    _jobResults.TryRemove(jobId, out _);
                }

                if (oldJobs.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} old verification jobs", oldJobs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old verification jobs");
            }
        }
    }
}