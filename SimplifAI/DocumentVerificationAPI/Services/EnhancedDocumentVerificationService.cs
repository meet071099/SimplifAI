using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using DocumentVerificationAPI.Models;
using Microsoft.Identity.Client;
using System.Text.Json;

namespace DocumentVerificationAPI.Services
{
    public class EnhancedDocumentVerificationService : IDocumentVerificationService
    {
        private readonly ILogger<EnhancedDocumentVerificationService> _logger;
        private readonly DocumentAnalysisClient _client;
        private readonly IAzureAIFoundryService _azureAIFoundryService;
        private readonly IFormService _formService;
        private readonly string _endpoint;
        private readonly string _apiKey;

        public EnhancedDocumentVerificationService(
            ILogger<EnhancedDocumentVerificationService> logger, 
            IConfiguration configuration,
            IAzureAIFoundryService azureAIFoundryService,
            IFormService formService)
        {
            _logger = logger;
            _azureAIFoundryService = azureAIFoundryService;
            _formService = formService;
            
            _endpoint = configuration.GetValue<string>("AzureDocumentIntelligence:Endpoint") ?? string.Empty;
            _apiKey = configuration.GetValue<string>("AzureDocumentIntelligence:ApiKey") ?? string.Empty;

            // Validate configuration
            ValidateConfiguration();

            try
            {
                var credential = new AzureKeyCredential(_apiKey);
                _client = new DocumentAnalysisClient(new Uri(_endpoint), credential);
                _logger.LogInformation("*** ENHANCED DOCUMENT VERIFICATION SERVICE INITIALIZED *** with endpoint: {Endpoint}", _endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Enhanced Document Intelligence client with endpoint: {Endpoint}", _endpoint);
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
                _logger.LogInformation("*** ENHANCED DOCUMENT VERIFICATION SERVICE CALLED *** Starting enhanced document verification for {FileName}, expected type: {DocumentType}, formId: {FormId}", 
                    fileName, expectedDocumentType, formId);

                // Check service availability first
                //if (!await IsServiceAvailableAsync())
                //{
                //    _logger.LogWarning("Azure Document Intelligence service is not available, falling back to basic validation");
                //    return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Service temporarily unavailable");
                //}

                // Reset stream position
                documentStream.Position = 0;

                // Validate document size before sending to Azure
                if (documentStream.Length > 50 * 1024 * 1024) // 50MB limit
                {
                    _logger.LogWarning("Document {FileName} exceeds size limit for Azure Document Intelligence", fileName);
                    return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Document too large for automated analysis");
                }

                // Step 1: Extract information using Azure Document Intelligence
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-document", documentStream, cancellationToken: cts.Token);
                var azureResult = operation.Value;

                // Step 2: Check if document is blurred
                var extractedText = azureResult.Content ?? string.Empty;
                var overallConfidence = CalculateOverallConfidence(azureResult);
                var isBlurred = DetectBlurFromAzureResult(azureResult, overallConfidence);

                // If document is blurred, return immediately without AI Foundry verification
                if (isBlurred)
                {
                    _logger.LogInformation("Document {FileName} is blurred, skipping AI Foundry verification", fileName);
                    return CreateBlurredDocumentResult(expectedDocumentType, fileName, extractedText, overallConfidence);
                }

                // Step 3: If not blurred and formId is provided, verify names using Azure AI Foundry
                string authenticityResult = "not verified";
                PromptResponse resultresponse = new PromptResponse();
                if (formId.HasValue)
                {
                    try
                    {
                        // Get personal info from form
                        var personalInfo = await _formService.GetPersonalInfoAsync(formId.Value);
                        if (personalInfo != null)
                        {
                            // Create authenticity request
                            var authenticityRequest = new DocumentAuthenticityRequest
                            {
                                FormFirstName = personalInfo.FirstName ?? string.Empty,
                                FormLastName = personalInfo.LastName ?? string.Empty,
                                ExtractedText = extractedText,
                                DocumentType = expectedDocumentType,
                                FormId = formId
                            };

                            // Verify authenticity using Azure AI Foundry
                            var promptResponse = await _azureAIFoundryService.VerifyDocumentAuthenticityAsync(authenticityRequest);
                            resultresponse = promptResponse;
                            authenticityResult = promptResponse.isAuthentic ? "authentic" : "not authentic";
                            _logger.LogInformation("AI Foundry authenticity verification completed for {FileName}: IsAuthentic={IsAuthentic}, Reason={Reason}", 
                                fileName, promptResponse.isAuthentic, promptResponse.reason);
                        }
                        else
                        {
                            _logger.LogWarning("Could not retrieve personal info for formId {FormId}, skipping authenticity verification", formId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during AI Foundry authenticity verification for {FileName}", fileName);
                        authenticityResult = "verification failed";
                    }
                }
                else
                {
                    _logger.LogInformation("No formId provided for {FileName}, skipping authenticity verification", fileName);
                }

                // Adjust confidence score based on authenticity result
                var adjustedConfidence = AzureAIFoundryService.AdjustConfidenceForAuthenticity(overallConfidence, authenticityResult);

                // Create the final verification result
                var result = CreateEnhancedVerificationResult(
                    azureResult, 
                    extractedText, 
                    expectedDocumentType, 
                    fileName, 
                    adjustedConfidence, 
                    isBlurred, 
                    authenticityResult,
                    resultresponse);

                _logger.LogInformation("Enhanced document verification completed for {FileName} with confidence: {Confidence}%, authenticity: {Authenticity}", 
                    fileName, result.ConfidenceScore, authenticityResult);

                return result;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout during enhanced document verification for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Request timed out - please try again");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Enhanced document verification timed out for {FileName}", fileName);
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
                _logger.LogError(ex, "Network error during enhanced document verification for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Network error - please check your connection and try again");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during enhanced document verification for {FileName}", fileName);
                return CreateFallbackVerificationResult(expectedDocumentType, fileName, "Unexpected error occurred - please try again or contact support");
            }
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

        private DocumentVerificationResult CreateBlurredDocumentResult(string expectedDocumentType, string fileName, string extractedText, decimal confidence)
        {
            return new DocumentVerificationResult
            {
                ConfidenceScore = confidence,
                IsBlurred = true,
                IsCorrectType = true, // We don't check type for blurred documents
                VerificationStatus = "Failed",
                StatusColor = "Red",
                Message = "Document appears to be blurred or of poor quality. Please upload a clear, high-quality image.",
                RequiresUserConfirmation = false,
                VerificationDetails = CreateSimpleVerificationDetails(expectedDocumentType, fileName, extractedText, "Blurred document detected", confidence),
                AuthenticityResult = "not performed",
                AuthenticityVerified = false,
                AIFoundryUsed = false
            };
        }

        private DocumentVerificationResult CreateEnhancedVerificationResult(
            AnalyzeResult azureResult, 
            string extractedText, 
            string expectedDocumentType, 
            string fileName, 
            decimal confidence, 
            bool isBlurred, 
            string authenticityResult,
            PromptResponse response)
        {
            // Determine verification status based on blur and authenticity
            var (status, color, message, requiresConfirmation) = DetermineEnhancedVerificationStatus(
                confidence, isBlurred, authenticityResult, expectedDocumentType);

            return new DocumentVerificationResult
            {
                ConfidenceScore = confidence,
                IsBlurred = isBlurred,
                IsCorrectType = true, // We no longer check document type using keywords
                VerificationStatus = status,
                StatusColor = color,
                Message = message,
                RequiresUserConfirmation = requiresConfirmation,
                VerificationDetails = CreateEnhancedVerificationDetails(azureResult, extractedText, expectedDocumentType, fileName, authenticityResult),
                AuthenticityResult = authenticityResult,
                AuthenticityVerified = authenticityResult.Equals("authentic", StringComparison.OrdinalIgnoreCase),
                AIFoundryUsed = !authenticityResult.Equals("not verified", StringComparison.OrdinalIgnoreCase),
                promptResponse = response
            };
        }

        private (string status, string color, string message, bool requiresConfirmation) DetermineEnhancedVerificationStatus(
            decimal confidenceScore, bool isBlurred, string authenticityResult, string expectedType)
        {
            // Handle blurred documents first
            if (isBlurred)
            {
                return ("Failed", "Red", 
                    "Document appears to be blurred or of poor quality. Please upload a clear, high-quality image.", 
                    false);
            }

            // Handle authenticity verification results
            if (authenticityResult.Equals("authentic", StringComparison.OrdinalIgnoreCase))
            {
                if (confidenceScore >= 70)
                {
                    return ("Verified", "Green", 
                        $"Document verified successfully. Personal information matches form data with {confidenceScore}% confidence.", 
                        false);
                }
                else
                {
                    return ("Verified", "Green", 
                        $"Document verified. Personal information matches form data, but image quality could be improved ({confidenceScore}% confidence).", 
                        false);
                }
            }
            else if (authenticityResult.StartsWith("not authentic", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the detailed reason if available
                var reason = "Personal information does not match form data";
                if (authenticityResult.Contains(":"))
                {
                    var reasonPart = authenticityResult.Substring(authenticityResult.IndexOf(':') + 1).Trim();
                    if (!string.IsNullOrEmpty(reasonPart) && !reasonPart.Equals("reason not specified", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = reasonPart;
                    }
                }

                return ("Failed", "Red", 
                    $"Document verification failed. {reason} (Confidence: {confidenceScore}%)", 
                    false);
            }
            else if (authenticityResult.Contains("verification failed") || authenticityResult.Contains("service error"))
            {
                return ("Review", "Yellow", 
                    $"Document extracted successfully ({confidenceScore}% confidence), but authenticity verification encountered an error. Manual review required.", 
                    true);
            }
            else if (authenticityResult.Equals("not verified", StringComparison.OrdinalIgnoreCase))
            {
                // No form data provided for verification
                if (confidenceScore >= 70)
                {
                    return ("Extracted", "Blue", 
                        $"Document text extracted successfully with {confidenceScore}% confidence. No personal information verification performed.", 
                        false);
                }
                else
                {
                    return ("Review", "Yellow", 
                        $"Document text extracted with {confidenceScore}% confidence. Consider uploading a clearer image for better results.", 
                        true);
                }
            }
            else
            {
                // Unknown authenticity result
                return ("Review", "Yellow", 
                    $"Document extracted with {confidenceScore}% confidence, but authenticity verification returned unexpected result. Manual review required.", 
                    true);
            }
        }

        private string CreateEnhancedVerificationDetails(AnalyzeResult result, string extractedText, string expectedDocumentType, string fileName, string authenticityResult)
        {
            var details = new
            {
                FileName = fileName,
                ExpectedType = expectedDocumentType,
                AnalysisTimestamp = DateTime.UtcNow,
                AzureModelUsed = "prebuilt-document",
                AuthenticityVerification = new
                {
                    Result = authenticityResult,
                    VerifiedByAI = !authenticityResult.Equals("not verified", StringComparison.OrdinalIgnoreCase),
                    AIFoundryUsed = true,
                    IsAuthentic = authenticityResult.Equals("authentic", StringComparison.OrdinalIgnoreCase),
                    Reason = authenticityResult.StartsWith("not authentic:", StringComparison.OrdinalIgnoreCase) 
                        ? authenticityResult.Substring(authenticityResult.IndexOf(':') + 1).Trim() 
                        : null,
                    ConfidenceAdjusted = true
                },
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

        private string CreateSimpleVerificationDetails(string expectedDocumentType, string fileName, string extractedText, string reason, decimal confidence)
        {
            var details = new
            {
                FileName = fileName,
                ExpectedType = expectedDocumentType,
                AnalysisTimestamp = DateTime.UtcNow,
                AzureModelUsed = "prebuilt-document",
                Reason = reason,
                AuthenticityVerification = new
                {
                    Result = "not performed",
                    VerifiedByAI = false,
                    AIFoundryUsed = false,
                    Reason = reason
                },
                DocumentAnalysis = new
                {
                    ExtractedTextLength = extractedText.Length,
                    OverallConfidence = confidence
                },
                ExtractedTextPreview = extractedText.Length > 300 ? extractedText[..300] + "..." : extractedText
            };

            return JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
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
                VerificationDetails = CreateFallbackVerificationDetails(expectedDocumentType, fileName, errorMessage),
                AuthenticityResult = "not performed",
                AuthenticityVerified = false,
                AIFoundryUsed = false
            };
        }

        private string CreateFallbackVerificationDetails(string expectedDocumentType, string fileName, string errorMessage)
        {
            var details = new
            {
                FileName = fileName,
                ExpectedType = expectedDocumentType,
                AnalysisTimestamp = DateTime.UtcNow,
                AzureModelUsed = "N/A - Fallback Mode",
                ErrorMessage = errorMessage,
                FallbackMode = true,
                ManualReviewRequired = true,
                AuthenticityVerification = new
                {
                    Result = "not performed",
                    VerifiedByAI = false,
                    AIFoundryUsed = false,
                    Reason = "Service unavailable"
                },
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
                var testImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAIAAADTED8xAAADMElEQVR4nOzVwQnAIBQFQYXff81RUkQCOyDj1YOPnbXWPmeTRef+/3O/OyBjzh3CD95BfqICMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMK0CMO0TAAD//2Anhf4QtqobAAAAAElFTkSuQmCC";
                var testImageBytes = Convert.FromBase64String(testImageBase64);
                
                using var testStream = new MemoryStream(testImageBytes);

                // Use a very short timeout and start the operation without waiting for completion
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Started, "prebuilt-document", testStream, cancellationToken: cts.Token);
                
                _logger.LogInformation("Azure Document Intelligence service is available and accessible. Service connectivity test successful.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure Document Intelligence service availability check failed");
                return false;
            }
        }

        private void ValidateConfiguration()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(_endpoint))
            {
                errors.Add("Azure Document Intelligence endpoint is not configured");
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                errors.Add("Azure Document Intelligence API key is not configured");
            }

            if (errors.Any())
            {
                var errorMessage = "Configuration validation failed: " + string.Join(", ", errors);
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }

        // Implement remaining interface methods with basic implementations
        public async Task<DocumentVerificationResult> GetVerificationStatusAsync(Guid documentId)
        {
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
            await Task.Delay(1);
            return true;
        }

        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            await Task.Delay(1);
            return true;
        }

        public async Task<DocumentVerificationResult> RetryVerificationAsync(Guid documentId)
        {
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
            // Return a simple list without keyword dependencies
            return new[] { "Passport", "DriverLicense", "NationalId", "Visa", "Birth Certificate" };
        }
    }
}