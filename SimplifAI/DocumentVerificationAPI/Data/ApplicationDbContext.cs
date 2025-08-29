using Microsoft.EntityFrameworkCore;
using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Form> Forms { get; set; } = null!;
        public DbSet<PersonalInfo> PersonalInfo { get; set; } = null!;
        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<EmailQueue> EmailQueue { get; set; } = null!;
        public DbSet<EmailLog> EmailLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Form entity
            modelBuilder.Entity<Form>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UniqueUrl).IsUnique();
                entity.HasIndex(e => e.RecruiterEmail); // Index for recruiter queries
                entity.HasIndex(e => e.Status); // Index for status filtering
                entity.HasIndex(e => e.CreatedAt); // Index for date range queries
                entity.HasIndex(e => new { e.RecruiterEmail, e.Status }); // Composite index for common queries
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Status).HasDefaultValue("Pending");
            });

            // Configure PersonalInfo entity
            modelBuilder.Entity<PersonalInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FormId).IsUnique(); // Unique index for one-to-one relationship
                entity.HasIndex(e => e.Email); // Index for email searches
                entity.HasIndex(e => new { e.FirstName, e.LastName }); // Composite index for name searches
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                // Configure relationship with Form (One-to-One)
                entity.HasOne(p => p.Form)
                      .WithOne(f => f.PersonalInfo)
                      .HasForeignKey<PersonalInfo>(p => p.FormId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Document entity
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FormId); // Index for form document queries
                entity.HasIndex(e => e.DocumentType); // Index for document type filtering
                entity.HasIndex(e => e.VerificationStatus); // Index for status filtering
                entity.HasIndex(e => e.UploadedAt); // Index for date range queries
                entity.HasIndex(e => new { e.FormId, e.DocumentType }); // Composite index for form + type queries
                entity.HasIndex(e => new { e.VerificationStatus, e.UploadedAt }); // Composite index for status + date queries
                entity.Property(e => e.UploadedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.VerificationStatus).HasDefaultValue("Pending");
                entity.Property(e => e.IsBlurred).HasDefaultValue(false);
                entity.Property(e => e.IsCorrectType).HasDefaultValue(false);
                
                // Configure relationship with Form (One-to-Many)
                entity.HasOne(d => d.Form)
                      .WithMany(f => f.Documents)
                      .HasForeignKey(d => d.FormId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure EmailQueue entity
            modelBuilder.Entity<EmailQueue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Status); // Index for status filtering
                entity.HasIndex(e => e.Priority); // Index for priority ordering
                entity.HasIndex(e => e.CreatedAt); // Index for date ordering
                entity.HasIndex(e => e.ScheduledFor); // Index for scheduled email processing
                entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt }); // Composite index for queue processing
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Status).HasDefaultValue("Pending");
                entity.Property(e => e.RetryCount).HasDefaultValue(0);
                entity.Property(e => e.MaxRetries).HasDefaultValue(3);
                entity.Property(e => e.Priority).HasDefaultValue(1);
                
                // Configure optional relationship with Form
                entity.HasOne(e => e.Form)
                      .WithMany()
                      .HasForeignKey(e => e.FormId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure EmailLog entity
            modelBuilder.Entity<EmailLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.EmailQueueId); // Index for queue log queries
                entity.HasIndex(e => e.AttemptedAt); // Index for date range queries
                entity.HasIndex(e => e.Success); // Index for success/failure filtering
                entity.HasIndex(e => new { e.EmailQueueId, e.AttemptedAt }); // Composite index for queue + date queries
                entity.Property(e => e.AttemptedAt).HasDefaultValueSql("GETUTCDATE()");
                
                // Configure relationship with EmailQueue (One-to-Many)
                entity.HasOne(e => e.EmailQueue)
                      .WithMany()
                      .HasForeignKey(e => e.EmailQueueId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed data for testing different document types
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Create test forms
            var testForm1 = new Form
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UniqueUrl = "test-form-1",
                RecruiterEmail = "recruiter1@test.com",
                CreatedAt = DateTime.UtcNow
            };

            var testForm2 = new Form
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                UniqueUrl = "test-form-2",
                RecruiterEmail = "recruiter2@test.com",
                CreatedAt = DateTime.UtcNow
            };

            modelBuilder.Entity<Form>().HasData(testForm1, testForm2);

            // Create test personal info
            var testPersonalInfo1 = new PersonalInfo
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                FormId = testForm1.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@test.com",
                Phone = "+1234567890",
                Address = "123 Test Street, Test City, TC 12345",
                DateOfBirth = new DateTime(1990, 1, 15),
                CreatedAt = DateTime.UtcNow
            };

            modelBuilder.Entity<PersonalInfo>().HasData(testPersonalInfo1);

            // Create test documents with different types and verification statuses
            var testDocuments = new[]
            {
                new Document
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    FormId = testForm1.Id,
                    DocumentType = "Passport",
                    FileName = "passport_sample.jpg",
                    FilePath = "/uploads/test/passport_sample.jpg",
                    FileSize = 1024000,
                    ContentType = "image/jpeg",
                    VerificationStatus = "Verified",
                    ConfidenceScore = 95.5m,
                    IsBlurred = false,
                    IsCorrectType = true,
                    StatusColor = "Green",
                    UploadedAt = DateTime.UtcNow
                },
                new Document
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    FormId = testForm1.Id,
                    DocumentType = "DriverLicense",
                    FileName = "license_sample.jpg",
                    FilePath = "/uploads/test/license_sample.jpg",
                    FileSize = 856000,
                    ContentType = "image/jpeg",
                    VerificationStatus = "Verified",
                    ConfidenceScore = 72.3m,
                    IsBlurred = false,
                    IsCorrectType = true,
                    StatusColor = "Yellow",
                    UploadedAt = DateTime.UtcNow
                },
                new Document
                {
                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    FormId = testForm2.Id,
                    DocumentType = "Passport",
                    FileName = "blurred_passport.jpg",
                    FilePath = "/uploads/test/blurred_passport.jpg",
                    FileSize = 512000,
                    ContentType = "image/jpeg",
                    VerificationStatus = "Failed",
                    ConfidenceScore = 25.8m,
                    IsBlurred = true,
                    IsCorrectType = false,
                    StatusColor = "Red",
                    UploadedAt = DateTime.UtcNow
                }
            };

            modelBuilder.Entity<Document>().HasData(testDocuments);
        }
    }
}