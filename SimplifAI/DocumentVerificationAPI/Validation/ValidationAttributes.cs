using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DocumentVerificationAPI.Validation
{
    /// <summary>
    /// Validates that a file upload meets size and type requirements
    /// </summary>
    public class FileValidationAttribute : ValidationAttribute
    {
        private readonly long _maxSizeInBytes;
        private readonly string[] _allowedContentTypes;
        private readonly string[] _allowedExtensions;

        public FileValidationAttribute(
            int maxSizeInMB = 10,
            string allowedContentTypes = "image/jpeg,image/jpg,image/png,application/pdf",
            string allowedExtensions = ".jpg,.jpeg,.png,.pdf")
        {
            _maxSizeInBytes = maxSizeInMB * 1024 * 1024;
            _allowedContentTypes = allowedContentTypes.Split(',').Select(t => t.Trim().ToLower()).ToArray();
            _allowedExtensions = allowedExtensions.Split(',').Select(e => e.Trim().ToLower()).ToArray();
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not IFormFile file)
            {
                return new ValidationResult("No file provided");
            }

            // Validate file size
            if (file.Length > _maxSizeInBytes)
            {
                var maxSizeMB = _maxSizeInBytes / (1024 * 1024);
                return new ValidationResult($"File size exceeds maximum allowed size of {maxSizeMB}MB");
            }

            if (file.Length < 1024) // Minimum 1KB
            {
                return new ValidationResult("File is too small. Please ensure the file is not corrupted");
            }

            // Validate content type
            if (!_allowedContentTypes.Contains(file.ContentType.ToLower()))
            {
                return new ValidationResult($"File type '{file.ContentType}' is not supported. Allowed types: {string.Join(", ", _allowedContentTypes)}");
            }

            // Validate file extension
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!_allowedExtensions.Contains(extension))
            {
                return new ValidationResult($"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", _allowedExtensions)}");
            }

            // Validate filename
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                return new ValidationResult("File must have a valid name");
            }

            // Check for dangerous file patterns
            var dangerousExtensions = new[] { ".exe", ".bat", ".cmd", ".scr", ".com", ".pif", ".vbs", ".js" };
            if (dangerousExtensions.Any(ext => file.FileName.ToLower().EndsWith(ext)))
            {
                return new ValidationResult("File type is not allowed for security reasons");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Enhanced email validation attribute
    /// </summary>
    public class EnhancedEmailAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Let Required attribute handle null/empty
            }

            var email = value.ToString()!.Trim();

            // Check length
            if (email.Length > 254)
            {
                return new ValidationResult("Email address is too long (maximum 254 characters)");
            }

            // Basic format validation
            var emailRegex = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.IgnoreCase);
            if (!emailRegex.IsMatch(email))
            {
                return new ValidationResult("Please enter a valid email address");
            }

            // Check for consecutive dots
            if (email.Contains(".."))
            {
                return new ValidationResult("Email address cannot contain consecutive dots");
            }

            // Check for dots at start or end
            if (email.StartsWith(".") || email.EndsWith("."))
            {
                return new ValidationResult("Email address cannot start or end with a dot");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Enhanced phone number validation attribute
    /// </summary>
    public class EnhancedPhoneAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Let Required attribute handle null/empty
            }

            var phone = value.ToString()!.Trim();

            // Remove common formatting characters
            var cleanPhone = Regex.Replace(phone, @"[\s\-\(\)\+]", "");

            // Check if it contains only digits
            if (!Regex.IsMatch(cleanPhone, @"^\d+$"))
            {
                return new ValidationResult("Phone number can only contain digits, spaces, hyphens, parentheses, and plus sign");
            }

            // Check length (international format: 7-15 digits)
            if (cleanPhone.Length < 7)
            {
                return new ValidationResult("Phone number is too short (minimum 7 digits)");
            }

            if (cleanPhone.Length > 15)
            {
                return new ValidationResult("Phone number is too long (maximum 15 digits)");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Date of birth validation attribute
    /// </summary>
    public class DateOfBirthAttribute : ValidationAttribute
    {
        private readonly int _minimumAge;
        private readonly int _maximumAge;

        public DateOfBirthAttribute(int minimumAge = 16, int maximumAge = 120)
        {
            _minimumAge = minimumAge;
            _maximumAge = maximumAge;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success; // Let Required attribute handle null
            }

            if (value is not DateTime dateOfBirth)
            {
                return new ValidationResult("Please enter a valid date");
            }

            var today = DateTime.Today;

            // Check if date is in the future
            if (dateOfBirth > today)
            {
                return new ValidationResult("Date of birth cannot be in the future");
            }

            // Calculate age
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age))
            {
                age--;
            }

            // Check minimum age
            if (age < _minimumAge)
            {
                return new ValidationResult($"You must be at least {_minimumAge} years old");
            }

            // Check maximum age
            if (age > _maximumAge)
            {
                return new ValidationResult("Please enter a valid date of birth");
            }

            // Check if date is too far in the past
            if (dateOfBirth.Year < 1900)
            {
                return new ValidationResult("Please enter a date after 1900");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Name validation attribute (for first name, last name)
    /// </summary>
    public class NameAttribute : ValidationAttribute
    {
        private readonly int _minLength;
        private readonly int _maxLength;

        public NameAttribute(int minLength = 2, int maxLength = 50)
        {
            _minLength = minLength;
            _maxLength = maxLength;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Let Required attribute handle null/empty
            }

            var name = value.ToString()!.Trim();

            // Check length
            if (name.Length < _minLength)
            {
                return new ValidationResult($"Name must be at least {_minLength} characters long");
            }

            if (name.Length > _maxLength)
            {
                return new ValidationResult($"Name must not exceed {_maxLength} characters");
            }

            // Check for valid characters (letters, spaces, hyphens, apostrophes)
            var nameRegex = new Regex(@"^[a-zA-Z\s\-']+$");
            if (!nameRegex.IsMatch(name))
            {
                return new ValidationResult("Name can only contain letters, spaces, hyphens, and apostrophes");
            }

            // Check for consecutive special characters
            if (Regex.IsMatch(name, @"[\s\-']{2,}"))
            {
                return new ValidationResult("Name cannot contain consecutive spaces or special characters");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Address validation attribute
    /// </summary>
    public class AddressAttribute : ValidationAttribute
    {
        private readonly int _minLength;
        private readonly int _maxLength;

        public AddressAttribute(int minLength = 10, int maxLength = 200)
        {
            _minLength = minLength;
            _maxLength = maxLength;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Let Required attribute handle null/empty
            }

            var address = value.ToString()!.Trim();

            // Check length
            if (address.Length < _minLength)
            {
                return new ValidationResult($"Address must be at least {_minLength} characters long");
            }

            if (address.Length > _maxLength)
            {
                return new ValidationResult($"Address must not exceed {_maxLength} characters");
            }

            // Check for valid characters
            var addressRegex = new Regex(@"^[a-zA-Z0-9\s\-\.,#\/]+$");
            if (!addressRegex.IsMatch(address))
            {
                return new ValidationResult("Address contains invalid characters");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Document type validation attribute
    /// </summary>
    public class DocumentTypeAttribute : ValidationAttribute
    {
        private readonly string[] _allowedTypes;

        public DocumentTypeAttribute(string allowedTypes = "Passport,DriverLicense,NationalID,UtilityBill,BankStatement,BirthCertificate")
        {
            _allowedTypes = allowedTypes.Split(',').Select(t => t.Trim()).ToArray();
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult("Document type is required");
            }

            var documentType = value.ToString()!.Trim();

            if (!_allowedTypes.Contains(documentType, StringComparer.OrdinalIgnoreCase))
            {
                return new ValidationResult($"Invalid document type. Allowed types: {string.Join(", ", _allowedTypes)}");
            }

            return ValidationResult.Success;
        }
    }
}