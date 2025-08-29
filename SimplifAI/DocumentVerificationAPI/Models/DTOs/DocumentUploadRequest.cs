using System.ComponentModel.DataAnnotations;
using DocumentVerificationAPI.Validation;

namespace DocumentVerificationAPI.Models.DTOs
{
    public class DocumentUploadRequest
    {
        [Required(ErrorMessage = "File is required")]
        [FileValidation(10, "image/jpeg,image/jpg,image/png,application/pdf", ".jpg,.jpeg,.png,.pdf")]
        public IFormFile File { get; set; } = null!;

        [Required(ErrorMessage = "Document type is required")]
        // [DocumentType("Passport,DriverLicense,NationalID,UtilityBill,BankStatement,BirthCertificate")]
        public string DocumentType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Form ID is required")]
        public Guid FormId { get; set; }
    }
}