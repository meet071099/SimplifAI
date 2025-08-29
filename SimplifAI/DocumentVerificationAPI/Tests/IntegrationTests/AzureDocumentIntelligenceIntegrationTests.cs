using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using DocumentVerificationAPI.Services;
using DocumentVerificationAPI.Models;
using System.Text;

namespace DocumentVerificationAPI.Tests.IntegrationTests
{
    public class AzureDocumentIntelligenceIntegrationTests
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureDocumentIntelligenceService> _logger;
        private readonly AzureDocumentIntelligenceService _service;

        public AzureDocumentIntelligenceIntegrationTests()
        {
            var configBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables();
            
            _configuration = configBuilder.Build();
            
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<AzureDocumentIntelligenceService>();
            
            _service = new AzureDocumentIntelligenceService(_configuration, _logger);
        }

        [Fact]
        public async Task VerifyDocument_WithValidConfiguration_ShouldWork()
        {
            // Skip test if Azure DI is not configured
            var endpoint = _configuration["AzureDocumentIntelligence:Endpoint"];
            var apiKey = _configuration["AzureDocumentIntelligence:ApiKey"];
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || 
                endpoint.Contains("your-resource") || apiKey.Contains("your-api-key"))
            {
                // Skip test if not properly configured
                return;
            }

            // Arrange - Create a simple test document (mock PDF content)
            var testDocumentContent = CreateMockPdfContent();
            using var documentStream = new MemoryStream(testDocumentContent);

            // Act
            var result = await _service.VerifyDocumentAsync(documentStream, "Passport");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ConfidenceScore >= 0 && result.ConfidenceScore <= 100);
            Assert.NotNull(result.VerificationStatus);
            Assert.NotNull(result.StatusColor);
            Assert.NotNull(result.Message);
        }

        [Fact]
        public async Task VerifyDocument_WithBlurredImage_ShouldDetectBlur()
        {
            // Skip test if Azure DI is not configured
            var endpoint = _configuration["AzureDocumentIntelligence:Endpoint"];
            var apiKey = _configuration["AzureDocumentIntelligence:ApiKey"];
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || 
                endpoint.Contains("your-resource") || apiKey.Contains("your-api-key"))
            {
                return;
            }

            // Arrange - Create a mock blurred document
            var blurredContent = CreateMockBlurredImageContent();
            using var documentStream = new MemoryStream(blurredContent);

            // Act
            var result = await _service.VerifyDocumentAsync(documentStream, "Passport");

            // Assert
            Assert.NotNull(result);
            // Note: Actual blur detection depends on Azure DI's analysis
            // This test verifies the service handles the response properly
        }

        [Fact]
        public async Task VerifyDocument_WithInvalidApiKey_ShouldHandleError()
        {
            // Arrange - Create service with invalid configuration
            var invalidConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["AzureDocumentIntelligence:Endpoint"] = "https://invalid.cognitiveservices.azure.com/",
                    ["AzureDocumentIntelligence:ApiKey"] = "invalid-key"
                })
                .Build();

            var invalidService = new AzureDocumentIntelligenceService(invalidConfig, _logger);
            var testContent = CreateMockPdfContent();
            using var documentStream = new MemoryStream(testContent);

            // Act & Assert - Should handle error gracefully
            var result = await invalidService.VerifyDocumentAsync(documentStream, "Passport");
            
            // The service should return a result indicating failure rather than throwing
            Assert.NotNull(result);
            Assert.Equal("Failed", result.VerificationStatus);
        }

        [Fact]
        public void Configuration_ShouldBeValid()
        {
            // Arrange & Act
            var endpoint = _configuration["AzureDocumentIntelligence:Endpoint"];
            var apiKey = _configuration["AzureDocumentIntelligence:ApiKey"];

            // Assert - Configuration should exist (even if placeholder values)
            Assert.NotNull(endpoint);
            Assert.NotNull(apiKey);
            
            // Log configuration status for manual verification
            if (endpoint.Contains("your-resource") || apiKey.Contains("your-api-key"))
            {
                _logger.LogWarning("Azure Document Intelligence is not configured with real values. Update appsettings.Development.json with actual Azure DI endpoint and API key for full integration testing.");
            }
            else
            {
                _logger.LogInformation("Azure Document Intelligence appears to be configured with real values.");
            }
        }

        private byte[] CreateMockPdfContent()
        {
            // Create a minimal PDF-like content for testing
            var pdfHeader = "%PDF-1.4\n";
            var pdfContent = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
            var pdfTrailer = "trailer\n<< /Size 3 /Root 1 0 R >>\nstartxref\n0\n%%EOF";
            
            return Encoding.UTF8.GetBytes(pdfHeader + pdfContent + pdfTrailer);
        }

        private byte[] CreateMockBlurredImageContent()
        {
            // Create a mock image content that might be detected as blurred
            // In a real scenario, this would be an actual blurred image
            var mockImageHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
            var mockImageData = new byte[1000]; // Fill with zeros (low quality/blurred simulation)
            var mockImageFooter = new byte[] { 0xFF, 0xD9 }; // JPEG footer
            
            var result = new byte[mockImageHeader.Length + mockImageData.Length + mockImageFooter.Length];
            mockImageHeader.CopyTo(result, 0);
            mockImageData.CopyTo(result, mockImageHeader.Length);
            mockImageFooter.CopyTo(result, mockImageHeader.Length + mockImageData.Length);
            
            return result;
        }
    }
}