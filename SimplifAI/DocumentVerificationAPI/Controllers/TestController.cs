using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TestController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("database-connection")]
        public async Task<IActionResult> TestDatabaseConnection()
        {
            try
            {
                // Test database connection by counting forms
                var formCount = await _context.Forms.CountAsync();
                var personalInfoCount = await _context.PersonalInfo.CountAsync();
                var documentCount = await _context.Documents.CountAsync();

                return Ok(new
                {
                    Message = "Database connection successful",
                    FormsCount = formCount,
                    PersonalInfoCount = personalInfoCount,
                    DocumentsCount = documentCount,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "Database connection failed",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("seed-data")]
        public async Task<IActionResult> GetSeedData()
        {
            try
            {
                var forms = await _context.Forms
                    .Include(f => f.PersonalInfo)
                    .Include(f => f.Documents)
                    .ToListAsync();

                return Ok(new
                {
                    Message = "Seed data retrieved successfully",
                    Data = forms.Select(f => new
                    {
                        f.Id,
                        f.UniqueUrl,
                        f.RecruiterEmail,
                        f.Status,
                        f.CreatedAt,
                        PersonalInfo = f.PersonalInfo != null ? new
                        {
                            f.PersonalInfo.FirstName,
                            f.PersonalInfo.LastName,
                            f.PersonalInfo.Email
                        } : null,
                        DocumentsCount = f.Documents.Count,
                        Documents = f.Documents.Select(d => new
                        {
                            d.DocumentType,
                            d.FileName,
                            d.VerificationStatus,
                            d.ConfidenceScore,
                            d.StatusColor
                        })
                    }),
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to retrieve seed data",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
}