using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DocumentVerificationAPI.Controllers;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;
using System.Text;

namespace DocumentVerificationAPI.Tests
{
    public class DocumentControllerTests
    {
        private readonly Mock<IFormService> _mockFormService;
        private readonly Mock<IFileStorageService> _mockFileStorageService;
        private readonly Mock<IDocumentVerificationService> _mockDocumentService;
        private readonly Mock<IAsyncDocumentVerificationService> _mockAsyncDocumentService;
        private readonly Mock<ISecurityService> _mockSecurityService;
        private readonly Mock<IPerformanceMonitoringService> _mockPerformanceService;
        private readonly Mock<ILogger<DocumentController>> _mockLogger;
        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _mockFormService = new Mock<IFormService>();
            _mockFileStorageService = new Mock<IFileStorageService>();
            _mockDocumentService = new Mock<IDocumentVerificationService>();
            _mockAsyncDocumentService = new Mock<IAsyncDocumentVerificationService>();
            _mockSecurityService = new Mock<ISecurityService>();
            _mockPerformanceService = new Mock<IPerformanceMonitoringService>();
            _mockLogger = new Mock<ILogger<DocumentController>>();
            
            _controller = new DocumentController(
                _mockFormService.Object,
                _mockFileStorageService.Object,
                _mockDocumentService.Object,
                _mockAsyncDocumentService.Object,
                _mockSecurityService.Object,
                _mockPerformanceService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task UploadDocument_ValidFile_ReturnsOkResult()
        {
            // Arrange
            var formId = Guid.NewGuid();
            var documentType = "Passport";
            var fileName = "passport.jpg";
            var fileContent = "fake file content";
            var fileBytes = Encoding.UTF8.GetBytes(fileContent);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(fileBytes.Length);
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(fileBytes));

            var expectedResult = new DocumentVerificationResult
            {
                DocumentId = Guid.NewGuid(),
                VerificationStatus = "verified",
                ConfidenceScore = 95.5m,
                IsBlurred = false,
                IsCorrectType = true,
                StatusColor = "green",
                Message = "Document verified successfully"
            };

            _mockFileStorageService.Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("saved-file-path");

            _mockDocumentService.Setup(s => s.VerifyDocumentAsync(It.IsAny<Stream>(), documentType, fileName))
                .ReturnsAsync(expectedResult);

            var request = new DocumentUploadRequest
            {
                File = mockFile.Object,
                DocumentType = documentType,
                FormId = formId
            };

            // Act
            var result = await _controller.UploadDocument(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<DocumentVerificationResult>(okResult.Value);
            Assert.Equal(expectedResult.VerificationStatus, response.VerificationStatus);
            Assert.Equal(expectedResult.ConfidenceScore, response.ConfidenceScore);
        }

        [Fact]
        public async Task UploadDocument_NullFile_ReturnsBadRequest()
        {
            // Arrange
            var request = new DocumentUploadRequest
            {
                File = null,
                DocumentType = "Passport",
                FormId = Guid.NewGuid()
            };

            // Act
            var result = await _controller.UploadDocument(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("No file provided", badRequestResult.Value.ToString());
        }

        [Fact]
        public async Task UploadDocument_EmptyFile_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(0);
            mockFile.Setup(f => f.FileName).Returns("empty.jpg");

            var request = new DocumentUploadRequest
            {
                File = mockFile.Object,
                DocumentType = "Passport",
                FormId = Guid.NewGuid()
            };

            // Act
            var result = await _controller.UploadDocument(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Empty file", badRequestResult.Value.ToString());
        }

        [Fact]
        public async Task UploadDocument_InvalidFileType_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1000);
            mockFile.Setup(f => f.FileName).Returns("document.txt");
            mockFile.Setup(f => f.ContentType).Returns("text/plain");

            var request = new DocumentUploadRequest
            {
                File = mockFile.Object,
                DocumentType = "Passport",
                FormId = Guid.NewGuid()
            };

            // Act
            var result = await _controller.UploadDocument(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Invalid file type", badRequestResult.Value.ToString());
        }

        [Fact]
        public async Task UploadDocument_FileTooLarge_ReturnsBadRequest()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11MB
            mockFile.Setup(f => f.FileName).Returns("large.jpg");
            mockFile.Setup(f => f.ContentType).Returns("image/jpeg");

            var request = new DocumentUploadRequest
            {
                File = mockFile.Object,
                DocumentType = "Passport",
                FormId = Guid.NewGuid()
            };

            // Act
            var result = await _controller.UploadDocument(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("File size exceeds", badRequestResult.Value.ToString());
        }

        [Fact]
        public async Task GetVerificationStatus_ExistingDocument_ReturnsOkResult()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var expectedResult = new DocumentVerificationResult
            {
                DocumentId = documentId,
                VerificationStatus = "verified",
                ConfidenceScore = 95.5m,
                StatusColor = "green"
            };

            _mockDocumentService.Setup(s => s.GetVerificationStatusAsync(documentId))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GetVerificationStatus(documentId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<DocumentVerificationResult>(okResult.Value);
            Assert.Equal(documentId, response.DocumentId);
        }

        [Fact]
        public async Task GetVerificationStatus_NonExistentDocument_ReturnsNotFound()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            _mockDocumentService.Setup(s => s.GetVerificationStatusAsync(documentId))
                .ReturnsAsync((DocumentVerificationResult)null);

            // Act
            var result = await _controller.GetVerificationStatus(documentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ConfirmDocument_ExistingDocument_ReturnsOkResult()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var expectedResult = new DocumentVerificationResult
            {
                DocumentId = documentId,
                VerificationStatus = "verified",
                StatusColor = "green"
            };

            _mockDocumentService.Setup(s => s.ConfirmDocumentAsync(documentId))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ConfirmDocument(documentId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<DocumentVerificationResult>(okResult.Value);
            Assert.Equal("verified", response.VerificationStatus);
        }

        [Fact]
        public async Task DeleteDocument_ExistingDocument_ReturnsNoContent()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            _mockDocumentService.Setup(s => s.DeleteDocumentAsync(documentId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteDocument(documentId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteDocument_NonExistentDocument_ReturnsNotFound()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            _mockDocumentService.Setup(s => s.DeleteDocumentAsync(documentId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteDocument(documentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RetryVerification_ExistingDocument_ReturnsOkResult()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            var expectedResult = new DocumentVerificationResult
            {
                DocumentId = documentId,
                VerificationStatus = "pending",
                StatusColor = "yellow"
            };

            _mockDocumentService.Setup(s => s.RetryVerificationAsync(documentId))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.RetryVerification(documentId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<DocumentVerificationResult>(okResult.Value);
            Assert.Equal("pending", response.VerificationStatus);
        }

        [Theory]
        [InlineData("image/jpeg")]
        [InlineData("image/png")]
        [InlineData("application/pdf")]
        public void IsValidFileType_ValidTypes_ReturnsTrue(string contentType)
        {
            // Act
            var result = _controller.IsValidFileType(contentType);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("text/plain")]
        [InlineData("application/exe")]
        [InlineData("video/mp4")]
        public void IsValidFileType_InvalidTypes_ReturnsFalse(string contentType)
        {
            // Act
            var result = _controller.IsValidFileType(contentType);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetMaxFileSizeInBytes_ReturnsCorrectSize()
        {
            // Act
            var maxSize = _controller.GetMaxFileSizeInBytes();

            // Assert
            Assert.Equal(10 * 1024 * 1024, maxSize); // 10MB
        }
    }
}