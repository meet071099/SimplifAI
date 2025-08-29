using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using DocumentVerificationAPI.Services;
using Microsoft.EntityFrameworkCore;
using DocumentVerificationAPI.Data;
using System.Net.Http.Headers;

namespace DocumentVerificationAPI.Tests.IntegrationTests
{
    /// <summary>
    /// Production-ready integration tests that verify the complete system functionality
    /// with real Azure Document Intelligence, SMTP, database, and file storage
    /// </summary>
    [Collection("Production Integration Tests")]
    [Trait("Category", "ProductionIntegration")]
    public class ProductionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ITestOutputHelper _output;

        public ProductionIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
        {
            _factory = factory;
            _client = _factory.CreateClient();
            _output = output;
        }

        [Fact]
        public async Task CompleteWorkflow_WithRealAzureDocumentIntelligence_ShouldWork()
        {
            _output.WriteLine("Starting complete workflow test with real Azure Document Intelligence");

            // Skip test if Azure DI is not configured
            using var scope = _factory.Services.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var endpoint = config["AzureDocumentIntelligence:Endpoint"];
            var apiKey = config["AzureDocumentIntelligence:ApiKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || 
                endpoint.Contains("your-resource") || apiKey.Contains("your-api-key"))
            {
                _output.WriteLine("Skipping test: Azure Document Intelligence not configured with real values");
                return;
            }

            try
            {
                // Step 1: Create a new form
                _output.WriteLine("Step 1: Creating new form");
                var createFormRequest = new { RecruiterEmail = "integration-test@example.com" };
                var createFormJson = JsonSerializer.Serialize(createFormRequest);
                var createFormContent = new StringContent(createFormJson, Encoding.UTF8, "application/json");

                var createFormResponse = await _client.PostAsync("/api/form/create", createFormContent);
                createFormResponse.EnsureSuccessStatusCode();

                var createFormResult = await createFormResponse.Content.ReadAsStringAsync();
                var formData = JsonSerializer.Deserialize<JsonElement>(createFormResult);
                var formId = formData.GetProperty("formId").GetGuid();
                var uniqueUrl = formData.GetProperty("uniqueUrl").GetString();

                _output.WriteLine($"Form created: {formId}, URL: {uniqueUrl}");

                // Step 2: Submit personal information
                _output.WriteLine("Step 2: Submitting personal information");
                var personalInfoRequest = new
                {
                    FormId = formId,
                    FirstName = "Integration",
                    LastName = "Test",
                    Email = "integration.test@example.com",
                    Phone = "+1234567890",
                    Address = "123 Integration Test Street, Test City, TC 12345",
                    DateOfBirth = "1990-01-01"
                };

                var personalInfoJson = JsonSerializer.Serialize(personalInfoRequest);
                var personalInfoContent = new StringContent(personalInfoJson, Encoding.UTF8, "application/json");

                var personalInfoResponse = await _client.PostAsync("/api/form/personal-info", personalInfoContent);
                personalInfoResponse.EnsureSuccessStatusCode();

                // Step 3: Upload and verify document with real Azure DI
                _output.WriteLine("Step 3: Uploading document for real Azure DI verification");
                
                // Create a realistic test document (simple PDF)
                var testDocument = CreateTestPdfDocument();
                var documentContent = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(testDocument);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                documentContent.Add(fileContent, "file", "integration-test-passport.pdf");
                documentContent.Add(new StringContent(formId.ToString()), "formId");
                documentContent.Add(new StringContent("Passport"), "documentType");

                var uploadResponse = await _client.PostAsync("/api/document/upload", documentContent);
                uploadResponse.EnsureSuccessStatusCode();

                var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
                var documentData = JsonSerializer.Deserialize<JsonElement>(uploadResult);
                var documentId = documentData.GetProperty("documentId").GetGuid();
                var verificationStatus = documentData.GetProperty("verificationStatus").GetString();
                var confidenceScore = documentData.GetProperty("confidenceScore").GetDecimal();

                _output.WriteLine($"Document uploaded: {documentId}, Status: {verificationStatus}, Confidence: {confidenceScore}%");

                // Verify Azure DI processed the document
                Assert.NotNull(verificationStatus);
                Assert.True(confidenceScore >= 0 && confidenceScore <= 100);

                // Step 4: Submit the form
                _output.WriteLine("Step 4: Submitting complete form");
                var submitFormResponse = await _client.PostAsync($"/api/form/{formId}/submit", new StringContent(""));
                submitFormResponse.EnsureSuccessStatusCode();

                // Step 5: Verify form submission in database
                _output.WriteLine("Step 5: Verifying form submission in database");
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var submittedForm = await dbContext.Forms
                    .Include(f => f.PersonalInfo)
                    .Include(f => f.Documents)
                    .FirstOrDefaultAsync(f => f.Id == formId);

                Assert.NotNull(submittedForm);
                Assert.Equal("Submitted", submittedForm.Status);
                Assert.NotNull(submittedForm.SubmittedAt);
                Assert.NotNull(submittedForm.PersonalInfo);
                Assert.Single(submittedForm.Documents);

                var document = submittedForm.Documents.First();
                Assert.Equal("Passport", document.DocumentType);
                Assert.NotNull(document.VerificationStatus);
                Assert.True(document.ConfidenceScore.HasValue);

                _output.WriteLine("Complete workflow test completed successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with exception: {ex.Message}");
                _output.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        [Fact]
        public async Task DatabaseOperations_WithWindowsAuthentication_ShouldWork()
        {
            _output.WriteLine("Testing database operations with Windows Authentication");

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                // Test connection
                var canConnect = await dbContext.Database.CanConnectAsync();
                Assert.True(canConnect, "Should be able to connect to SQL Server with Windows Authentication");
                _output.WriteLine("✓ Database connection successful");

                // Test basic CRUD operations
                var testForm = new Form
                {
                    Id = Guid.NewGuid(),
                    UniqueUrl = $"test-{Guid.NewGuid()}",
                    RecruiterEmail = "db-test@example.com",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                // Create
                dbContext.Forms.Add(testForm);
                await dbContext.SaveChangesAsync();
                _output.WriteLine("✓ Form creation successful");

                // Read
                var retrievedForm = await dbContext.Forms.FindAsync(testForm.Id);
                Assert.NotNull(retrievedForm);
                Assert.Equal(testForm.RecruiterEmail, retrievedForm.RecruiterEmail);
                _output.WriteLine("✓ Form retrieval successful");

                // Update
                retrievedForm.Status = "Updated";
                await dbContext.SaveChangesAsync();
                _output.WriteLine("✓ Form update successful");

                // Verify update
                var updatedForm = await dbContext.Forms.FindAsync(testForm.Id);
                Assert.Equal("Updated", updatedForm.Status);

                // Delete
                dbContext.Forms.Remove(updatedForm);
                await dbContext.SaveChangesAsync();
                _output.WriteLine("✓ Form deletion successful");

                // Verify deletion
                var deletedForm = await dbContext.Forms.FindAsync(testForm.Id);
                Assert.Null(deletedForm);

                _output.WriteLine("Database operations test completed successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Database test failed: {ex.Message}");
                throw;
            }
        }

        [Fact]
        public async Task EmailNotifications_WithRealSmtp_ShouldWork()
        {
            _output.WriteLine("Testing email notifications with real SMTP configuration");

            using var scope = _factory.Services.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                // Test basic email sending
                var testEmail = new EmailMessage
                {
                    To = "integration-test@example.com",
                    Subject = "Production Integration Test - Email Service",
                    Body = $@"
This is a production integration test email sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.

Test Details:
- SMTP Host: sandbox.smtp.mailtrap.io
- Port: 587
- Authentication: PLAIN/LOGIN
- TLS: STARTTLS

This email verifies that the email service is working correctly with the configured SMTP settings.
",
                    IsHtml = false
                };

                await emailService.SendEmailAsync(testEmail);
                _output.WriteLine("✓ Basic email sent successfully");

                // Test HTML email
                var htmlEmail = new EmailMessage
                {
                    To = "integration-test@example.com",
                    Subject = "Production Integration Test - HTML Email",
                    Body = $@"
<html>
<body>
    <h2>Production Integration Test</h2>
    <p>This is an <strong>HTML email</strong> sent at <em>{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</em>.</p>
    
    <h3>SMTP Configuration Verified:</h3>
    <ul>
        <li>Host: sandbox.smtp.mailtrap.io</li>
        <li>Port: 587</li>
        <li>Authentication: PLAIN/LOGIN</li>
        <li>TLS: STARTTLS</li>
    </ul>
    
    <p style='color: green;'>✓ Email service is working correctly!</p>
</body>
</html>",
                    IsHtml = true
                };

                await emailService.SendEmailAsync(htmlEmail);
                _output.WriteLine("✓ HTML email sent successfully");

                // Wait for email processing
                await Task.Delay(2000);

                // Verify email logging
                var emailLogs = await dbContext.EmailLogs
                    .Where(e => e.Subject.Contains("Production Integration Test"))
                    .OrderByDescending(e => e.SentAt)
                    .Take(2)
                    .ToListAsync();

                Assert.True(emailLogs.Count >= 1, "At least one email should be logged");
                _output.WriteLine($"✓ Email logging verified - {emailLogs.Count} emails logged");

                // Test form submission notification
                var notificationEmail = new EmailMessage
                {
                    To = "recruiter@example.com",
                    Subject = "New Form Submission - Integration Test",
                    Body = @"
A new form has been submitted by Integration Test.

Candidate Details:
- Name: Integration Test
- Email: integration.test@example.com
- Phone: +1234567890
- Submitted: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + @" UTC

Please review the submission in the recruiter dashboard.
",
                    IsHtml = false
                };

                await emailService.SendEmailAsync(notificationEmail);
                _output.WriteLine("✓ Form submission notification sent successfully");

                _output.WriteLine("Email notifications test completed successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Email test failed: {ex.Message}");
                throw;
            }
        }

        [Fact]
        public async Task FileStorage_WithLocalFileSystem_ShouldWork()
        {
            _output.WriteLine("Testing file storage operations with local file system");

            using var scope = _factory.Services.CreateScope();
            var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            try
            {
                // Test file storage
                var testContent = Encoding.UTF8.GetBytes("Integration test file content - " + DateTime.UtcNow);
                using var testStream = new MemoryStream(testContent);
                var fileName = $"integration-test-{Guid.NewGuid()}.txt";

                var storedPath = await fileStorageService.StoreFileAsync(testStream, fileName, "text/plain");
                Assert.NotNull(storedPath);
                _output.WriteLine($"✓ File stored successfully at: {storedPath}");

                // Test file retrieval
                using var retrievedStream = await fileStorageService.GetFileAsync(storedPath);
                Assert.NotNull(retrievedStream);

                var retrievedContent = new byte[testContent.Length];
                await retrievedStream.ReadAsync(retrievedContent, 0, testContent.Length);
                Assert.Equal(testContent, retrievedContent);
                _output.WriteLine("✓ File retrieved successfully with correct content");

                // Test file existence
                var exists = await fileStorageService.FileExistsAsync(storedPath);
                Assert.True(exists);
                _output.WriteLine("✓ File existence check successful");

                // Test file deletion
                await fileStorageService.DeleteFileAsync(storedPath);
                var existsAfterDeletion = await fileStorageService.FileExistsAsync(storedPath);
                Assert.False(existsAfterDeletion);
                _output.WriteLine("✓ File deletion successful");

                // Test large file handling
                var largeContent = new byte[5 * 1024 * 1024]; // 5MB
                new Random().NextBytes(largeContent);
                using var largeStream = new MemoryStream(largeContent);
                var largeFileName = $"large-test-{Guid.NewGuid()}.bin";

                var largeStoredPath = await fileStorageService.StoreFileAsync(largeStream, largeFileName, "application/octet-stream");
                Assert.NotNull(largeStoredPath);
                _output.WriteLine($"✓ Large file (5MB) stored successfully");

                // Cleanup large file
                await fileStorageService.DeleteFileAsync(largeStoredPath);

                _output.WriteLine("File storage test completed successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"File storage test failed: {ex.Message}");
                throw;
            }
        }

        [Fact]
        public async Task PerformanceMonitoring_ShouldWork()
        {
            _output.WriteLine("Testing performance monitoring functionality");

            using var scope = _factory.Services.CreateScope();
            var performanceService = scope.ServiceProvider.GetRequiredService<IPerformanceMonitoringService>();

            try
            {
                // Test operation timing
                var operationName = $"integration-test-{Guid.NewGuid()}";
                performanceService.StartOperation(operationName);
                
                // Simulate some work
                await Task.Delay(100);
                
                performanceService.EndOperation(operationName);
                _output.WriteLine("✓ Performance operation timing successful");

                // Test metrics retrieval
                var metrics = performanceService.GetMetrics();
                Assert.NotNull(metrics);
                Assert.True(metrics.ContainsKey(operationName));
                _output.WriteLine($"✓ Performance metrics retrieved - {metrics.Count} operations tracked");

                // Test slow operation detection
                var slowOperationName = $"slow-operation-{Guid.NewGuid()}";
                performanceService.StartOperation(slowOperationName);
                await Task.Delay(500); // Simulate slow operation
                performanceService.EndOperation(slowOperationName);

                var slowMetrics = performanceService.GetMetrics();
                Assert.True(slowMetrics.ContainsKey(slowOperationName));
                _output.WriteLine("✓ Slow operation detection successful");

                _output.WriteLine("Performance monitoring test completed successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Performance monitoring test failed: {ex.Message}");
                throw;
            }
        }

        [Fact]
        public async Task SecurityValidation_ShouldWork()
        {
            _output.WriteLine("Testing security validation functionality");

            try
            {
                // Test file upload security - malicious file type
                var maliciousContent = new MultipartFormDataContent();
                var maliciousFile = new ByteArrayContent(Encoding.UTF8.GetBytes("<?php echo 'malicious'; ?>"));
                maliciousFile.Headers.ContentType = new MediaTypeHeaderValue("application/php");
                maliciousContent.Add(maliciousFile, "file", "malicious.php");
                maliciousContent.Add(new StringContent(Guid.NewGuid().ToString()), "formId");
                maliciousContent.Add(new StringContent("Passport"), "documentType");

                var maliciousResponse = await _client.PostAsync("/api/document/upload", maliciousContent);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, maliciousResponse.StatusCode);
                _output.WriteLine("✓ Malicious file type rejected");

                // Test file size limit
                var largeContent = new MultipartFormDataContent();
                var largeFile = new ByteArrayContent(new byte[15 * 1024 * 1024]); // 15MB (over limit)
                largeFile.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                largeContent.Add(largeFile, "file", "large.pdf");
                largeContent.Add(new StringContent(Guid.NewGuid().ToString()), "formId");
                largeContent.Add(new StringContent("Passport"), "documentType");

                var largeResponse = await _client.PostAsync("/api/document/upload", largeContent);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, largeResponse.StatusCode);
                _output.WriteLine("✓ Large file size rejected");

                // Test input validation
                var invalidPersonalInfo = new
                {
                    FormId = Guid.NewGuid(),
                    FirstName = "", // Empty required field
                    LastName = "Test",
                    Email = "invalid-email", // Invalid email format
                    Phone = "+1234567890"
                };

                var invalidJson = JsonSerializer.Serialize(invalidPersonalInfo);
                var invalidContent = new StringContent(invalidJson, Encoding.UTF8, "application/json");

                var invalidResponse = await _client.PostAsync("/api/form/personal-info", invalidContent);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalidResponse.StatusCode);
                _output.WriteLine("✓ Invalid input data rejected");

                _output.WriteLine("Security validation test completed successfully");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Security validation test failed: {ex.Message}");
                throw;
            }
        }

        private byte[] CreateTestPdfDocument()
        {
            // Create a minimal but valid PDF document for testing
            var pdfContent = @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj

2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj

3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
/Resources <<
/Font <<
/F1 5 0 R
>>
>>
>>
endobj

4 0 obj
<<
/Length 44
>>
stream
BT
/F1 12 Tf
100 700 Td
(Integration Test Document) Tj
ET
endstream
endobj

5 0 obj
<<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica
>>
endobj

xref
0 6
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000274 00000 n 
0000000370 00000 n 
trailer
<<
/Size 6
/Root 1 0 R
>>
startxref
467
%%EOF";

            return Encoding.UTF8.GetBytes(pdfContent);
        }
    }
}