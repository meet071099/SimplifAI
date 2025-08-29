using Xunit;
using DocumentVerificationAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Tests
{
    public class DocumentAuthenticityRequestTests
    {
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
        public void DocumentAuthenticityRequest_ValidationAttributes_AreApplied()
        {
            // Arrange
            var request = new DocumentAuthenticityRequest
            {
                FormFirstName = "",
                FormLastName = "",
                ExtractedText = ""
            };

            // Act
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);

            // Assert
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains("FormFirstName"));
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains("FormLastName"));
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains("ExtractedText"));
        }

        [Fact]
        public void DocumentAuthenticityRequest_OptionalFields_CanBeNull()
        {
            // Arrange & Act
            var request = new DocumentAuthenticityRequest
            {
                FormFirstName = "Test",
                FormLastName = "User",
                ExtractedText = "Test document text",
                DocumentType = null,
                FormId = null
            };

            // Assert
            Assert.Null(request.DocumentType);
            Assert.Null(request.FormId);
            
            // Validate that required fields still work
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(request, validationContext, validationResults, true);
            Assert.True(isValid);
        }
    }
}