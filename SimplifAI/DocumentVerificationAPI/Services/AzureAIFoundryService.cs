using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using DocumentVerificationAPI.Models;
using System.ClientModel;
using System.Text.Json;

namespace DocumentVerificationAPI.Services
{
    public class AuthenticityResponse
    {
        public bool IsAuthentic { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class AzureAIFoundryService : IAzureAIFoundryService
    {
        private readonly ILogger<AzureAIFoundryService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _endpoint;
        private readonly string _agentId;
        private readonly int _timeoutSeconds;
        private readonly int _maxRetries;
        private readonly bool _enableFallback;
        private readonly string _apiKey;
        public AzureAIFoundryService(
            ILogger<AzureAIFoundryService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            _endpoint = configuration["AzureAIFoundry:Endpoint"] ?? throw new ArgumentNullException("AzureAIFoundry:Endpoint");
            _agentId = configuration["AzureAIFoundry:AgentId"] ?? throw new ArgumentNullException("AzureAIFoundry:AgentId");
            _timeoutSeconds = configuration.GetValue<int>("AzureAIFoundry:TimeoutSeconds", 120);
            _maxRetries = configuration.GetValue<int>("AzureAIFoundry:MaxRetries", 3);
            _enableFallback = configuration.GetValue<bool>("AzureAIFoundry:EnableFallback", true);
            _apiKey = configuration.GetValue<string>("AzureAIFoundry:ApiKey") ?? string.Empty;

            _logger.LogInformation("AzureAIFoundryService initialized with endpoint: {Endpoint}, agentId: {AgentId}", _endpoint, _agentId);
        }

        public async Task<PromptResponse> VerifyDocumentAuthenticityAsync(string formFirstName, string formLastName, string extractedText)
        {
            try
            {
                _logger.LogInformation("Starting document authenticity verification for {FirstName} {LastName}", formFirstName, formLastName);

                // Create the authenticity verification prompt
                var prompt = CreateAuthenticityVerificationPrompt(formFirstName, formLastName, extractedText);

                // Send request to Azure AI Foundry using SDK
                var response = await SendAuthenticityVerificationRequestAsync(prompt);

                _logger.LogInformation("Document authenticity verification completed successfully");
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during document authenticity verification");
                
                if (_enableFallback)
                {
                    return new PromptResponse 
                    { 
                        isAuthentic = false, 
                        reason = "Unable to verify document authenticity - service temporarily unavailable" 
                    };
                }
                
                throw;
            }
        }

        public async Task<PromptResponse> VerifyDocumentAuthenticityAsync(DocumentAuthenticityRequest request)
        {
            try
            {
                _logger.LogInformation("Starting document authenticity verification using enhanced method for {FirstName} {LastName}", 
                    request.FormFirstName, request.FormLastName);

                // Create enhanced authenticity verification prompt
                var prompt = CreateEnhancedAuthenticityVerificationPrompt(request);

                // Send request to Azure AI Foundry using SDK
                var response = await SendAuthenticityVerificationRequestAsync(prompt);

                _logger.LogInformation("Document authenticity verification completed successfully using enhanced method");
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during document authenticity verification using enhanced method");
                
                if (_enableFallback)
                {
                    return new PromptResponse 
                    { 
                        isAuthentic = false, 
                        reason = "Unable to verify document authenticity - service temporarily unavailable" 
                    };
                }
                
                throw;
            }
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                // Check if configuration is valid first
                if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_agentId))
                {
                    _logger.LogWarning("Azure AI Foundry configuration is invalid - missing endpoint or agent ID");
                    return false;
                }

                // Try to connect to Azure AI Foundry and get the agent
                var uri = new Uri(_endpoint);
                var projectClient = new AIProjectClient(uri, new DefaultAzureCredential());
                var agentsClient = projectClient.GetPersistentAgentsClient();
                
                // Use async method to get the agent
                var agentResponse = agentsClient.Administration.GetAgent(_agentId);
                var agent = agentResponse.Value;

                _logger.LogDebug("Successfully connected to Azure AI Foundry and retrieved agent {AgentId} - {AgentName}", _agentId, agent.Name);
                return agent != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure AI Foundry service availability check failed");
                return false;
            }
        }

        private string CreateEnhancedAuthenticityVerificationPrompt(DocumentAuthenticityRequest request)
        {
            return $@"
Please analyze the following document text and verify its authenticity based on the provided personal information.

Form Personal Information:
- First Name: {request.FormFirstName}
- Last Name: {request.FormLastName}
{(request.DocumentType != null ? $"- Expected Document Type: {request.DocumentType}" : "")}

Document Text Extracted:
{request.ExtractedText}

Please perform the following analysis with FLEXIBLE name matching:
1. Compare the names in the document with the form information using these flexible matching rules:
   - If the document contains additional names (like middle names) that aren't in the form, this is ACCEPTABLE
   - If the form contains names that aren't in the document (like missing surname), leverage what's available for verification
   - Consider variations in name formatting, order, or cultural naming conventions
   - Focus on substantial name matches rather than exact character-by-character matching
2. Check if the document appears to be authentic based on the extracted text
3. Look for any inconsistencies or red flags in the document content
4. Verify that the document type matches expectations (if provided)

FLEXIBLE MATCHING EXAMPLES:
- Document: ""JOHN MICHAEL SMITH"" vs Form: ""John Smith"" → ACCEPTABLE (document has additional middle name)
- Document: ""MARIA GONZALEZ"" vs Form: ""Maria Elena Gonzalez"" → ACCEPTABLE (form has additional middle name)
- Document: ""DAVID"" vs Form: ""David Johnson"" → ACCEPTABLE (document missing surname, leverage first name match)
- Document: ""CHEN WEI"" vs Form: ""Wei Chen"" → ACCEPTABLE (different name order, cultural variation)

IMPORTANT: You must respond with a valid JSON object in exactly this format:

{{
  ""isAuthentic"": boolean,
  ""reason"": ""detailed explanation of your analysis and decision""
}}

Examples:
- If authentic: {{""isAuthentic"": true, ""reason"": ""Document verified successfully. First name 'John' matches form data, document contains additional middle name 'Michael' which is acceptable. Document appears genuine.""}}
- If not authentic: {{""isAuthentic"": false, ""reason"": ""Significant name mismatch - document shows 'MICHELLE DE LA PAZ' but form shows 'Meet Patel'. No substantial name components match, indicating this document does not belong to the form submitter.""}}

Focus on substantial name component matches and document authenticity. Be flexible with name variations but strict with completely different names. Provide detailed reasoning in the 'reason' field.
";
        }

        private string CreateAuthenticityVerificationPrompt(string formFirstName, string formLastName, string extractedText)
        {
            return $@"
Please analyze the following document text and verify its authenticity based on the provided personal information.

Form Personal Information:
- First Name: {formFirstName}
- Last Name: {formLastName}

Document Text Extracted:
{extractedText}

Please perform the following analysis with FLEXIBLE name matching:
1. Compare the names in the document with the form information using these flexible matching rules:
   - If the document contains additional names (like middle names) that aren't in the form, this is ACCEPTABLE
   - If the form contains names that aren't in the document (like missing surname), leverage what's available for verification
   - Consider variations in name formatting, order, or cultural naming conventions
   - Focus on substantial name matches rather than exact character-by-character matching
2. Look for any inconsistencies or red flags in the document content
3. Check if the document appears genuine

FLEXIBLE MATCHING EXAMPLES:
- Document: ""JOHN MICHAEL SMITH"" vs Form: ""John Smith"" → ACCEPTABLE (document has additional middle name)
- Document: ""MARIA GONZALEZ"" vs Form: ""Maria Elena Gonzalez"" → ACCEPTABLE (form has additional middle name)
- Document: ""DAVID"" vs Form: ""David Johnson"" → ACCEPTABLE (document missing surname, leverage first name match)
- Document: ""CHEN WEI"" vs Form: ""Wei Chen"" → ACCEPTABLE (different name order, cultural variation)

IMPORTANT: You must respond with a valid JSON object in exactly this format:

{{
  ""isAuthentic"": boolean,
  ""reason"": ""detailed explanation of your analysis and decision""
}}

Examples:
- If authentic: {{""isAuthentic"": true, ""reason"": ""Document verified successfully. First name 'John' matches form data, document contains additional middle name 'Michael' which is acceptable. Document appears genuine.""}}
- If not authentic: {{""isAuthentic"": false, ""reason"": ""Significant name mismatch - document shows 'John Smith' but form shows 'Jane Doe'. No substantial name components match, indicating this document does not belong to the form submitter.""}}

Focus on substantial name component matches and document authenticity. Be flexible with name variations but strict with completely different names. Provide detailed reasoning in the 'reason' field.
";
        }

        private async Task<PromptResponse> SendAuthenticityVerificationRequestAsync(string prompt)
        {
            var retryCount = 0;
            while (retryCount < _maxRetries)
            {
                try
                {
                    _logger.LogDebug("Starting agent conversation for authenticity verification, attempt {Attempt}", retryCount + 1);

                    // Use the agent to process the prompt using Azure AI Foundry SDK
                    var agentResponse = await ProcessWithAgentAsync(prompt);

                    var promptResponse = JsonSerializer.Deserialize<PromptResponse>(agentResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (promptResponse != null)
                    {
                        _logger.LogDebug("Agent response parsed successfully: IsAuthentic={IsAuthentic}, Reason={Reason}", 
                            promptResponse.isAuthentic, promptResponse.reason);
                        return promptResponse;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize agent response to PromptResponse");
                        return new PromptResponse 
                        { 
                            isAuthentic = false, 
                            reason = "Unable to parse agent response" 
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Agent request failed, attempt {Attempt} of {MaxRetries}", retryCount + 1, _maxRetries);
                    
                    if (retryCount == _maxRetries - 1)
                    {
                        return new PromptResponse 
                        { 
                            isAuthentic = false, 
                            reason = "Unable to verify document authenticity - service error" 
                        };
                    }
                }

                retryCount++;
                if (retryCount < _maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // Exponential backoff
                }
            }

            return new PromptResponse 
            { 
                isAuthentic = false, 
                reason = "Unable to verify document authenticity - max retries exceeded" 
            };
        }

        private async Task<string> ProcessWithAgentAsync(string prompt)
        {
            _logger.LogInformation("Processing authenticity verification with agent {AgentId}", _agentId);
            
            // Use Azure AI Foundry SDK for agent interaction
            var agentResponse = await TryAgentInteractionAsync(prompt);
            
            if (!string.IsNullOrEmpty(agentResponse))
            {
                _logger.LogDebug("Successfully received agent response: {Response}", agentResponse);
                return agentResponse;
            }
            else
            {
                _logger.LogWarning("Agent returned empty response");
                return "Unable to verify document authenticity - no response from agent";
            }
        }

        private async Task<string> TryAgentInteractionAsync(string prompt)
        {
            Uri endpoint = new Uri(_endpoint);
            AIProjectClient projectClient = new(endpoint, new DefaultAzureCredential());

            PersistentAgentsClient agentsClient = projectClient.GetPersistentAgentsClient();

            PersistentAgent agent = agentsClient.Administration.GetAgent(_agentId);

            PersistentAgentThread thread = agentsClient.Threads.CreateThread();
            _logger.LogDebug("Created thread, ID: {ThreadId}", thread.Id);

            PersistentThreadMessage messageResponse = agentsClient.Messages.CreateMessage(
                thread.Id,
                Azure.AI.Agents.Persistent.MessageRole.User,
                prompt);

            Azure.AI.Agents.Persistent.ThreadRun run = agentsClient.Runs.CreateRun(
                thread.Id,
                agent.Id);

            // Poll until the run reaches a terminal status
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                run = agentsClient.Runs.GetRun(thread.Id, run.Id);
            }
            while (run.Status == Azure.AI.Agents.Persistent.RunStatus.Queued
                || run.Status == Azure.AI.Agents.Persistent.RunStatus.InProgress);
            
            if (run.Status != Azure.AI.Agents.Persistent.RunStatus.Completed)
            {
                throw new InvalidOperationException($"Run failed or was canceled: {run.LastError?.Message}");
            }

            Pageable<PersistentThreadMessage> messages = agentsClient.Messages.GetMessages(
                thread.Id, order: Azure.AI.Agents.Persistent.ListSortOrder.Ascending);

            // Extract the agent's response
            var agentResponse = ExtractAgentResponse(messages.ToList());
            
            // Log messages for debugging
            foreach (PersistentThreadMessage threadMessage in messages)
            {
                _logger.LogDebug($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role,10}");
                foreach (Azure.AI.Agents.Persistent.MessageContent contentItem in threadMessage.ContentItems)
                {
                    if (contentItem is Azure.AI.Agents.Persistent.MessageTextContent textItem)
                    {
                        _logger.LogDebug("Message content: {Content}", textItem.Text);
                    }
                }
            }

            return agentResponse;
        }



        private async Task<Azure.AI.Agents.Persistent.ThreadRun> WaitForRunCompletionAsync(PersistentAgentsClient agentsClient, string threadId, string runId)
        {
            var maxWaitTime = TimeSpan.FromSeconds(_timeoutSeconds);
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(2);

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                var runResponse = agentsClient.Runs.GetRun(threadId, runId);
                var run = runResponse.Value;

                _logger.LogDebug("Agent run status: {Status}", run.Status);

                if (run.Status == Azure.AI.Agents.Persistent.RunStatus.Completed)
                {
                    return run;
                }
                else if (run.Status == Azure.AI.Agents.Persistent.RunStatus.Failed || run.Status == Azure.AI.Agents.Persistent.RunStatus.Cancelled)
                {
                    throw new InvalidOperationException($"Agent run failed with status: {run.Status}");
                }

                await Task.Delay(pollInterval);
            }

            throw new TimeoutException($"Agent run did not complete within {maxWaitTime.TotalSeconds} seconds");
        }

        private string ExtractAgentResponse(IReadOnlyList<PersistentThreadMessage> messages)
        {
            // Get the most recent assistant message
            var assistantMessage = messages
                .Where(m => m.Role.ToString().Equals("assistant", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            if (assistantMessage != null)
            {
                try
                {
                    // Access ContentItems directly - this is where the actual content is stored
                    foreach (var contentItem in assistantMessage.ContentItems)
                    {
                        if (contentItem is Azure.AI.Agents.Persistent.MessageTextContent textItem)
                        {
                            var text = textItem.Text;
                            if (!string.IsNullOrEmpty(text))
                            {
                                _logger.LogDebug("Extracted agent response text: {Text}", text);
                                return text;
                            }
                        }
                    }
                    
                    _logger.LogWarning("No text content found in assistant message ContentItems");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract text from assistant message ContentItems");
                }
            }
            else
            {
                _logger.LogWarning("No assistant message found in the conversation");
            }

            return "No response received from agent";
        }



        private string ParseAuthenticityResponse(string responseContent)
        {
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return "Unable to determine authenticity - empty response";
            }

            var response = responseContent.Trim();
            
            // Try to parse as PromptResponse JSON first (from our serialized response)
            try
            {
                var promptResponse = JsonSerializer.Deserialize<PromptResponse>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (promptResponse != null)
                {
                    return promptResponse.isAuthentic ? "authentic" : "not authentic";
                }
            }
            catch (JsonException)
            {
                // Fall through to try AuthenticityResponse format
            }
            
            // Try to parse as AuthenticityResponse JSON (legacy format)
            try
            {
                // Look for JSON object in the response (sometimes AI adds extra text)
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var authenticityResponse = JsonSerializer.Deserialize<AuthenticityResponse>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (authenticityResponse != null)
                    {
                        if (authenticityResponse.IsAuthentic)
                        {
                            return "authentic";
                        }
                        else
                        {
                            // Return just the status, let MapToDocumentVerificationResponse handle formatting
                            return "not authentic";
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response from AI agent: {Response}", response);
                // Fall through to legacy parsing
            }

            // Fallback to legacy format parsing
            var upperResponse = response.ToUpperInvariant();
            
            if (upperResponse.StartsWith("AUTHENTIC"))
            {
                return "authentic";
            }
            else if (upperResponse.StartsWith("NOT_AUTHENTIC:"))
            {
                return "not authentic";
            }
            
            var lowerResponse = response.ToLowerInvariant();
            
            if (lowerResponse == "authentic")
            {
                return "authentic";
            }
            else if (lowerResponse == "not authentic")
            {
                return "not authentic";
            }
            
            // Check for responses containing the keywords
            if (lowerResponse.Contains("not authentic"))
            {
                return "not authentic";
            }
            else if (lowerResponse.Contains("authentic"))
            {
                return "authentic";
            }
            
            // Return the original response for MapToDocumentVerificationResponse to handle
            return response;
        }

        public static decimal AdjustConfidenceForAuthenticity(decimal originalConfidence, string authenticityResult)
        {
            if (string.IsNullOrWhiteSpace(authenticityResult))
            {
                return originalConfidence;
            }

            var lowerResult = authenticityResult.ToLowerInvariant();

            // If document is authentic, maintain or slightly boost confidence
            if (lowerResult.StartsWith("authentic"))
            {
                // Slight boost for authentic documents, but cap at 95%
                return Math.Min(95m, originalConfidence + 5m);
            }
            
            // If document is not authentic, significantly reduce confidence
            if (lowerResult.StartsWith("not authentic"))
            {
                // Reduce confidence based on severity of the issue
                if (lowerResult.Contains("name mismatch") || lowerResult.Contains("wrong document type"))
                {
                    // Severe issues - reduce confidence to 15-25%
                    return Math.Max(15m, originalConfidence * 0.25m);
                }
                else if (lowerResult.Contains("fake") || lowerResult.Contains("tampered"))
                {
                    // Very severe issues - reduce confidence to 5-15%
                    return Math.Max(5m, originalConfidence * 0.15m);
                }
                else
                {
                    // General authenticity issues - reduce confidence to 20-35%
                    return Math.Max(20m, originalConfidence * 0.35m);
                }
            }

            // For other cases (verification failed, etc.), slightly reduce confidence
            return Math.Max(originalConfidence * 0.8m, 10m);
        }
    }
}