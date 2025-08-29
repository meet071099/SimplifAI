using System.ComponentModel.DataAnnotations;
using DocumentVerificationAPI.Validation;

namespace DocumentVerificationAPI.Models.DTOs
{
    public class PersonalInfoRequest
    {
        [Required(ErrorMessage = "First name is required")]
        [Name(2, 50)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Name(2, 50)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EnhancedEmail]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [EnhancedPhone]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        [Address(10, 500)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required")]
        [DateOfBirth(16, 120)]
        public DateTime? DateOfBirth { get; set; }
    }
}