using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;

namespace DocumentVerificationAPI.Tests.IntegrationTests
{
    public class FormControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public FormControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the app's ApplicationDbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add ApplicationDbContext using an in-memory database for testing
                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("InMemoryDbForTesting");
                    });
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task CreateForm_ShouldReturnCreatedForm()
        {
            // Arrange
            var request = new CreateFormRequest
            {
                RecruiterEmail = "recruiter@example.com"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/forms", request);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var form = JsonSerializer.Deserialize<FormResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(form);
            Assert.Equal("recruiter@example.com", form.RecruiterEmail);
            Assert.Equal("Pending", form.Status);
            Assert.NotNull(form.UniqueUrl);
        }

        [Fact]
        public async Task GetForm_ExistingForm_ShouldReturnForm()
        {
            // Arrange - Create a form first
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-url",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/forms/{form.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var formResponse = JsonSerializer.Deserialize<FormResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(formResponse);
            Assert.Equal(form.Id, formResponse.Id);
            Assert.Equal(form.RecruiterEmail, formResponse.RecruiterEmail);
        }

        [Fact]
        public async Task GetForm_NonExistentForm_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var response = await _client.GetAsync($"/api/forms/{nonExistentId}");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdatePersonalInfo_ValidData_ShouldReturnUpdatedInfo()
        {
            // Arrange - Create a form first
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

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
            var response = await _client.PutAsJsonAsync($"/api/forms/{form.Id}/personal-info", personalInfoRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var personalInfoResponse = JsonSerializer.Deserialize<PersonalInfoResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(personalInfoResponse);
            Assert.Equal("John", personalInfoResponse.FirstName);
            Assert.Equal("Doe", personalInfoResponse.LastName);
            Assert.Equal("john.doe@example.com", personalInfoResponse.Email);
        }

        [Fact]
        public async Task SubmitForm_CompleteForm_ShouldReturnSubmittedForm()
        {
            // Arrange - Create a complete form
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
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
                Phone = "123-456-7890",
                Address = "123 Main St"
            };

            var document = new Document
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                DocumentType = "Passport",
                FileName = "passport.jpg",
                FilePath = "/uploads/passport.jpg",
                FileSize = 1024,
                ContentType = "image/jpeg",
                VerificationStatus = "Verified",
                ConfidenceScore = 95.5m,
                StatusColor = "Green"
            };

            context.Forms.Add(form);
            context.PersonalInfo.Add(personalInfo);
            context.Documents.Add(document);
            await context.SaveChangesAsync();

            // Act
            var response = await _client.PostAsync($"/api/forms/{form.Id}/submit", null);

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var submissionResponse = JsonSerializer.Deserialize<FormSubmissionResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(submissionResponse);
            Assert.Equal("Submitted", submissionResponse.Status);
            Assert.NotNull(submissionResponse.SubmittedAt);
        }

        [Fact]
        public async Task SubmitForm_IncompleteForm_ShouldReturnBadRequest()
        {
            // Arrange - Create an incomplete form (no personal info or documents)
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "incomplete-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

            // Act
            var response = await _client.PostAsync($"/api/forms/{form.Id}/submit", null);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetFormByUrl_ExistingUrl_ShouldReturnForm()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "unique-test-url",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/forms/url/unique-test-url");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var formResponse = JsonSerializer.Deserialize<FormResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(formResponse);
            Assert.Equal("unique-test-url", formResponse.UniqueUrl);
        }

        [Fact]
        public async Task ValidatePersonalInfo_InvalidEmail_ShouldReturnBadRequest()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Pending"
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

            var invalidPersonalInfo = new PersonalInfoRequest
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "invalid-email", // Invalid email format
                Phone = "123-456-7890",
                Address = "123 Main St"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/forms/{form.Id}/personal-info", invalidPersonalInfo);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetFormProgress_ShouldReturnProgressInfo()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "progress-form",
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

            context.Forms.Add(form);
            context.PersonalInfo.Add(personalInfo);
            await context.SaveChangesAsync();

            // Act
            var response = await _client.GetAsync($"/api/forms/{form.Id}/progress");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var progressResponse = JsonSerializer.Deserialize<FormProgressResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(progressResponse);
            Assert.True(progressResponse.PersonalInfoComplete);
            Assert.False(progressResponse.DocumentsComplete); // No documents added
        }
    }
}