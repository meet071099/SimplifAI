using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models.DTOs
{
    public class FormRejectionRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string RecruiterEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(1000, MinimumLength = 10)]
        public string Reason { get; set; } = string.Empty;
    }
}