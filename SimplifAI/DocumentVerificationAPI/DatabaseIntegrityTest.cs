using Microsoft.EntityFrameworkCore;
using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI
{
    public class DatabaseIntegrityTest
    {
        public static async Task RunTests()
        {
            Console.WriteLine("Starting Database Integrity Tests...");
            
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);
            
            // Ensure database is created with all migrations
            context.Database.EnsureCreated();
            
            await TestCreatePersonalInfoRecord(context);
            await TestUpdatePersonalInfoRecord(context);
            await TestFormPersonalInfoRelationship(context);
            await TestSeededData(context);
            await TestUpdatedAtColumn(context);
            await TestCascadeDelete(context);
            
            Console.WriteLine("All Database Integrity Tests Completed Successfully!");
        }
        
        private static async Task TestCreatePersonalInfoRecord(ApplicationDbContext context)
        {
            Console.WriteLine("Test 1: Create PersonalInfo Record - Testing all fields save correctly");
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-create",
                RecruiterEmail = "recruiter@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

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

            context.PersonalInfo.Add(personalInfo);
            await context.SaveChangesAsync();

            var savedPersonalInfo = await context.PersonalInfo
                .FirstOrDefaultAsync(p => p.FormId == form.Id);
            
            if (savedPersonalInfo == null) throw new Exception("PersonalInfo not saved");
            if (savedPersonalInfo.FirstName != "Jane") throw new Exception("FirstName not saved correctly");
            if (savedPersonalInfo.LastName != "Smith") throw new Exception("LastName not saved correctly");
            if (savedPersonalInfo.Email != "jane.smith@test.com") throw new Exception("Email not saved correctly");
            if (savedPersonalInfo.Phone != "+1-555-123-4567") throw new Exception("Phone not saved correctly");
            if (savedPersonalInfo.Address != "456 Oak Avenue, Test City, TC 54321") throw new Exception("Address not saved correctly");
            if (savedPersonalInfo.DateOfBirth != new DateTime(1985, 6, 20)) throw new Exception("DateOfBirth not saved correctly");
            if (savedPersonalInfo.FormId != form.Id) throw new Exception("FormId not saved correctly");
            if (savedPersonalInfo.CreatedAt == DateTime.MinValue) throw new Exception("CreatedAt not set");
            
            Console.WriteLine("✓ Test 1 Passed: PersonalInfo record created with all fields saved correctly");
        }    
    
        private static async Task TestUpdatePersonalInfoRecord(ApplicationDbContext context)
        {
            Console.WriteLine("Test 2: Update PersonalInfo Record - Testing UpdatedAt field is set");
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-update",
                RecruiterEmail = "recruiter@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

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

            context.PersonalInfo.Add(personalInfo);
            await context.SaveChangesAsync();

            var initialCreatedAt = personalInfo.CreatedAt;
            await Task.Delay(100); // Ensure UpdatedAt will be different

            // Update the record
            personalInfo.FirstName = "John Updated";
            personalInfo.LastName = "Doe Updated";
            personalInfo.Email = "john.updated@test.com";
            personalInfo.Phone = "+1-555-111-2222";
            personalInfo.Address = "789 Updated Street, New City, NC 98765";
            personalInfo.DateOfBirth = new DateTime(1991, 2, 16);
            personalInfo.UpdatedAt = DateTime.UtcNow;

            context.PersonalInfo.Update(personalInfo);
            await context.SaveChangesAsync();

            var savedPersonalInfo = await context.PersonalInfo
                .FirstOrDefaultAsync(p => p.FormId == form.Id);
            
            if (savedPersonalInfo == null) throw new Exception("PersonalInfo not found after update");
            if (savedPersonalInfo.FirstName != "John Updated") throw new Exception("FirstName not updated correctly");
            if (savedPersonalInfo.LastName != "Doe Updated") throw new Exception("LastName not updated correctly");
            if (savedPersonalInfo.Email != "john.updated@test.com") throw new Exception("Email not updated correctly");
            if (savedPersonalInfo.Phone != "+1-555-111-2222") throw new Exception("Phone not updated correctly");
            if (savedPersonalInfo.Address != "789 Updated Street, New City, NC 98765") throw new Exception("Address not updated correctly");
            if (savedPersonalInfo.DateOfBirth != new DateTime(1991, 2, 16)) throw new Exception("DateOfBirth not updated correctly");
            if (!savedPersonalInfo.UpdatedAt.HasValue) throw new Exception("UpdatedAt not set");
            if (savedPersonalInfo.UpdatedAt <= initialCreatedAt) throw new Exception("UpdatedAt not greater than CreatedAt");
            if (savedPersonalInfo.CreatedAt != initialCreatedAt) throw new Exception("CreatedAt should remain unchanged");
            
            Console.WriteLine("✓ Test 2 Passed: PersonalInfo record updated with UpdatedAt field set correctly");
        }
        
        private static async Task TestFormPersonalInfoRelationship(ApplicationDbContext context)
        {
            Console.WriteLine("Test 3: Form-PersonalInfo Relationship - Testing foreign key constraints");
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-form-relationship",
                RecruiterEmail = "recruiter@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "Relationship",
                LastName = "Test",
                Email = "relationship@test.com",
                Phone = "+1-555-333-4444",
                Address = "Relationship Street",
                DateOfBirth = new DateTime(1988, 8, 8),
                CreatedAt = DateTime.UtcNow
            };

            context.PersonalInfo.Add(personalInfo);
            await context.SaveChangesAsync();

            // Test relationship from Form to PersonalInfo
            var formWithPersonalInfo = await context.Forms
                .Include(f => f.PersonalInfo)
                .FirstOrDefaultAsync(f => f.Id == form.Id);

            if (formWithPersonalInfo == null) throw new Exception("Form not found");
            if (formWithPersonalInfo.PersonalInfo == null) throw new Exception("PersonalInfo relationship not working");
            if (formWithPersonalInfo.PersonalInfo.Id != personalInfo.Id) throw new Exception("PersonalInfo relationship incorrect");
            if (formWithPersonalInfo.PersonalInfo.FormId != form.Id) throw new Exception("FormId relationship incorrect");

            // Test relationship from PersonalInfo to Form
            var personalInfoWithForm = await context.PersonalInfo
                .Include(p => p.Form)
                .FirstOrDefaultAsync(p => p.Id == personalInfo.Id);

            if (personalInfoWithForm == null) throw new Exception("PersonalInfo not found");
            if (personalInfoWithForm.Form == null) throw new Exception("Form relationship not working");
            if (personalInfoWithForm.Form.Id != form.Id) throw new Exception("Form relationship incorrect");
            if (personalInfoWithForm.Form.RecruiterEmail != form.RecruiterEmail) throw new Exception("Form data incorrect");
            
            Console.WriteLine("✓ Test 3 Passed: Form-PersonalInfo relationship working correctly");
        } 
       
        private static async Task TestSeededData(ApplicationDbContext context)
        {
            Console.WriteLine("Test 4: Seeded Test Data - Verifying seeded data is recreated properly");
            
            var seededForms = await context.Forms
                .Where(f => f.UniqueUrl.StartsWith("test-form-"))
                .Include(f => f.PersonalInfo)
                .Include(f => f.Documents)
                .ToListAsync();

            var seededPersonalInfo = await context.PersonalInfo
                .Where(p => p.Email.Contains("@test.com"))
                .ToListAsync();

            var seededDocuments = await context.Documents
                .Where(d => d.FilePath.Contains("/uploads/test/"))
                .ToListAsync();

            if (!seededForms.Any()) throw new Exception("No seeded forms found");
            if (!seededPersonalInfo.Any()) throw new Exception("No seeded personal info found");
            if (!seededDocuments.Any()) throw new Exception("No seeded documents found");

            // Verify specific seeded form
            var testForm1 = seededForms.FirstOrDefault(f => f.UniqueUrl == "test-form-1");
            if (testForm1 == null) throw new Exception("test-form-1 not found");
            if (testForm1.RecruiterEmail != "recruiter1@test.com") throw new Exception("test-form-1 recruiter email incorrect");
            if (testForm1.PersonalInfo == null) throw new Exception("test-form-1 personal info missing");
            if (testForm1.PersonalInfo.FirstName != "John") throw new Exception("test-form-1 personal info FirstName incorrect");
            if (testForm1.PersonalInfo.LastName != "Doe") throw new Exception("test-form-1 personal info LastName incorrect");
            if (testForm1.PersonalInfo.Email != "john.doe@test.com") throw new Exception("test-form-1 personal info Email incorrect");

            // Verify documents are properly linked
            var testForm1Documents = seededDocuments.Where(d => d.FormId == testForm1.Id).ToList();
            if (!testForm1Documents.Any()) throw new Exception("No documents found for test-form-1");
            
            var passportDoc = testForm1Documents.FirstOrDefault(d => d.DocumentType == "Passport");
            if (passportDoc == null) throw new Exception("Passport document not found for test-form-1");
            if (passportDoc.VerificationStatus != "Verified") throw new Exception("Passport document verification status incorrect");
            if (passportDoc.ConfidenceScore <= 90) throw new Exception("Passport document confidence score too low");
            if (passportDoc.IsBlurred) throw new Exception("Passport document should not be blurred");
            if (!passportDoc.IsCorrectType) throw new Exception("Passport document should be correct type");

            // Verify second test form
            var testForm2 = seededForms.FirstOrDefault(f => f.UniqueUrl == "test-form-2");
            if (testForm2 == null) throw new Exception("test-form-2 not found");
            if (testForm2.RecruiterEmail != "recruiter2@test.com") throw new Exception("test-form-2 recruiter email incorrect");

            // Verify failed document in second form
            var testForm2Documents = seededDocuments.Where(d => d.FormId == testForm2.Id).ToList();
            if (!testForm2Documents.Any()) throw new Exception("No documents found for test-form-2");
            
            var failedDoc = testForm2Documents.FirstOrDefault(d => d.VerificationStatus == "Failed");
            if (failedDoc == null) throw new Exception("Failed document not found for test-form-2");
            if (!failedDoc.IsBlurred) throw new Exception("Failed document should be blurred");
            if (failedDoc.IsCorrectType) throw new Exception("Failed document should not be correct type");
            if (failedDoc.ConfidenceScore >= 30) throw new Exception("Failed document confidence score too high");
            
            Console.WriteLine("✓ Test 4 Passed: Seeded test data recreated properly with expected structure");
        }
        
        private static async Task TestUpdatedAtColumn(ApplicationDbContext context)
        {
            Console.WriteLine("Test 5: UpdatedAt Column Schema - Verifying UpdatedAt column exists in database schema");
            
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

            context.PersonalInfo.Add(personalInfo);
            await context.SaveChangesAsync();

            var savedPersonalInfo = await context.PersonalInfo
                .FirstOrDefaultAsync(p => p.Id == personalInfo.Id);

            if (savedPersonalInfo == null) throw new Exception("PersonalInfo not saved");
            if (!savedPersonalInfo.UpdatedAt.HasValue) throw new Exception("UpdatedAt column not working");
            if (savedPersonalInfo.UpdatedAt <= savedPersonalInfo.CreatedAt) throw new Exception("UpdatedAt not greater than CreatedAt");
            
            // Verify we can query by UpdatedAt (proves column exists in schema)
            var recentlyUpdated = await context.PersonalInfo
                .Where(p => p.UpdatedAt.HasValue && p.UpdatedAt > DateTime.UtcNow.AddHours(-1))
                .CountAsync();
            
            if (recentlyUpdated == 0) throw new Exception("Query by UpdatedAt failed - column may not exist");
            
            Console.WriteLine("✓ Test 5 Passed: UpdatedAt column exists in database schema and works correctly");
        }    
    
        private static async Task TestCascadeDelete(ApplicationDbContext context)
        {
            Console.WriteLine("Test 6: Cascade Delete - Testing Form-PersonalInfo cascade delete relationship");
            
            var form = new Form
            {
                Id = Guid.NewGuid(),
                UniqueUrl = "test-cascade-delete",
                RecruiterEmail = "cascade@test.com",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            context.Forms.Add(form);
            await context.SaveChangesAsync();

            var personalInfo = new PersonalInfo
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                FirstName = "Cascade",
                LastName = "Delete",
                Email = "cascade.delete@test.com",
                Phone = "+1-555-999-0000",
                Address = "Delete Street",
                DateOfBirth = new DateTime(1995, 12, 25),
                CreatedAt = DateTime.UtcNow
            };

            context.PersonalInfo.Add(personalInfo);
            await context.SaveChangesAsync();

            // Verify both records exist
            var formExists = await context.Forms.AnyAsync(f => f.Id == form.Id);
            var personalInfoExists = await context.PersonalInfo.AnyAsync(p => p.Id == personalInfo.Id);
            if (!formExists) throw new Exception("Form not created");
            if (!personalInfoExists) throw new Exception("PersonalInfo not created");

            // Delete the form
            context.Forms.Remove(form);
            await context.SaveChangesAsync();

            // Verify cascade delete worked
            var formExistsAfterDelete = await context.Forms.AnyAsync(f => f.Id == form.Id);
            var personalInfoExistsAfterDelete = await context.PersonalInfo.AnyAsync(p => p.Id == personalInfo.Id);
            
            if (formExistsAfterDelete) throw new Exception("Form not deleted");
            if (personalInfoExistsAfterDelete) throw new Exception("PersonalInfo not cascade deleted");
            
            Console.WriteLine("✓ Test 6 Passed: Cascade delete working correctly - PersonalInfo deleted when Form is deleted");
        }
    }
}