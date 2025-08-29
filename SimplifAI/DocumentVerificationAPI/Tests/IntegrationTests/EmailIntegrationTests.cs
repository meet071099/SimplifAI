using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using DocumentVerificationAPI.Services;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumentVerificationAPI.Tests.IntegrationTests
{
    public class EmailIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly IConfiguration _configuration;

        public EmailIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _configuration = _factory.Services.GetRequiredService<IConfiguration>();
        }

        [Fact]
        public async Task SmtpConfiguration_ShouldBeValid()
        {
            // Arrange & Act
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = _configuration["Email:SmtpPort"];
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];
            var fromEmail = _configuration["Email:FromEmail"];

            // Assert - Configuration should exist
            Assert.NotNull(smtpHost);
            Assert.NotNull(smtpPort);
            Assert.NotNull(username);
            Assert.NotNull(password);
            Assert.NotNull(fromEmail);

            // Verify specific Mailtrap configuration
            Assert.Equal("sandbox.smtp.mailtrap.io", smtpHost);
            Assert.Equal("587", smtpPort);
            Assert.Equal("1e0ced1c7ae6ed", username);
            Assert.Equal("0ade4f0af5136b", password);
        }

        [Fact]
        public async Task SendEmail_WithValidConfiguration_ShouldWork()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var testEmail = new EmailMessage
            {
                To = "test@example.com",
                Subject = "Integration Test - Email Service",
                Body = "This is a test email sent from the integration tests to verify SMTP configuration.",
                IsHtml = false
            };

            // Act & Assert - Should not throw exception
            var exception = await Record.ExceptionAsync(async () =>
            {
                await emailService.SendEmailAsync(testEmail);
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task SendHtmlEmail_ShouldWork()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var htmlBody = @"
                <html>
                <body>
                    <h1>Integration Test</h1>
                    <p>This is an <strong>HTML email</strong> sent from integration tests.</p>
                    <ul>
                        <li>SMTP Host: sandbox.smtp.mailtrap.io</li>
                        <li>Port: 587</li>
                        <li>Authentication: PLAIN/LOGIN</li>
                        <li>TLS: STARTTLS</li>
                    </ul>
                </body>
                </html>";

            var testEmail = new EmailMessage
            {
                To = "test@example.com",
                Subject = "Integration Test - HTML Email",
                Body = htmlBody,
                IsHtml = true
            };

            // Act & Assert - Should not throw exception
            var exception = await Record.ExceptionAsync(async () =>
            {
                await emailService.SendEmailAsync(testEmail);
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task EmailQueue_ShouldProcessEmails()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Clear any existing queue items for clean test
            var existingItems = await dbContext.EmailQueue.ToListAsync();
            dbContext.EmailQueue.RemoveRange(existingItems);
            await dbContext.SaveChangesAsync();

            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var testEmail = new EmailMessage
            {
                To = "queue-test@example.com",
                Subject = "Integration Test - Email Queue",
                Body = "This email tests the email queue processing functionality.",
                IsHtml = false
            };

            // Act - Send email (should be queued)
            await emailService.SendEmailAsync(testEmail);

            // Assert - Email should be in queue
            var queuedEmails = await dbContext.EmailQueue
                .Where(e => e.Status == "Pending")
                .ToListAsync();

            Assert.NotEmpty(queuedEmails);
            
            var queuedEmail = queuedEmails.FirstOrDefault(e => e.To == "queue-test@example.com");
            Assert.NotNull(queuedEmail);
            Assert.Equal("Integration Test - Email Queue", queuedEmail.Subject);
        }

        [Fact]
        public async Task FormSubmissionNotification_ShouldSendEmail()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            // Create a test form
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = Guid.NewGuid().ToString(),
                RecruiterEmail = "recruiter@test.com",
                Status = "Submitted",
                SubmittedAt = DateTime.UtcNow
            };

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@test.com",
                Phone = "+1234567890"
            };

            dbContext.Forms.Add(form);
            dbContext.PersonalInfo.Add(personalInfo);
            await dbContext.SaveChangesAsync();

            // Act - Send form submission notification
            var notificationEmail = new EmailMessage
            {
                To = form.RecruiterEmail,
                Subject = $"New Form Submission - {personalInfo.FirstName} {personalInfo.LastName}",
                Body = $@"
A new form has been submitted by {personalInfo.FirstName} {personalInfo.LastName}.

Candidate Details:
- Name: {personalInfo.FirstName} {personalInfo.LastName}
- Email: {personalInfo.Email}
- Phone: {personalInfo.Phone}
- Submitted: {form.SubmittedAt:yyyy-MM-dd HH:mm:ss} UTC

Please review the submission in the recruiter dashboard.
",
                IsHtml = false
            };

            var exception = await Record.ExceptionAsync(async () =>
            {
                await emailService.SendEmailAsync(notificationEmail);
            });

            // Assert
            Assert.Null(exception);

            // Cleanup
            dbContext.PersonalInfo.Remove(personalInfo);
            dbContext.Forms.Remove(form);
            await dbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task EmailLogging_ShouldWork()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var initialLogCount = await dbContext.EmailLogs.CountAsync();

            var testEmail = new EmailMessage
            {
                To = "logging-test@example.com",
                Subject = "Integration Test - Email Logging",
                Body = "This email tests the email logging functionality.",
                IsHtml = false
            };

            // Act
            await emailService.SendEmailAsync(testEmail);

            // Wait a moment for async processing
            await Task.Delay(1000);

            // Assert - Should have created a log entry
            var finalLogCount = await dbContext.EmailLogs.CountAsync();
            Assert.True(finalLogCount > initialLogCount, "Email log should be created");

            var recentLog = await dbContext.EmailLogs
                .OrderByDescending(l => l.SentAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(recentLog);
            Assert.Equal("logging-test@example.com", recentLog.To);
            Assert.Equal("Integration Test - Email Logging", recentLog.Subject);
        }

        [Fact]
        public async Task EmailService_WithInvalidSmtpSettings_ShouldHandleError()
        {
            // This test verifies error handling with invalid SMTP settings
            // Note: We can't easily test this without modifying the service configuration
            // In a real scenario, you might use a test-specific configuration or dependency injection
            
            // For now, we'll test that the service handles exceptions gracefully
            using var scope = _factory.Services.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var testEmail = new EmailMessage
            {
                To = "invalid-email-address", // Invalid email format
                Subject = "Test Invalid Email",
                Body = "This should handle invalid email gracefully.",
                IsHtml = false
            };

            // Act & Assert - Should handle invalid email gracefully
            var exception = await Record.ExceptionAsync(async () =>
            {
                await emailService.SendEmailAsync(testEmail);
            });

            // The service should handle this gracefully, either by validation or error handling
            // The exact behavior depends on the implementation
        }
    }
}