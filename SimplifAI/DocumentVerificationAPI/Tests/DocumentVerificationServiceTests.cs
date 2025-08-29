using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Azure.AI.DocumentIntelligence;
using Azure.Core;
using DocumentVerificationAPI.Services;
using DocumentVerificationAPI.Models.DTOs;

namespace DocumentVerificationAPI.Tests
{
    public class DocumentVerificationServiceTests
    {
        private readonly Mock<ILogger<AzureDocumentIntelligenceService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly AzureDocumentIntelligenceService _service;

        public DocumentVerificationServiceTests()
        {
            _mockLogger = new Mock<ILogger<AzureDocumentIntelligenceService>>();
            _mockConfiguration = new Mock<IConfiguration>();

            // Setup configuration
            _mockConfiguration.Setup(c => c["AzureDocumentIntelligence:Endpoint"])
                .Returns("https://test.cognitiveservices.azure.com/");
            _mockConfiguration.Setup(c => c["AzureDocumentIntelligence:ApiKey"])
                .Returns("test-api-key");

            _service = new AzureDocumentIntelligenceService(_mockLogger.Object, _mockConfiguration.Object);
        }

        [Fact]
        public void DetermineVerificationStatus_HighConfidence_ReturnsGreen()
        {
            // Arrange
            var confidenceScore = 95.0m;
            var isBlurred = false;
            var isCorrectType = true;

            // Act
            var (status, color, message) = _service.DetermineVerificationStatus(confidenceScore, isBlurred, isCorrectType);

            // Assert
            Assert.Equal("verified", status);
            Assert.Equal("green", color);
            Assert.Contains("verified successfully", message.ToLower());
        }

        [Fact]
        public void DetermineVerificationStatus_MediumConfidence_ReturnsYellow()
        {
            // Arrange
            var confidenceScore = 75.0m;
            var isBlurred = false;
            var isCorrectType = true;

            // Act
            var (status, color, message) = _service.DetermineVerificationStatus(confidenceScore, isBlurred, isCorrectType);

            // Assert
            Assert.Equal("review_required", status);
            Assert.Equal("yellow", color);
            Assert.Contains("review", message.ToLower());
        }

        [Fact]
        public void DetermineVerificationStatus_LowConfidence_ReturnsRed()
        {
            // Arrange
            var confidenceScore = 30.0m;
            var isBlurred = false;
            var isCorrectType = true;

            // Act
            var (status, color, message) = _service.DetermineVerificationStatus(confidenceScore, isBlurred, isCorrectType);

            // Assert
            Assert.Equal("failed", status);
            Assert.Equal("red", color);
            Assert.Contains("low confidence", message.ToLower());
        }

        [Fact]
        public void DetermineVerificationStatus_BlurredDocument_ReturnsRed()
        {
            // Arrange
            var confidenceScore = 95.0m;
            var isBlurred = true;
            var isCorrectType = true;

            // Act
            var (status, color, message) = _service.DetermineVerificationStatus(confidenceScore, isBlurred, isCorrectType);

            // Assert
            Assert.Equal("failed", status);
            Assert.Equal("red", color);
            Assert.Contains("blurred", message.ToLower());
        }

        [Fact]
        public void DetermineVerificationStatus_WrongDocumentType_ReturnsRed()
        {
            // Arrange
            var confidenceScore = 95.0m;
            var isBlurred = false;
            var isCorrectType = false;

            // Act
            var (status, color, message) = _service.DetermineVerificationStatus(confidenceScore, isBlurred, isCorrectType);

            // Assert
            Assert.Equal("failed", status);
            Assert.Equal("red", color);
            Assert.Contains("document type", message.ToLower());
        }

        [Fact]
        public void IsMatchingDocumentType_PassportDetected_ReturnsTrue()
        {
            // Arrange
            var detectedType = "passport";
            var expectedType = "Passport";

            // Act
            var result = _service.IsMatchingDocumentType(detectedType, expectedType);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsMatchingDocumentType_DriverLicenseDetected_ReturnsTrue()
        {
            // Arrange
            var detectedType = "driver license";
            var expectedType = "DriverLicense";

            // Act
            var result = _service.IsMatchingDocumentType(detectedType, expectedType);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsMatchingDocumentType_MismatchedTypes_ReturnsFalse()
        {
            // Arrange
            var detectedType = "passport";
            var expectedType = "DriverLicense";

            // Act
            var result = _service.IsMatchingDocumentType(detectedType, expectedType);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CheckImageQuality_HighQualityImage_ReturnsFalse()
        {
            // This would require mocking Azure DI response
            // For now, test the logic with mock data
            var mockAnalysisResult = CreateMockAnalysisResult(confidence: 0.95f);

            // Act
            var isBlurred = _service.CheckImageQuality(mockAnalysisResult);

            // Assert
            Assert.False(isBlurred);
        }

        [Fact]
        public void CheckImageQuality_LowQualityImage_ReturnsTrue()
        {
            // This would require mocking Azure DI response
            var mockAnalysisResult = CreateMockAnalysisResult(confidence: 0.3f);

            // Act
            var isBlurred = _service.CheckImageQuality(mockAnalysisResult);

            // Assert
            Assert.True(isBlurred);
        }

        [Fact]
        public void CalculateOverallConfidence_HighConfidenceFields_ReturnsHighScore()
        {
            // This would require mocking Azure DI response with high confidence fields
            var mockAnalysisResult = CreateMockAnalysisResult(confidence: 0.95f);

            // Act
            var confidence = _service.CalculateOverallConfidence(mockAnalysisResult);

            // Assert
            Assert.True(confidence >= 85);
        }

        [Fact]
        public void CalculateOverallConfidence_LowConfidenceFields_ReturnsLowScore()
        {
            // This would require mocking Azure DI response with low confidence fields
            var mockAnalysisResult = CreateMockAnalysisResult(confidence: 0.3f);

            // Act
            var confidence = _service.CalculateOverallConfidence(mockAnalysisResult);

            // Assert
            Assert.True(confidence < 50);
        }

        [Theory]
        [InlineData("Passport", "passport")]
        [InlineData("DriverLicense", "driver license")]
        [InlineData("NationalId", "national id")]
        [InlineData("SocialSecurityCard", "social security")]
        public void GetDocumentTypeKeywords_ValidType_ReturnsCorrectKeywords(string documentType, string expectedKeyword)
        {
            // Act
            var keywords = _service.GetDocumentTypeKeywords(documentType);

            // Assert
            Assert.Contains(expectedKeyword, keywords);
        }

        [Fact]
        public void GetDocumentTypeKeywords_UnknownType_ReturnsEmptyList()
        {
            // Act
            var keywords = _service.GetDocumentTypeKeywords("UnknownType");

            // Assert
            Assert.Empty(keywords);
        }

        private dynamic CreateMockAnalysisResult(float confidence)
        {
            // This is a simplified mock - in real tests you'd create proper Azure DI response objects
            return new
            {
                Documents = new[]
                {
                    new
                    {
                        Fields = new Dictionary<string, object>
                        {
                            ["DocumentType"] = new { Content = "passport", Confidence = confidence },
                            ["FirstName"] = new { Content = "John", Confidence = confidence },
                            ["LastName"] = new { Content = "Doe", Confidence = confidence }
                        }
                    }
                },
                Pages = new[]
                {
                    new
                    {
                        Lines = new[]
                        {
                            new { Content = "PASSPORT", Confidence = confidence }
                        }
                    }
                }
            };
        }
    }
}