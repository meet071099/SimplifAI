using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Services;
using Xunit;

namespace DocumentVerificationAPI.Tests
{
    public class EmailServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SmtpEmailService _emailService;
        private readonly ILogger<SmtpEmailService> _logger;

        public EmailServiceTests()
        {
            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            // Create mock configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Email:SmtpHost", "sandbox.smtp.mailtrap.io"},
                    {"Email:SmtpPort", "587"},
                    {"Email:Username", "1e0ced1c7ae6ed"},
                    {"Email:Password", "0ade4f0af5136b"},
                    {"Email:FromEmail", "noreply@documentverification.com"}
                })
                .Build();

            // Create mock logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<SmtpEmailService>();

            _emailService = new SmtpEmailService(_logger, configuration, _context);
        }

        [Fact]
        public async Task QueueEmailAsync_ShouldCreateEmailQueueEntry()
        {
            // Arrange
            var emailRequest = new EmailNotificationRequest
            {
                ToEmail = "test@example.com",
                Subject = "Test Email",
                Body = "This is a test email",
                IsHtml = true
            };

            // Act
            var emailQueueId = await _emailService.QueueEmailAsync(emailRequest, priority: 1);

            // Assert
            Assert.NotEqual(Guid.Empty, emailQueueId);

            var queuedEmail = await _context.EmailQueue.FindAsync(emailQueueId);
            Assert.NotNull(queuedEmail);
            Assert.Equal("test@example.com", queuedEmail.ToEmail);
            Assert.Equal("Test Email", queuedEmail.Subject);
            Assert.Equal("This is a test email", queuedEmail.Body);
            Assert.Equal("Pending", queuedEmail.Status);
            Assert.Equal(1, queuedEmail.Priority);
        }

        [Fact]
        public async Task QueueFormSubmissionNotificationAsync_ShouldCreateEmailQueueEntry()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form",
                RecruiterEmail = "recruiter@example.com",
                Status = "Submitted",
                SubmittedAt = DateTime.UtcNow
            };

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com"
            };

            var documents = new List<Document>
            {
                new Document
                {
                    Id = Guid.NewGuid(),
                    FormId = form.Id,
                    DocumentType = "Passport",
                    FileName = "passport.jpg",
                    VerificationStatus = "Verified",
                    ConfidenceScore = 95.5m,
                    StatusColor = "Green"
                }
            };

            // Act
            var emailQueueId = await _emailService.QueueFormSubmissionNotificationAsync(form, personalInfo, documents);

            // Assert
            Assert.NotEqual(Guid.Empty, emailQueueId);

            var queuedEmail = await _context.EmailQueue.FindAsync(emailQueueId);
            Assert.NotNull(queuedEmail);
            Assert.Equal("recruiter@example.com", queuedEmail.ToEmail);
            Assert.Contains("John Doe", queuedEmail.Subject);
            Assert.Contains("John", queuedEmail.Body);
            Assert.Contains("Doe", queuedEmail.Body);
            Assert.Equal("Pending", queuedEmail.Status);
            Assert.Equal(1, queuedEmail.Priority); // High priority for form submissions
            Assert.Equal(form.Id, queuedEmail.FormId);
        }

        [Fact]
        public async Task GetEmailQueueStatsAsync_ShouldReturnCorrectStats()
        {
            // Arrange
            var emailQueue1 = new EmailQueue
            {
                ToEmail = "test1@example.com",
                Subject = "Test 1",
                Body = "Body 1",
                Status = "Pending"
            };

            var emailQueue2 = new EmailQueue
            {
                ToEmail = "test2@example.com",
                Subject = "Test 2",
                Body = "Body 2",
                Status = "Sent",
                SentAt = DateTime.UtcNow
            };

            var emailQueue3 = new EmailQueue
            {
                ToEmail = "test3@example.com",
                Subject = "Test 3",
                Body = "Body 3",
                Status = "Failed"
            };

            _context.EmailQueue.AddRange(emailQueue1, emailQueue2, emailQueue3);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _emailService.GetEmailQueueStatsAsync();

            // Assert
            Assert.Equal(1, stats.PendingEmails);
            Assert.Equal(1, stats.SentEmails);
            Assert.Equal(1, stats.FailedEmails);
            Assert.Equal(0, stats.RetryEmails);
            Assert.NotNull(stats.OldestPendingEmail);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}