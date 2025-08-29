using Microsoft.EntityFrameworkCore;
using Xunit;
using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI.Tests
{
    public class DatabaseIntegrityTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public DatabaseIntegrityTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            // Ensure database is created with all migrations
            _context.Database.EnsureCreated();
        }

        [Fact]
        public async Task CreatePersonalInfoRecord_ShouldSaveAllFieldsCorrectly()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-create",
                RecruiterEmail = "recruiter@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@test.com",
                Phone = "+1-555-123-4567",
                Address = "456 Oak Avenue, Test City, TC 54321",
                DateOfBirth = new DateTime(1985, 6, 20),
                CreatedAt = DateTime.UtcNow
            };

            // Act
            _context.PersonalInfo.Add(personalInfo);
            await _context.SaveChangesAsync();

            // Assert
            var savedPersonalInfo = await _context.PersonalInfo
                .FirstOrDefaultAsync(p => p.FormId == form.Id);
            
            Assert.NotNull(savedPersonalInfo);
            Assert.Equal(personalInfo.FirstName, savedPersonalInfo.FirstName);
            Assert.Equal(personalInfo.LastName, savedPersonalInfo.LastName);
            Assert.Equal(personalInfo.Email, savedPersonalInfo.Email);
            Assert.Equal(personalInfo.Phone, savedPersonalInfo.Phone);
            Assert.Equal(personalInfo.Address, savedPersonalInfo.Address);
            Assert.Equal(personalInfo.DateOfBirth, savedPersonalInfo.DateOfBirth);
            Assert.Equal(form.Id, savedPersonalInfo.FormId);
            Assert.True(savedPersonalInfo.CreatedAt > DateTime.MinValue);
        }

        [Fact]
        public async Task UpdatePersonalInfoRecord_ShouldSetUpdatedAtField()
        {
            // Arrange - Create initial record
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-update",
                RecruiterEmail = "recruiter@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@test.com",
                Phone = "+1-555-987-6543",
                Address = "123 Main Street, Test City, TC 12345",
                DateOfBirth = new DateTime(1990, 1, 15),
                CreatedAt = DateTime.UtcNow
            };

            _context.PersonalInfo.Add(personalInfo);
            await _context.SaveChangesAsync();

            var initialCreatedAt = personalInfo.CreatedAt;

            // Wait a moment to ensure UpdatedAt will be different
            await Task.Delay(100);

            // Act - Update the record
            personalInfo.FirstName = "John Updated";
            personalInfo.LastName = "Doe Updated";
            personalInfo.Email = "john.updated@test.com";
            personalInfo.Phone = "+1-555-111-2222";
            personalInfo.Address = "789 Updated Street, New City, NC 98765";
            personalInfo.DateOfBirth = new DateTime(1991, 2, 16);
            personalInfo.UpdatedAt = DateTime.UtcNow;

            _context.PersonalInfo.Update(personalInfo);
            await _context.SaveChangesAsync();

            // Assert
            var savedPersonalInfo = await _context.PersonalInfo
                .FirstOrDefaultAsync(p => p.FormId == form.Id);
            
            Assert.NotNull(savedPersonalInfo);
            Assert.Equal("John Updated", savedPersonalInfo.FirstName);
            Assert.Equal("Doe Updated", savedPersonalInfo.LastName);
            Assert.Equal("john.updated@test.com", savedPersonalInfo.Email);
            Assert.Equal("+1-555-111-2222", savedPersonalInfo.Phone);
            Assert.Equal("789 Updated Street, New City, NC 98765", savedPersonalInfo.Address);
            Assert.Equal(new DateTime(1991, 2, 16), savedPersonalInfo.DateOfBirth);
            
            // Verify UpdatedAt is set and different from CreatedAt
            Assert.NotNull(savedPersonalInfo.UpdatedAt);
            Assert.True(savedPersonalInfo.UpdatedAt > initialCreatedAt);
            Assert.Equal(initialCreatedAt, savedPersonalInfo.CreatedAt); // CreatedAt should remain unchanged
        }

        [Fact]
        public async Task FormPersonalInfoRelationship_ShouldMaintainForeignKeyConstraints()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-relationship",
                RecruiterEmail = "recruiter@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            var personalInfoRequest = new PersonalInfoRequest
            {
                FirstName = "Relationship",
                LastName = "Test",
                Email = "relationship@test.com",
                Phone = "+1-555-333-4444",
                Address = "Relationship Street",
                DateOfBirth = new DateTime(1988, 8, 8)
            };

            // Act
            var personalInfo = await _formService.UpdatePersonalInfoAsync(form.Id, personalInfoRequest);

            // Assert - Verify relationship exists
            var formWithPersonalInfo = await _context.Forms
                .Include(f => f.PersonalInfo)
                .FirstOrDefaultAsync(f => f.Id == form.Id);

            Assert.NotNull(formWithPersonalInfo);
            Assert.NotNull(formWithPersonalInfo.PersonalInfo);
            Assert.Equal(personalInfo.Id, formWithPersonalInfo.PersonalInfo.Id);
            Assert.Equal(form.Id, formWithPersonalInfo.PersonalInfo.FormId);

            // Verify navigation property works both ways
            var personalInfoWithForm = await _context.PersonalInfo
                .Include(p => p.Form)
                .FirstOrDefaultAsync(p => p.Id == personalInfo.Id);

            Assert.NotNull(personalInfoWithForm);
            Assert.NotNull(personalInfoWithForm.Form);
            Assert.Equal(form.Id, personalInfoWithForm.Form.Id);
            Assert.Equal(form.RecruiterEmail, personalInfoWithForm.Form.RecruiterEmail);
        }

        [Fact]
        public async Task SeededTestData_ShouldBeRecreatedProperly()
        {
            // Act - Check if seeded data exists
            var seededForms = await _context.Forms
                .Where(f => f.UniqueUrl.StartsWith("test-form-"))
                .Include(f => f.PersonalInfo)
                .Include(f => f.Documents)
                .ToListAsync();

            var seededPersonalInfo = await _context.PersonalInfo
                .Where(p => p.Email.Contains("@test.com"))
                .ToListAsync();

            var seededDocuments = await _context.Documents
                .Where(d => d.FilePath.Contains("/uploads/test/"))
                .ToListAsync();

            // Assert - Verify seeded data exists and has expected structure
            Assert.NotEmpty(seededForms);
            Assert.NotEmpty(seededPersonalInfo);
            Assert.NotEmpty(seededDocuments);

            // Verify specific seeded form exists
            var testForm1 = seededForms.FirstOrDefault(f => f.UniqueUrl == "test-form-1");
            Assert.NotNull(testForm1);
            Assert.Equal("recruiter1@test.com", testForm1.RecruiterEmail);
            Assert.NotNull(testForm1.PersonalInfo);
            Assert.Equal("John", testForm1.PersonalInfo.FirstName);
            Assert.Equal("Doe", testForm1.PersonalInfo.LastName);
            Assert.Equal("john.doe@test.com", testForm1.PersonalInfo.Email);

            // Verify documents are properly linked
            var testForm1Documents = seededDocuments.Where(d => d.FormId == testForm1.Id).ToList();
            Assert.NotEmpty(testForm1Documents);
            
            var passportDoc = testForm1Documents.FirstOrDefault(d => d.DocumentType == "Passport");
            Assert.NotNull(passportDoc);
            Assert.Equal("Verified", passportDoc.VerificationStatus);
            Assert.True(passportDoc.ConfidenceScore > 90);
            Assert.False(passportDoc.IsBlurred);
            Assert.True(passportDoc.IsCorrectType);

            // Verify second test form
            var testForm2 = seededForms.FirstOrDefault(f => f.UniqueUrl == "test-form-2");
            Assert.NotNull(testForm2);
            Assert.Equal("recruiter2@test.com", testForm2.RecruiterEmail);

            // Verify failed document in second form
            var testForm2Documents = seededDocuments.Where(d => d.FormId == testForm2.Id).ToList();
            Assert.NotEmpty(testForm2Documents);
            
            var failedDoc = testForm2Documents.FirstOrDefault(d => d.VerificationStatus == "Failed");
            Assert.NotNull(failedDoc);
            Assert.True(failedDoc.IsBlurred);
            Assert.False(failedDoc.IsCorrectType);
            Assert.True(failedDoc.ConfidenceScore < 30);
        }

        [Fact]
        public async Task DatabaseSchema_ShouldHaveUpdatedAtColumnInPersonalInfo()
        {
            // Arrange - Create a PersonalInfo record directly to test schema
            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = Guid.NewGuid(),
                FirstName = "Schema",
                LastName = "Test",
                Email = "schema@test.com",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow.AddMinutes(5) // Set UpdatedAt to test column exists
            };

            // Act
            _context.PersonalInfo.Add(personalInfo);
            await _context.SaveChangesAsync();

            // Assert - Retrieve and verify UpdatedAt column exists and works
            var savedPersonalInfo = await _context.PersonalInfo
                .FirstOrDefaultAsync(p => p.Id == personalInfo.Id);

            Assert.NotNull(savedPersonalInfo);
            Assert.NotNull(savedPersonalInfo.UpdatedAt);
            Assert.True(savedPersonalInfo.UpdatedAt > savedPersonalInfo.CreatedAt);
            
            // Verify we can query by UpdatedAt (proves column exists in schema)
            var recentlyUpdated = await _context.PersonalInfo
                .Where(p => p.UpdatedAt.HasValue && p.UpdatedAt > DateTime.UtcNow.AddHours(-1))
                .CountAsync();
            
            Assert.True(recentlyUpdated > 0);
        }

        [Fact]
        public async Task CascadeDelete_ShouldWorkCorrectlyForFormPersonalInfoRelationship()
        {
            // Arrange
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-cascade-delete",
                RecruiterEmail = "cascade@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Forms.Add(form);
            await _context.SaveChangesAsync();

            var personalInfoRequest = new PersonalInfoRequest
            {
                FirstName = "Cascade",
                LastName = "Delete",
                Email = "cascade.delete@test.com",
                Phone = "+1-555-999-0000",
                Address = "Delete Street",
                DateOfBirth = new DateTime(1995, 12, 25)
            };

            var personalInfo = await _formService.UpdatePersonalInfoAsync(form.Id, personalInfoRequest);

            // Verify both records exist
            var formExists = await _context.Forms.AnyAsync(f => f.Id == form.Id);
            var personalInfoExists = await _context.PersonalInfo.AnyAsync(p => p.Id == personalInfo.Id);
            Assert.True(formExists);
            Assert.True(personalInfoExists);

            // Act - Delete the form
            _context.Forms.Remove(form);
            await _context.SaveChangesAsync();

            // Assert - PersonalInfo should be cascade deleted
            var formExistsAfterDelete = await _context.Forms.AnyAsync(f => f.Id == form.Id);
            var personalInfoExistsAfterDelete = await _context.PersonalInfo.AnyAsync(p => p.Id == personalInfo.Id);
            
            Assert.False(formExistsAfterDelete);
            Assert.False(personalInfoExistsAfterDelete); // Should be cascade deleted
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}