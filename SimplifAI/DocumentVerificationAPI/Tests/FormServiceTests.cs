using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;

namespace DocumentVerificationAPI.Tests
{
    public class FormServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ILogger<FormService>> _mockLogger;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<IPerformanceMonitoringService> _mockPerformanceService;
        private readonly FormService _formService;

        public FormServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<FormService>>();
            _mockCacheService = new Mock<ICacheService>();
            _mockPerformanceService = new Mock<IPerformanceMonitoringService>();
            _formService = new FormService(_context, _mockLogger.Object, _mockCacheService.Object, _mockPerformanceService.Object);
        }

        [Fact]
        public async Task CreateFormAsync_ShouldCreateNewForm()
        {
            // Arrange
            var request = new FormCreationRequest { RecruiterEmail = "recruiter@example.com" };

            // Act
            var form = await _formService.CreateFormAsync(request);

            // Assert
            Assert.NotNull(form);
            Assert.Equal(request.RecruiterEmail, form.RecruiterEmail);
            Assert.Equal("Pending", form.Status);
            Assert.NotNull(form.UniqueUrl);
            Assert.True(form.UniqueUrl.Length > 0);

            var savedForm = await _context.Forms.FindAsync(form.Id);
            Assert.NotNull(savedForm);
        }

        [Fact]
        public async Task GetFormByIdAsync_ExistingForm_ReturnsForm()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            // Act
            var result = await _formService.GetFormByIdAsync(form.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(form.Id, result.Id);
            Assert.Equal(form.RecruiterEmail, result.RecruiterEmail);
        }

        [Fact]
        public async Task GetFormByIdAsync_NonExistentForm_ReturnsNull()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await _formService.GetFormByIdAsync(nonExistentId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetFormByUrlAsync_ExistingUrl_ReturnsForm()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-url",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            // Act
            var result = await _formService.GetFormByUrlAsync("test-form-url");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(form.Id, result.Id);
            Assert.Equal("test-form-url", result.UniqueUrl);
        }

        [Fact]
        public async Task UpdatePersonalInfoAsync_ShouldCreateOrUpdatePersonalInfo()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            var personalInfoRequest = new PersonalInfoRequest
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Phone = "123-456-7890",
                Address = "123 Main St",
                DateOfBirth = new DateTime(1990, 1, 1)
            };

            // Act
            var result = await _formService.UpdatePersonalInfoAsync(form.Id, personalInfoRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John", result.FirstName);
            Assert.Equal("Doe", result.LastName);
            Assert.Equal("john.doe@example.com", result.Email);

            var savedPersonalInfo = await _context.PersonalInfo
                .FirstOrDefaultAsync(p => p.FormId == form.Id);
            Assert.NotNull(savedPersonalInfo);
        }

        [Fact]
        public async Task SubmitFormAsync_ValidForm_ShouldUpdateStatus()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com"
            };

            var document = new Document
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                DocumentType = "Passport",
                FileName = "passport.jpg",
                VerificationStatus = "Verified"
            };

            _context.Forms.Add(form);
            _context.PersonalInfo.Add(personalInfo);
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // Act
            var result = await _formService.SubmitFormAsync(form.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Submitted", result.Status);
            Assert.NotNull(result.SubmittedAt);

            var updatedForm = await _context.Forms.FindAsync(form.Id);
            Assert.Equal("Submitted", updatedForm.Status);
        }

        [Fact]
        public async Task SubmitFormAsync_IncompleteForm_ShouldThrowException()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _formService.SubmitFormAsync(form.Id));
        }

        [Fact]
        public async Task GetFormWithDetailsAsync_ShouldReturnCompleteForm()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com"
            };

            var document = new Document
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                DocumentType = "Passport",
                FileName = "passport.jpg",
                VerificationStatus = "Verified"
            };

            _context.Forms.Add(form);
            _context.PersonalInfo.Add(personalInfo);
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // Act
            var result = await _formService.GetFormWithDetailsAsync(form.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.PersonalInfo);
            Assert.Single(result.Documents);
            Assert.Equal("John", result.PersonalInfo.FirstName);
            Assert.Equal("Passport", result.Documents.First().DocumentType);
        }

        [Fact]
        public async Task ValidateFormCompletionAsync_CompleteForm_ReturnsTrue()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Phone = "123-456-7890"
            };

            var document = new Document
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                DocumentType = "Passport",
                FileName = "passport.jpg",
                VerificationStatus = "Verified"
            };

            _context.Forms.Add(form);
            _context.PersonalInfo.Add(personalInfo);
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // Act
            var result = await _formService.ValidateFormCompletionAsync(form.Id);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.ValidationErrors);
        }

        [Fact]
        public async Task ValidateFormCompletionAsync_IncompleteForm_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            // Act
            var result = await _formService.ValidateFormCompletionAsync(form.Id);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.ValidationErrors);
        }

        [Fact]
        public async Task GetFormsByRecruiterAsync_ShouldReturnRecruiterForms()
        {
            // Arrange
            var recruiterEmail = "recruiter@example.com";
            var form1 = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "form1",
                RecruiterEmail = recruiterEmail,
                Status = "Pending"
            };

            var form2 = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "form2",
                RecruiterEmail = recruiterEmail,
                Status = "Submitted"
            };

            var otherForm = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "other-form",
                RecruiterEmail = "other@example.com",
                Status = "Pending"
            };

            _context.Forms.AddRange(form1, form2, otherForm);
            await _context.SaveChangesAsync();

            // Act
            var result = await _formService.GetFormsByRecruiterAsync(recruiterEmail);

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, f => Assert.Equal(recruiterEmail, f.RecruiterEmail));
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}