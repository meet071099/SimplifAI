using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Xunit;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;
using Microsoft.EntityFrameworkCore;
using DocumentVerificationAPI.Data;

namespace DocumentVerificationAPI.Tests.IntegrationTests
{
    public class EndToEndIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public EndToEndIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task CompleteFormSubmissionWorkflow_ShouldWork()
        {
            // Arrange - Create a new form
            var createFormRequest = new
            {
                RecruiterEmail = "recruiter@test.com"
            };

            var createFormJson = JsonSerializer.Serialize(createFormRequest);
            var createFormContent = new StringContent(createFormJson, Encoding.UTF8, "application/json");

            // Act - Create form
            var createFormResponse = await _client.PostAsync("/api/form/create", createFormContent);
            createFormResponse.EnsureSuccessStatusCode();

            var createFormResult = await createFormResponse.Content.ReadAsStringAsync();
            var formData = JsonSerializer.Deserialize<JsonElement>(createFormResult);
            var formId = formData.GetProperty("formId").GetGuid();
            var uniqueUrl = formData.GetProperty("uniqueUrl").GetString();

            // Act - Get form by URL
            var getFormResponse = await _client.GetAsync($"/api/form/{uniqueUrl}");
            getFormResponse.EnsureSuccessStatusCode();

            // Act - Submit personal info
            var personalInfoRequest = new
            {
                FormId = formId,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@test.com",
                Phone = "+1234567890",
                Address = "123 Test Street, Test City",
                DateOfBirth = "1990-01-01"
            };

            var personalInfoJson = JsonSerializer.Serialize(personalInfoRequest);
            var personalInfoContent = new StringContent(personalInfoJson, Encoding.UTF8, "application/json");

            var personalInfoResponse = await _client.PostAsync("/api/form/personal-info", personalInfoContent);
            personalInfoResponse.EnsureSuccessStatusCode();

            // Act - Upload document (mock file)
            var documentContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Mock PDF content"));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            documentContent.Add(fileContent, "file", "test-passport.pdf");
            documentContent.Add(new StringContent(formId.ToString()), "formId");
            documentContent.Add(new StringContent("Passport"), "documentType");

            var uploadResponse = await _client.PostAsync("/api/document/upload", documentContent);
            uploadResponse.EnsureSuccessStatusCode();

            var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
            var documentData = JsonSerializer.Deserialize<JsonElement>(uploadResult);
            var documentId = documentData.GetProperty("documentId").GetGuid();

            // Act - Submit form
            var submitFormResponse = await _client.PostAsync($"/api/form/{formId}/submit", new StringContent(""));
            submitFormResponse.EnsureSuccessStatusCode();

            // Assert - Verify form was submitted
            var finalFormResponse = await _client.GetAsync($"/api/form/{uniqueUrl}");
            finalFormResponse.EnsureSuccessStatusCode();

            var finalFormResult = await finalFormResponse.Content.ReadAsStringAsync();
            var finalFormData = JsonSerializer.Deserialize<JsonElement>(finalFormResult);
            var status = finalFormData.GetProperty("status").GetString();

            Assert.Equal("Submitted", status);
        }

        [Fact]
        public async Task DatabaseConnection_ShouldWork()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Act & Assert - Test database connection
            var canConnect = await dbContext.Database.CanConnectAsync();
            Assert.True(canConnect, "Database connection should work with Windows Authentication");

            // Test basic database operations
            var formsCount = await dbContext.Forms.CountAsync();
            Assert.True(formsCount >= 0, "Should be able to query Forms table");
        }

        [Fact]
        public async Task FileStorage_ShouldWork()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            var testContent = Encoding.UTF8.GetBytes("Test file content");
            var testStream = new MemoryStream(testContent);
            var fileName = "test-file.txt";

            // Act - Store file
            var storedPath = await fileStorageService.StoreFileAsync(testStream, fileName, "text/plain");
            Assert.NotNull(storedPath);

            // Act - Retrieve file
            var retrievedStream = await fileStorageService.GetFileAsync(storedPath);
            Assert.NotNull(retrievedStream);

            // Assert - Content matches
            var retrievedContent = new byte[testContent.Length];
            await retrievedStream.ReadAsync(retrievedContent, 0, testContent.Length);
            Assert.Equal(testContent, retrievedContent);

            // Cleanup
            await fileStorageService.DeleteFileAsync(storedPath);
        }

        [Fact]
        public async Task EmailService_ShouldWork()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var testEmail = new EmailMessage
            {
                To = "test@example.com",
                Subject = "Integration Test Email",
                Body = "This is a test email from integration tests.",
                IsHtml = false
            };

            // Act & Assert - Should not throw exception
            await emailService.SendEmailAsync(testEmail);
            
            // Note: In a real test environment, you might want to verify the email was queued
            // or sent by checking the email queue or using a test email service
        }

        [Fact]
        public async Task PerformanceMonitoring_ShouldWork()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var performanceService = scope.ServiceProvider.GetRequiredService<IPerformanceMonitoringService>();

            // Act - Test performance monitoring
            performanceService.StartOperation("test-operation");
            await Task.Delay(100); // Simulate some work
            performanceService.EndOperation("test-operation");

            // Act - Get metrics
            var metrics = performanceService.GetMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.ContainsKey("test-operation"));
        }

        [Fact]
        public async Task SecurityValidation_ShouldWork()
        {
            // Test file upload security
            var maliciousContent = new MultipartFormDataContent();
            var maliciousFile = new ByteArrayContent(Encoding.UTF8.GetBytes("<?php echo 'malicious'; ?>"));
            maliciousFile.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/php");
            maliciousContent.Add(maliciousFile, "file", "malicious.php");
            maliciousContent.Add(new StringContent(Guid.NewGuid().ToString()), "formId");
            maliciousContent.Add(new StringContent("Passport"), "documentType");

            // Act - Should reject malicious file
            var response = await _client.PostAsync("/api/document/upload", maliciousContent);
            
            // Assert - Should return bad request
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ApiEndpoints_ShouldReturnCorrectStatusCodes()
        {
            // Test health check endpoint
            var healthResponse = await _client.GetAsync("/api/monitoring/health");
            healthResponse.EnsureSuccessStatusCode();

            // Test metrics endpoint
            var metricsResponse = await _client.GetAsync("/api/monitoring/metrics");
            metricsResponse.EnsureSuccessStatusCode();

            // Test invalid form URL
            var invalidFormResponse = await _client.GetAsync("/api/form/invalid-url");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, invalidFormResponse.StatusCode);
        }
    }
}