using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DocumentVerificationAPI.Services;
using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI.Tests
{
    public class AzureAIFoundryServiceTests
    {
        private readonly Mock<ILogger<AzureAIFoundryService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<HttpClient> _mockHttpClient;

        public AzureAIFoundryServiceTests()
        {
            _mockLogger = new Mock<ILogger<AzureAIFoundryService>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockHttpClient = new Mock<HttpClient>();

            // Setup configuration
            _mockConfiguration.Setup(c => c["AzureAIFoundry:Endpoint"]).Returns("https://test-endpoint.com");
            _mockConfiguration.Setup(c => c["AzureAIFoundry:ApiKey"]).Returns("test-api-key");
            _mockConfiguration.Setup(c => c["AzureAIFoundry:AgentId"]).Returns("test-agent-id");
            _mockConfiguration.Setup(c => c.GetValue<int>("AzureAIFoundry:TimeoutSeconds", 120)).Returns(120);
            _mockConfiguration.Setup(c => c.GetValue<int>("AzureAIFoundry:MaxRetries", 3)).Returns(3);
            _mockConfiguration.Setup(c => c.GetValue<bool>("AzureAIFoundry:EnableFallback", true)).Returns(true);
        }

        [Fact]
        public void DocumentAuthenticityRequest_ValidData_CreatesCorrectly()
        {
            // Arrange & Act
            var request = new DocumentAuthenticityRequest
            {
                FormFirstName = "John",
                FormLastName = "Doe",
                ExtractedText = "John Doe, Driver License, DOB: 01/01/1990",
                DocumentType = "Driver License",
                FormId = Guid.NewGuid()
            };

            // Assert
            Assert.Equal("John", request.FormFirstName);
            Assert.Equal("Doe", request.FormLastName);
            Assert.Contains("John Doe", request.ExtractedText);
            Assert.Equal("Driver License", request.DocumentType);
            Assert.NotEqual(Guid.Empty, request.FormId);
        }

        [Fact]
        public void DocumentAuthenticityRequest_RequiredFields_AreNotEmpty()
        {
            // Arrange & Act
            var request = new DocumentAuthenticityRequest
            {
                FormFirstName = "Jane",
                FormLastName = "Smith",
                ExtractedText = "Sample extracted text from document"
            };

            // Assert
            Assert.False(string.IsNullOrEmpty(request.FormFirstName));
            Assert.False(string.IsNullOrEmpty(request.FormLastName));
            Assert.False(string.IsNullOrEmpty(request.ExtractedText));
        }

        [Fact]
        public void AzureAIFoundryService_Constructor_InitializesCorrectly()
        {
            // Arrange & Act
            var httpClient = new HttpClient();
            
            // This will test that the constructor doesn't throw exceptions
            var service = new AzureAIFoundryService(httpClient, _mockLogger.Object, _mockConfiguration.Object);

            // Assert
            Assert.NotNull(service);
        }
    }
}