using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Models;
using DocumentVerificationAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DocumentVerificationAPI.Services
{
    public class FormService : IFormService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FormService> _logger;
        private readonly ICacheService _cacheService;
        private readonly IPerformanceMonitoringService _performanceMonitoring;

        public FormService(
            ApplicationDbContext context, 
            ILogger<FormService> logger,
            ICacheService cacheService,
            IPerformanceMonitoringService performanceMonitoring)
        {
            _context = context;
            _logger = logger;
            _cacheService = cacheService;
            _performanceMonitoring = performanceMonitoring;
        }

        public async Task<Form> CreateFormAsync(FormCreationRequest request)
        {
            try
            {
                var form = new Form
                {
                    Id = Guid.NewGuid(),
                    UniqueUrl = GenerateUniqueUrl(),
                    RecruiterEmail = request.RecruiterEmail,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Pending"
                };

                _context.Forms.Add(form);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Form created successfully with ID: {FormId} and URL: {UniqueUrl}", form.Id, form.UniqueUrl);
                return form;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating form for recruiter: {RecruiterEmail}", request.RecruiterEmail);
                throw;
            }
        }

        public async Task<Form?> GetFormByUrlAsync(string uniqueUrl)
        {
            using var timer = _performanceMonitoring.StartTimer("form_get_by_url", new Dictionary<string, object>
            {
                ["UniqueUrl"] = uniqueUrl
            });

            try
            {
                // Validate URL format for security
                if (!IsValidFormUrl(uniqueUrl))
                {
                    _logger.LogWarning("Invalid form URL format: {UniqueUrl}", uniqueUrl);
                    return null;
                }

                // Try to get from cache first
                var cacheKey = $"form_url_{uniqueUrl}";
                var cachedForm = await _cacheService.GetAsync<Form>(cacheKey);
                if (cachedForm != null)
                {
                    _logger.LogDebug("Form retrieved from cache: {FormId}", cachedForm.Id);
                    _performanceMonitoring.RecordCounter("form_cache_hit");
                    
                    // Still need to check expiration and status for cached forms
                    if (IsFormExpired(cachedForm) || cachedForm.Status == "Submitted")
                    {
                        await _cacheService.RemoveAsync(cacheKey);
                        return null;
                    }
                    
                    return cachedForm;
                }

                _performanceMonitoring.RecordCounter("form_cache_miss");

                var form = await _context.Forms
                    .Include(f => f.PersonalInfo)
                    .Include(f => f.Documents)
                    .AsNoTracking() // Performance optimization for read-only queries
                    .FirstOrDefaultAsync(f => f.UniqueUrl == uniqueUrl);

                // Check if form has expired (30 days from creation)
                if (form != null && IsFormExpired(form))
                {
                    _logger.LogWarning("Attempted access to expired form: {FormId}, URL: {UniqueUrl}", form.Id, uniqueUrl);
                    return null;
                }

                // Check if form is already submitted and should not be accessible
                if (form != null && form.Status == "Submitted")
                {
                    _logger.LogWarning("Attempted access to submitted form: {FormId}, URL: {UniqueUrl}", form.Id, uniqueUrl);
                    return null;
                }

                // Cache the form for 15 minutes if it's valid
                if (form != null)
                {
                    await _cacheService.SetAsync(cacheKey, form, TimeSpan.FromMinutes(15));
                }

                return form;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form by URL: {UniqueUrl}", uniqueUrl);
                return null;
            }
        }

        public async Task<Form?> GetFormByIdAsync(Guid formId, bool includePersonalInfo = false, bool includeDocuments = false)
        {
            try
            {
                var query = _context.Forms.AsQueryable();

                if (includePersonalInfo)
                    query = query.Include(f => f.PersonalInfo);

                if (includeDocuments)
                    query = query.Include(f => f.Documents);

                return await query.FirstOrDefaultAsync(f => f.Id == formId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving form by ID: {FormId}", formId);
                return null;
            }
        }

        public async Task<PersonalInfo> SavePersonalInfoAsync(Guid formId, PersonalInfo personalInfo)
        {
            try
            {
                // Validate form access before saving personal info
                var form = await _context.Forms.FindAsync(formId);
                if (form == null)
                {
                    throw new ArgumentException($"Form with ID {formId} not found");
                }

                if (IsFormExpired(form))
                {
                    throw new InvalidOperationException("Cannot save personal info for expired form");
                }

                if (form.Status == "Submitted")
                {
                    throw new InvalidOperationException("Cannot modify submitted form");
                }

                // Sanitize input data
                personalInfo = SanitizePersonalInfo(personalInfo);

                // Check if personal info already exists for this form
                var existingPersonalInfo = await _context.PersonalInfo
                    .FirstOrDefaultAsync(p => p.FormId == formId);

                if (existingPersonalInfo != null)
                {
                    // Update existing personal info
                    existingPersonalInfo.FirstName = personalInfo.FirstName;
                    existingPersonalInfo.LastName = personalInfo.LastName;
                    existingPersonalInfo.Email = personalInfo.Email;
                    existingPersonalInfo.Phone = personalInfo.Phone;
                    existingPersonalInfo.Address = personalInfo.Address;
                    existingPersonalInfo.DateOfBirth = personalInfo.DateOfBirth;

                    _context.PersonalInfo.Update(existingPersonalInfo);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Personal info updated for form: {FormId}", formId);
                    return existingPersonalInfo;
                }
                else
                {
                    // Create new personal info
                    personalInfo.Id = Guid.NewGuid();
                    personalInfo.FormId = formId;
                    personalInfo.CreatedAt = DateTime.UtcNow;

                    _context.PersonalInfo.Add(personalInfo);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Personal info created for form: {FormId}", formId);
                    return personalInfo;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving personal info for form: {FormId}", formId);
                throw;
            }
        }

        public async Task<Document> AddDocumentAsync(Document document)
        {
            try
            {
                document.Id = Guid.NewGuid();
                document.UploadedAt = DateTime.UtcNow;
                document.VerificationStatus = "Pending";

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document added successfully: {DocumentId} for form: {FormId}", document.Id, document.FormId);
                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding document for form: {FormId}", document.FormId);
                throw;
            }
        }

        public async Task<Document> UpdateDocumentVerificationAsync(Guid documentId, DocumentVerificationResult verificationResult)
        {
            try
            {
                var document = await _context.Documents.FindAsync(documentId);
                if (document == null)
                {
                    throw new ArgumentException($"Document with ID {documentId} not found");
                }

                // Update document with verification results
                document.VerificationStatus = verificationResult.VerificationStatus;
                document.ConfidenceScore = verificationResult.ConfidenceScore;
                document.IsBlurred = verificationResult.IsBlurred;
                document.IsCorrectType = verificationResult.IsCorrectType;
                document.StatusColor = verificationResult.StatusColor;
                document.VerificationDetails = verificationResult.promptResponse?.reason;

                _context.Documents.Update(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document verification updated: {DocumentId} with status: {Status}", documentId, verificationResult.VerificationStatus);
                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document verification: {DocumentId}", documentId);
                throw;
            }
        }

        public async Task<Form> SubmitFormAsync(Guid formId)
        {
            try
            {
                var form = await _context.Forms.FindAsync(formId);
                if (form == null)
                {
                    throw new ArgumentException($"Form with ID {formId} not found");
                }

                form.Status = "Submitted";
                form.SubmittedAt = DateTime.UtcNow;

                _context.Forms.Update(form);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Form submitted successfully: {FormId}", formId);
                return form;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting form: {FormId}", formId);
                throw;
            }
        }

        public async Task<IEnumerable<Form>> GetFormsByRecruiterAsync(string recruiterEmail, bool includePersonalInfo = false, bool includeDocuments = false)
        {
            try
            {
                var query = _context.Forms
                    .Where(f => f.RecruiterEmail == recruiterEmail);

                if (includePersonalInfo)
                    query = query.Include(f => f.PersonalInfo);

                if (includeDocuments)
                    query = query.Include(f => f.Documents);

                return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving forms for recruiter: {RecruiterEmail}", recruiterEmail);
                return Enumerable.Empty<Form>();
            }
        }

        public async Task<bool> ValidateFormForSubmissionAsync(Guid formId)
        {
            try
            {
                var form = await _context.Forms
                    .Include(f => f.PersonalInfo)
                    .Include(f => f.Documents)
                    .FirstOrDefaultAsync(f => f.Id == formId);

                if (form == null)
                {
                    _logger.LogWarning("Form not found for validation: {FormId}", formId);
                    return false;
                }

                // Check if form is already submitted
                if (form.Status == "Submitted")
                {
                    _logger.LogWarning("Form already submitted: {FormId}", formId);
                    return false;
                }

                // Check if personal info exists
                if (form.PersonalInfo == null)
                {
                    _logger.LogWarning("Personal info missing for form: {FormId}", formId);
                    return false;
                }

                // Validate required personal info fields
                if (string.IsNullOrWhiteSpace(form.PersonalInfo.FirstName) ||
                    string.IsNullOrWhiteSpace(form.PersonalInfo.LastName) ||
                    string.IsNullOrWhiteSpace(form.PersonalInfo.Email))
                {
                    _logger.LogWarning("Required personal info fields missing for form: {FormId}", formId);
                    return false;
                }

                // Check if at least one document is uploaded
                if (!form.Documents.Any())
                {
                    _logger.LogWarning("No documents uploaded for form: {FormId}", formId);
                    return false;
                }

                // Check if all documents have been processed (not pending)
                var pendingDocuments = form.Documents.Where(d => d.VerificationStatus == "Pending").ToList();
                if (pendingDocuments.Any())
                {
                    _logger.LogWarning("Form has pending documents: {FormId}, Count: {PendingCount}", formId, pendingDocuments.Count);
                    return false;
                }

                _logger.LogInformation("Form validation successful: {FormId}", formId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating form for submission: {FormId}", formId);
                return false;
            }
        }

        public async Task<Models.DTOs.PaginatedFormsResult> GetSubmittedFormsAsync(string? recruiterEmail, string? status, string? searchTerm, int page, int pageSize)
        {
            try
            {
                var query = _context.Forms
                    .Include(f => f.PersonalInfo)
                    .Include(f => f.Documents)
                    .Where(f => f.Status == "Submitted" || f.Status == "Approved" || f.Status == "Rejected" || f.Status == "Under Review" || f.Status == "Pending");

                // Filter by recruiter email if provided
                if (!string.IsNullOrWhiteSpace(recruiterEmail))
                {
                    query = query.Where(f => f.RecruiterEmail == recruiterEmail);
                }

                // Filter by status if provided
                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(f => f.Status == status);
                }

                // Search by candidate name or email if provided
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(f => f.PersonalInfo != null && 
                        (f.PersonalInfo.FirstName.Contains(searchTerm) ||
                         f.PersonalInfo.LastName.Contains(searchTerm) ||
                         f.PersonalInfo.Email.Contains(searchTerm)));
                }

                var totalCount = await query.CountAsync();

                var forms = await query
                    .OrderByDescending(f => f.SubmittedAt ?? f.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} submitted forms out of {TotalCount} total", forms.Count, totalCount);

                return new Models.DTOs.PaginatedFormsResult
                {
                    Forms = forms,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting submitted forms");
                throw;
            }
        }

        public async Task<Form> UpdateFormStatusAsync(Guid formId, string status, string? reviewNotes)
        {
            try
            {
                var form = await _context.Forms.FindAsync(formId);
                if (form == null)
                {
                    throw new ArgumentException($"Form with ID {formId} not found");
                }

                form.Status = status;
                // Note: ReviewNotes would need to be added to the Form model if we want to store them
                // For now, we'll just update the status

                _context.Forms.Update(form);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Form status updated: {FormId} to {Status}", formId, status);
                return form;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating form status: {FormId}", formId);
                throw;
            }
        }

        public async Task<Models.DTOs.DashboardStats> GetDashboardStatsAsync(string? recruiterEmail)
        {
            try
            {
                var query = _context.Forms.AsQueryable();

                // Filter by recruiter email if provided
                if (!string.IsNullOrWhiteSpace(recruiterEmail))
                {
                    query = query.Where(f => f.RecruiterEmail == recruiterEmail);
                }

                var totalForms = await query.CountAsync();
                var submittedForms = await query.CountAsync(f => f.Status == "Submitted" || f.Status == "Approved" || f.Status == "Rejected" || f.Status == "Under Review");
                var approvedForms = await query.CountAsync(f => f.Status == "Approved");
                var rejectedForms = await query.CountAsync(f => f.Status == "Rejected");
                var pendingReview = await query.CountAsync(f => f.Status == "Submitted" || f.Status == "Under Review");

                // Count forms with low confidence documents
                var formsWithLowConfidenceDocuments = await query
                    .Include(f => f.Documents)
                    .CountAsync(f => f.Documents.Any(d => d.ConfidenceScore < 50));

                _logger.LogInformation("Dashboard stats retrieved successfully");

                return new Models.DTOs.DashboardStats
                {
                    TotalForms = totalForms,
                    SubmittedForms = submittedForms,
                    ApprovedForms = approvedForms,
                    RejectedForms = rejectedForms,
                    PendingReview = pendingReview,
                    FormsWithLowConfidenceDocuments = formsWithLowConfidenceDocuments
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                throw;
            }
        }

        #region Security Methods

        private string GenerateUniqueUrl()
        {
            // Generate a cryptographically secure unique URL token
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var tokenBytes = new byte[32]; // 256-bit token
            rng.GetBytes(tokenBytes);
            
            var token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
            
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return $"form-{timestamp}-{token}";
        }

        private bool IsValidFormUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // Check URL format: form-{timestamp}-{token}
            var parts = url.Split('-');
            if (parts.Length != 3 || parts[0] != "form")
                return false;

            // Validate timestamp part
            if (!long.TryParse(parts[1], out var timestamp))
                return false;

            // Validate token part (should be base64url encoded)
            var token = parts[2];
            if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
                return false;

            // Check for valid base64url characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"^[A-Za-z0-9_-]+$"))
                return false;

            return true;
        }

        private bool IsFormExpired(Form form)
        {
            // Forms expire after 30 days from creation
            var expirationDate = form.CreatedAt.AddDays(30);
            return DateTime.UtcNow > expirationDate;
        }

        private PersonalInfo SanitizePersonalInfo(PersonalInfo personalInfo)
        {
            return new PersonalInfo
            {
                Id = personalInfo.Id,
                FormId = personalInfo.FormId,
                FirstName = SanitizeString(personalInfo.FirstName, 50),
                LastName = SanitizeString(personalInfo.LastName, 50),
                Email = SanitizeEmail(personalInfo.Email),
                Phone = SanitizePhone(personalInfo.Phone),
                Address = SanitizeString(personalInfo.Address, 200),
                DateOfBirth = personalInfo.DateOfBirth,
                CreatedAt = personalInfo.CreatedAt
            };
        }

        private string SanitizeString(string input, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove potentially dangerous characters and trim
            var sanitized = input.Trim();
            
            // Remove HTML tags and script content
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"<[^>]*>", "");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"<script[^>]*>.*?</script>", "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            
            // Remove SQL injection patterns
            var sqlPatterns = new[] { "'", "\"", ";", "--", "/*", "*/", "xp_", "sp_", "exec", "execute", "select", "insert", "update", "delete", "drop", "create", "alter" };
            foreach (var pattern in sqlPatterns)
            {
                sanitized = sanitized.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
            }

            // Truncate to max length
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            return sanitized;
        }

        private string SanitizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            var sanitized = email.Trim().ToLowerInvariant();
            
            // Basic email format validation
            if (!System.Text.RegularExpressions.Regex.IsMatch(sanitized, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            {
                throw new ArgumentException("Invalid email format");
            }

            // Check for maximum length
            if (sanitized.Length > 254)
            {
                throw new ArgumentException("Email address is too long");
            }

            return sanitized;
        }

        private string SanitizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            // Remove all non-digit characters except + - ( ) and spaces
            var sanitized = System.Text.RegularExpressions.Regex.Replace(phone, @"[^\d\+\-\(\)\s]", "");
            
            // Validate phone number format
            var digitsOnly = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^\d]", "");
            if (digitsOnly.Length < 7 || digitsOnly.Length > 15)
            {
                throw new ArgumentException("Invalid phone number format");
            }

            return sanitized.Trim();
        }

        #endregion

        public async Task<PersonalInfo> UpdatePersonalInfoAsync(Guid formId, Models.DTOs.PersonalInfoRequest personalInfoRequest)
        {
            var form = await _context.Forms
                .Include(f => f.PersonalInfo)
                .FirstOrDefaultAsync(f => f.Id == formId);

            if (form == null)
                throw new ArgumentException("Form not found", nameof(formId));

            if (form.PersonalInfo == null)
            {
                form.PersonalInfo = new PersonalInfo
                {
                    Id = Guid.NewGuid(),
                    FormId = formId
                };
                _context.PersonalInfo.Add(form.PersonalInfo);
            }

            // Update personal info from request
            form.PersonalInfo.FirstName = SanitizeString(personalInfoRequest.FirstName, 50);
            form.PersonalInfo.LastName = SanitizeString(personalInfoRequest.LastName, 50);
            form.PersonalInfo.Email = SanitizeEmail(personalInfoRequest.Email);
            form.PersonalInfo.Phone = SanitizePhone(personalInfoRequest.Phone);
            form.PersonalInfo.DateOfBirth = personalInfoRequest.DateOfBirth;
            form.PersonalInfo.Address = SanitizeString(personalInfoRequest.Address, 200);
            form.PersonalInfo.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return form.PersonalInfo;
        }

        public async Task<Form?> GetFormWithDetailsAsync(Guid formId)
        {
            return await _context.Forms
                .Include(f => f.PersonalInfo)
                .Include(f => f.Documents)
                .FirstOrDefaultAsync(f => f.Id == formId);
        }

        public async Task<bool> ValidateFormCompletionAsync(Guid formId)
        {
            var form = await GetFormWithDetailsAsync(formId);
            if (form == null)
                return false;

            // Check if personal info is complete
            var personalInfoComplete = form.PersonalInfo != null &&
                !string.IsNullOrWhiteSpace(form.PersonalInfo.FirstName) &&
                !string.IsNullOrWhiteSpace(form.PersonalInfo.LastName) &&
                !string.IsNullOrWhiteSpace(form.PersonalInfo.Email);

            // Check if at least one document is uploaded and verified
            var documentsComplete = form.Documents != null &&
                form.Documents.Any(d => d.VerificationStatus == "Verified");

            return personalInfoComplete && documentsComplete;
        }

        public async Task<Document?> GetDocumentByIdAsync(Guid documentId)
        {
            try
            {
                _logger.LogInformation("Retrieving document by ID: {DocumentId}", documentId);

                var document = await _context.Documents
                    .Include(d => d.Form)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                {
                    _logger.LogWarning("Document not found: {DocumentId}", documentId);
                    return null;
                }

                _logger.LogInformation("Document retrieved successfully: {DocumentId}", documentId);
                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document by ID: {DocumentId}", documentId);
                return null;
            }
        }

        public async Task<PersonalInfo?> GetPersonalInfoAsync(Guid formId)
        {
            try
            {
                _logger.LogInformation("Getting personal info for form: {FormId}", formId);

                var personalInfo = await _context.PersonalInfo
                    .FirstOrDefaultAsync(p => p.FormId == formId);

                if (personalInfo == null)
                {
                    _logger.LogWarning("Personal info not found for form: {FormId}", formId);
                    return null;
                }

                _logger.LogInformation("Personal info retrieved successfully for form: {FormId}", formId);
                return personalInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting personal info for form: {FormId}", formId);
                throw;
            }
        }
    }
}