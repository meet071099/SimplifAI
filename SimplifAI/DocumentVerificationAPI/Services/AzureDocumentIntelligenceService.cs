using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using DocumentVerificationAPI.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocumentVerificationAPI.Services
{
    public class AzureDocumentIntelligenceService : IDocumentVerificationService
    {
        private readonly ILogger<AzureDocumentIntelligenceService> _logger;
        private readonly DocumentAnalysisClient _client;
        private readonly string _endpoint;
        private readonly string _apiKey;

        private readonly Dictionary<string, string[]> _documentTypeKeywords = new()
        {
            { "Passport", new[] { "passport", "travel document", "nationality", "passport no", "passport number", "given names", "surname", "date of birth", "place of birth", "issuing authority", "country code" } },
            { "DriverLicense", new[] { "driver", "license", "licence", "driving", "dl", "driver's license", "class", "restrictions", "endorsements", "expires", "issued", "vehicle", "motorcycle" } },
            { "NationalId", new[] { "national", "identity", "id card", "citizen", "national id", "identification", "id number", "date of birth", "address", "government", "state issued" } },
            { "Visa", new[] { "visa", "entry", "immigration", "permit", "valid until", "visa type", "entries", "duration of stay", "embassy", "consulate" } },
            { "Birth Certificate", new[] { "birth", "certificate", "date of birth" } }
        };

        public AzureDocumentIntelligenceService(ILogger<AzureDocumentIntelligenceService> logger, IConfiguration configuration)
        {
            _logger = logger;
            
            _endpoint = configuration.GetValue<string>("AzureDocumentIntelligence:Endpoint") ?? string.Empty;
            _apiKey = configuration.GetValue<string>("AzureDocumentIntelligence:ApiKey") ?? string.Empty;

            // Validate configuration
            ValidateConfiguration();

            try
            {
                var credential = new AzureKeyCredential(_apiKey);
                _client = new DocumentAnalysisClient(new Uri(_endpoint), credential);
                _logger.LogInformation("Azure Document Intelligence client initialized successfully with endpoint: {Endpoint}", _endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Document Intelligence client with endpoint: {Endpoint}", _endpoint);
                throw;
            }
        }

        public async Task<DocumentVerificationResult> VerifyDocumentAsync(Stream documentStream, string expectedDocumentType, string fileName)
        {
            return await VerifyDocumentAsync(documentStream, expectedDocumentType, fileName, null);
        }

        public async Task<DocumentVerificationResult> VerifyDocumentAsync(Stream documentStream, string expectedDocumentType, string fileName, Guid? formId)
        {
            try
            {
                _logger.LogInformation("Starting Azure Document Intelligence analysis for {FileName}, expected type: {DocumentType}", fileName, expectedDocumentType);

                // Check service availability first
                if (!await IsServiceAvailableAsync())
                {
                    _logger.LogWarning("Azure Document Intelligence service is not available, falling back to basic validation");
                    return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Service temporarily unavailable");
                }

                // Reset stream position
                documentStream.Position = 0;

                // Validate document size before sending to Azure
                if (documentStream.Length > 50 * 1024 * 1024) // 50MB limit
                {
                    _logger.LogWarning("Document {FileName} exceeds size limit for Azure Document Intelligence", fileName);
                    return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Document too large for automated analysis");
                }

                // Set timeout for Azure operation
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-document", documentStream, cancellationToken: cts.Token);
                var azureResult = operation.Value;

                // Extract and analyze the document
                var analysisResult = AnalyzeDocumentResult(azureResult, expectedDocumentType, fileName);

                _logger.LogInformation("Azure Document Intelligence analysis completed for {FileName} with confidence: {Confidence}%", 
                    fileName, analysisResult.ConfidenceScore);

                return analysisResult;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout during Azure Document Intelligence analysis for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Request timed out - please try again");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Azure Document Intelligence analysis timed out for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Analysis timed out - please try again with a smaller or clearer document");
            }
            catch (RequestFailedException ex) when (ex.Status == 429)
            {
                _logger.LogWarning("Azure Document Intelligence rate limit exceeded for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Service temporarily busy - please try again in a few moments");
            }
            catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
            {
                _logger.LogError(ex, "Azure Document Intelligence authentication/authorization error for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Authentication error - service temporarily unavailable");
            }
            catch (RequestFailedException ex) when (ex.Status >= 500)
            {
                _logger.LogError(ex, "Azure Document Intelligence server error for {FileName}: {ErrorCode}", fileName, ex.ErrorCode);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Service temporarily unavailable - please try again later");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Document Intelligence API error for {FileName}: {ErrorCode} - {ErrorMessage}", 
                    fileName, ex.ErrorCode, ex.Message);
                
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, $"Document analysis failed: {GetUserFriendlyErrorMessage(ex)}");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error during Azure Document Intelligence analysis for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Network error - please check your connection and try again");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during document verification for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Unexpected error occurred - please try again or contact support");
            }
        }

        private DocumentVerificationResult CreateFallbackVerificationResult(string expectedDocumentType, string fileName, string errorMessage)
        {
            return new DocumentVerificationResult
            {
                ConfidenceScore = 0,
                IsBlurred = false,
                IsCorrectType = true, // Assume correct type for fallback
                VerificationStatus = "Manual Review Required",
                StatusColor = "Yellow",
                Message = $"{errorMessage}. Manual review will be required for this document.",
                RequiresUserConfirmation = true,
                VerificationDetails = CreateFallbackVerificationDetails(expectedDocumentType, fileName, errorMessage)
            };
        }

        private string GetUserFriendlyErrorMessage(RequestFailedException ex)
        {
            return ex.ErrorCode switch
            {
                "InvalidRequest" => "The document format is not supported or the file is corrupted",
                "InvalidImageSize" => "The document image is too large or too small",
                "InvalidImageFormat" => "The document format is not supported",
                "InvalidImageUrl" => "There was an issue accessing the document",
                "NotSupportedLanguage" => "The document language is not supported",
                "BadArgument" => "Invalid document provided",
                "Timeout" => "Document analysis timed out",
                "InternalServerError" => "Service temporarily unavailable",
                "ServiceUnavailable" => "Service temporarily unavailable",
                _ => "Document analysis failed"
            };
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                _logger.LogInformation("Testing Azure Document Intelligence service availability with endpoint: {Endpoint}", _endpoint);

                // Create a minimal valid document for testing service availability
                // Use a simple 1x1 pixel PNG image encoded as base64 - this is much smaller and more reliable than text
                var testImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAIAAADTED8xAAADMElEQVR4nOzVwQnAIBQFQYXff81RUkQCOyDj1YOPnbXWPmeTRef+/3O/OyBjzh3CD95BfqICMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMO0TAAD//2Anhf4QtqobAAAAAElFTkSuQmCC";
                var testImageBytes = Convert.FromBase64String(testImageBase64);
                
                using var testStream = new MemoryStream(testImageBytes);

                // Use a very short timeout and start the operation without waiting for completion
                // This tests connectivity and authentication without consuming resources
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Started, "prebuilt-document", testStream, cancellationToken: cts.Token);
                
                // If we get here without exceptions, the service is available and accessible
                _logger.LogInformation("Azure Document Intelligence service is available and accessible. Service connectivity test successful.");
                
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError("Azure Document Intelligence service returned 404 Not Found. " +
                               "This usually indicates an incorrect endpoint URL or the service is not available in the specified region. " +
                               "Current endpoint: {Endpoint}. " +
                               "Please verify the endpoint format and region. " +
                               "Expected format: https://{{resource-name}}.cognitiveservices.azure.com/", _endpoint);
                
                LogEndpointDiagnostics();
                return false;
            }
            catch (RequestFailedException ex) when (ex.Status == 401)
            {
                _logger.LogError("Azure Document Intelligence authentication failed (401 Unauthorized). " +
                               "This indicates an invalid API key. " +
                               "Please verify the API key is correct and active. " +
                               "API Key length: {ApiKeyLength}", _apiKey?.Length ?? 0);
                return false;
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError("Azure Document Intelligence access forbidden (403 Forbidden). " +
                               "This indicates insufficient permissions or subscription issues. " +
                               "Please check your Azure subscription status and ensure the Document Intelligence service is enabled. " +
                               "Endpoint: {Endpoint}", _endpoint);
                return false;
            }
            catch (RequestFailedException ex) when (ex.Status >= 500)
            {
                _logger.LogError(ex, "Azure Document Intelligence server error ({StatusCode}). " +
                                "The service is temporarily unavailable. " +
                                "Error Code: {ErrorCode}, Message: {ErrorMessage}", 
                                ex.Status, ex.ErrorCode, ex.Message);
                return false;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Document Intelligence API error ({StatusCode}). " +
                                "Error Code: {ErrorCode}, Message: {ErrorMessage}. " +
                                "Endpoint: {Endpoint}", 
                                ex.Status, ex.ErrorCode, ex.Message, _endpoint);
                
                LogEndpointDiagnostics();
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while connecting to Azure Document Intelligence service. " +
                                "This could indicate DNS resolution issues, network connectivity problems, or firewall restrictions. " +
                                "Endpoint: {Endpoint}. " +
                                "Please check your network connection and firewall settings.", _endpoint);
                
                LogEndpointDiagnostics();
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout while connecting to Azure Document Intelligence service. " +
                                "The service may be experiencing high load or network issues. " +
                                "Endpoint: {Endpoint}", _endpoint);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Azure Document Intelligence service availability check. " +
                                "Endpoint: {Endpoint}", _endpoint);
                
                LogEndpointDiagnostics();
                return false;
            }
        }

        private DocumentVerificationResult AnalyzeDocumentResult(AnalyzeResult result, string expectedDocumentType, string fileName)
        {
            // Extract text content
            var extractedText = result.Content ?? string.Empty;

            // Calculate overall confidence from Azure's analysis
            var overallConfidence = CalculateOverallConfidence(result);

            // Detect if document is blurred or poor quality
            var isBlurred = DetectBlurFromAzureResult(result, overallConfidence);

            // Detect document type from extracted content
            var detectedType = DetectDocumentTypeFromContent(extractedText, expectedDocumentType);
            var isCorrectType = string.Equals(detectedType, expectedDocumentType, StringComparison.OrdinalIgnoreCase);

            // Adjust confidence based on quality and type match
            var adjustedConfidence = AdjustConfidenceScore(overallConfidence, isBlurred, isCorrectType);

            // Determine verification status
            var (status, color, message, requiresConfirmation) = DetermineVerificationStatus(
                adjustedConfidence, isBlurred, isCorrectType, expectedDocumentType, detectedType);

            // Create detailed verification information
            var verificationDetails = CreateVerificationDetails(result, extractedText, detectedType, expectedDocumentType, fileName);

            return new DocumentVerificationResult
            {
                ConfidenceScore = adjustedConfidence,
                IsBlurred = isBlurred,
                IsCorrectType = isCorrectType,
                VerificationStatus = status,
                StatusColor = color,
                Message = message,
                RequiresUserConfirmation = requiresConfirmation,
                VerificationDetails = verificationDetails
            };
        }

        private decimal CalculateOverallConfidence(AnalyzeResult result)
        {
            if (result.Pages == null || !result.Pages.Any())
                return 0;

            var confidenceValues = new List<float>();

            foreach (var page in result.Pages)
            {
                // Collect word confidence scores
                if (page.Words != null)
                {
                    confidenceValues.AddRange(page.Words.Where(w => w.Confidence > 0).Select(w => w.Confidence));
                }

                // Collect line confidence scores if available
                if (page.Lines != null)
                {
                    foreach (var line in page.Lines)
                    {
                        // Lines don't have direct confidence, but we can infer from word quality
                        if (!string.IsNullOrWhiteSpace(line.Content))
                        {
                            confidenceValues.Add(0.85f); // Assume good confidence for detected lines
                        }
                    }
                }
            }

            if (!confidenceValues.Any())
                return 30; // Low confidence if no confidence data available

            var averageConfidence = confidenceValues.Average();
            return Math.Round((decimal)(averageConfidence * 100), 2);
        }

        private bool DetectBlurFromAzureResult(AnalyzeResult result, decimal overallConfidence)
        {
            // Primary blur detection: very low confidence indicates poor image quality
            if (overallConfidence < 25)
                return true;

            // Secondary blur detection: check word density and recognition quality
            if (result.Pages != null)
            {
                var totalWords = result.Pages.Sum(p => p.Words?.Count ?? 0);
                var totalPages = result.Pages.Count;
                var averageWordsPerPage = totalWords / (float)totalPages;

                // If very few words detected per page with low confidence, likely blurred
                if (averageWordsPerPage < 15 && overallConfidence < 50)
                    return true;

                // Check for inconsistent confidence scores (sign of blur/distortion)
                var allConfidences = result.Pages
                    .Where(p => p.Words != null)
                    .SelectMany(p => p.Words!)
                    .Where(w => w.Confidence > 0)
                    .Select(w => w.Confidence)
                    .ToList();

                if (allConfidences.Any())
                {
                    var confidenceVariance = CalculateVariance(allConfidences);
                    // High variance in confidence scores can indicate blur or distortion
                    if (confidenceVariance > 0.15 && overallConfidence < 60)
                        return true;
                }
            }

            return false;
        }

        private float CalculateVariance(List<float> values)
        {
            if (!values.Any()) return 0;

            var mean = values.Average();
            var variance = values.Select(x => Math.Pow(x - mean, 2)).Average();
            return (float)variance;
        }

        private string DetectDocumentTypeFromContent(string extractedText, string expectedDocumentType)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
                return "Unknown";

            var lowerText = extractedText.ToLower();
            var bestMatch = "Unknown";
            var highestScore = 0;

            // Score each document type based on keyword matches
            foreach (var kvp in _documentTypeKeywords)
            {
                var score = kvp.Value.Count(keyword => lowerText.Contains(keyword.ToLower()));
                if (score > highestScore)
                {
                    highestScore = score;
                    bestMatch = kvp.Key;
                }
            }

            // If no clear match found, return Unknown rather than guessing
            return highestScore >= 2 ? bestMatch : "Unknown";
        }

        private decimal AdjustConfidenceScore(decimal baseConfidence, bool isBlurred, bool isCorrectType)
        {
            var adjustedConfidence = baseConfidence;

            // Significantly reduce confidence for blurred documents
            if (isBlurred)
                adjustedConfidence = Math.Max(0, adjustedConfidence - 40);

            // Reduce confidence for wrong document type
            if (!isCorrectType)
                adjustedConfidence = Math.Max(0, adjustedConfidence - 30);

            return Math.Min(100, Math.Max(0, adjustedConfidence));
        }

        private (string status, string color, string message, bool requiresConfirmation) DetermineVerificationStatus(
            decimal confidenceScore, bool isBlurred, bool isCorrectType, string expectedType, string detectedType)
        {
            // Handle blurred documents first
            if (isBlurred)
            {
                return ("Failed", "Red", 
                    "Document appears to be blurred or of poor quality. Please upload a clear, high-quality image.", 
                    false);
            }

            // Handle incorrect document type
            if (!isCorrectType)
            {
                if (detectedType == "Unknown")
                {
                    return ("Warning", "Yellow", 
                        $"Could not clearly identify document type. Expected {expectedType}. Please ensure you uploaded the correct document type with clear, readable text.", 
                        true);
                }
                else
                {
                    return ("Warning", "Yellow", 
                        $"Document appears to be a {detectedType}, but {expectedType} was expected. Please verify you uploaded the correct document type.", 
                        false);
                }
            }

            // Handle confidence-based scoring for correct, clear documents
            if (confidenceScore >= 85)
            {
                return ("Verified", "Green", 
                    $"Document verified successfully with high confidence ({confidenceScore}%).", 
                    false);
            }
            else if (confidenceScore >= 60)
            {
                return ("Verified", "Green", 
                    $"Document verified with good confidence ({confidenceScore}%).", 
                    false);
            }
            else if (confidenceScore >= 40)
            {
                return ("Review", "Yellow", 
                    $"Document verification completed with moderate confidence ({confidenceScore}%). Please review the document quality.", 
                    true);
            }
            else
            {
                return ("Review", "Red", 
                    $"Document verification completed with low confidence ({confidenceScore}%). Please consider uploading a clearer image or different document.", 
                    true);
            }
        }

        private string CreateVerificationDetails(AnalyzeResult result, string extractedText, string detectedType, string expectedDocumentType, string fileName)
        {
            var details = new
            {
                FileName = fileName,
                ExpectedType = expectedDocumentType,
                DetectedType = detectedType,
                AnalysisTimestamp = DateTime.UtcNow,
                AzureModelUsed = "prebuilt-document",
                DocumentAnalysis = new
                {
                    PagesAnalyzed = result.Pages?.Count ?? 0,
                    TotalWords = result.Pages?.Sum(p => p.Words?.Count ?? 0) ?? 0,
                    TotalLines = result.Pages?.Sum(p => p.Lines?.Count ?? 0) ?? 0,
                    ExtractedTextLength = extractedText.Length,
                    HasTables = result.Tables?.Any() ?? false,
                    TableCount = result.Tables?.Count ?? 0
                },
                QualityMetrics = new
                {
                    AverageWordConfidence = result.Pages?
                        .Where(p => p.Words != null)
                        .SelectMany(p => p.Words!)
                        .Where(w => w.Confidence > 0)
                        .Average(w => w.Confidence) ?? 0,
                    WordsWithLowConfidence = result.Pages?
                        .Where(p => p.Words != null)
                        .SelectMany(p => p.Words!)
                        .Count(w => w.Confidence < 0.5) ?? 0
                },
                ExtractedTextPreview = extractedText.Length > 300 ? extractedText[..300] + "..." : extractedText
            };

            return JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
        }

        private string CreateFallbackVerificationDetails(string expectedDocumentType, string fileName, string errorMessage)
        {
            var details = new
            {
                FileName = fileName,
                ExpectedType = expectedDocumentType,
                DetectedType = "Unknown - Service Unavailable",
                AnalysisTimestamp = DateTime.UtcNow,
                AzureModelUsed = "N/A - Fallback Mode",
                ErrorMessage = errorMessage,
                FallbackMode = true,
                ManualReviewRequired = true,
                DocumentAnalysis = new
                {
                    PagesAnalyzed = 0,
                    TotalWords = 0,
                    TotalLines = 0,
                    ExtractedTextLength = 0,
                    HasTables = false,
                    TableCount = 0
                },
                QualityMetrics = new
                {
                    AverageWordConfidence = 0.0,
                    WordsWithLowConfidence = 0
                },
                ExtractedTextPreview = "Text extraction unavailable - service error"
            };

            return JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task<DocumentVerificationResult> GetVerificationStatusAsync(Guid documentId)
        {
            // This would typically query a database for the document status
            // For now, return a placeholder implementation
            await Task.Delay(1);
            return new DocumentVerificationResult
            {
                DocumentId = documentId,
                VerificationStatus = "Pending",
                Message = "Document verification status not implemented"
            };
        }

        public async Task<bool> ConfirmDocumentAsync(Guid documentId)
        {
            // This would typically update the document status in the database
            await Task.Delay(1);
            return true;
        }

        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            // This would typically delete the document from storage and database
            await Task.Delay(1);
            return true;
        }

        public async Task<DocumentVerificationResult> RetryVerificationAsync(Guid documentId)
        {
            // This would typically retry verification for a failed document
            await Task.Delay(1);
            return new DocumentVerificationResult
            {
                DocumentId = documentId,
                VerificationStatus = "Retry",
                Message = "Document retry verification not implemented"
            };
        }

        public string[] GetSupportedDocumentTypes()
        {
            return _documentTypeKeywords.Keys.ToArray();
        }

        #region Configuration Validation and Diagnostics

        private void ValidateConfiguration()
        {
            var errors = new List<string>();

            // Validate endpoint
            if (string.IsNullOrEmpty(_endpoint))
            {
                errors.Add("Azure Document Intelligence endpoint is not configured");
            }
            else if (!IsEndpointFormatValid(_endpoint))
            {
                errors.Add($"Azure Document Intelligence endpoint format is invalid: {_endpoint}. " +
                          "Expected format: https://{{resource-name}}.cognitiveservices.azure.com/");
            }

            // Validate API key
            if (string.IsNullOrEmpty(_apiKey))
            {
                errors.Add("Azure Document Intelligence API key is not configured");
            }
            else if (_apiKey.Length < 32)
            {
                errors.Add("Azure Document Intelligence API key appears to be too short (expected at least 32 characters)");
            }

            if (errors.Any())
            {
                var errorMessage = "Azure Document Intelligence configuration validation failed:\n" + 
                                 string.Join("\n", errors.Select(e => $"- {e}"));
                
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("Azure Document Intelligence configuration validation passed. " +
                                 "Endpoint: {Endpoint}, API Key Length: {ApiKeyLength}", 
                                 _endpoint, _apiKey?.Length ?? 0);
        }

        private bool IsEndpointFormatValid(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                return false;

            try
            {
                // Azure Document Intelligence endpoints should follow this pattern:
                // https://{resource-name}.cognitiveservices.azure.com/
                var pattern = @"^https://[\w-]+\.cognitiveservices\.azure\.com/?$";
                var isValidFormat = Regex.IsMatch(endpoint, pattern, RegexOptions.IgnoreCase);

                if (!isValidFormat)
                {
                    _logger.LogWarning("Endpoint format validation failed for: {Endpoint}. " +
                                     "Expected pattern: https://{{resource-name}}.cognitiveservices.azure.com/", endpoint);
                }

                return isValidFormat;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating endpoint format: {Endpoint}", endpoint);
                return false;
            }
        }

        private void LogEndpointDiagnostics()
        {
            try
            {
                _logger.LogInformation("=== Azure Document Intelligence Configuration Diagnostics ===");
                _logger.LogInformation("Endpoint: {Endpoint}", _endpoint);
                _logger.LogInformation("Endpoint Format Valid: {IsValid}", IsEndpointFormatValid(_endpoint));
                _logger.LogInformation("API Key Configured: {IsConfigured}", !string.IsNullOrEmpty(_apiKey));
                _logger.LogInformation("API Key Length: {Length}", _apiKey?.Length ?? 0);

                if (!string.IsNullOrEmpty(_endpoint))
                {
                    var uri = new Uri(_endpoint);
                    _logger.LogInformation("Endpoint Host: {Host}", uri.Host);
                    _logger.LogInformation("Endpoint Scheme: {Scheme}", uri.Scheme);
                    
                    // Extract region from hostname if possible
                    var hostParts = uri.Host.Split('.');
                    if (hostParts.Length > 0)
                    {
                        _logger.LogInformation("Resource Name: {ResourceName}", hostParts[0]);
                    }
                }

                _logger.LogInformation("=== End Diagnostics ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating endpoint diagnostics");
            }
        }

        #endregion
    }
}