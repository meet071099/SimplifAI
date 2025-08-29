using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models.DTOs
{
    public class CreateFormRequest
    {
        [Required(ErrorMessage = "Recruiter email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string RecruiterEmail { get; set; } = string.Empty;
    }
}