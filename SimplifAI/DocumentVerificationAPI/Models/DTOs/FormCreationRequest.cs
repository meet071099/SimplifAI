using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models.DTOs
{
    public class FormCreationRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string RecruiterEmail { get; set; } = string.Empty;
    }
}