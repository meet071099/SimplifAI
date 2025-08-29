using System.ComponentModel.DataAnnotations;

namespace DocumentVerificationAPI.Models
{
    /// <summary>
    /// Model class to hold form personal info and Document Intelligence extracted text for authenticity verification
    /// </summary>
    public class DocumentAuthenticityRequest
    {
        /// <summary>
        /// First name from the form
        /// </summary>
        [Required]
        [StringLength(100)]
        public string FormFirstName { get; set; } = string.Empty;

        /// <summary>
        /// Last name from the form
        /// </summary>
        [Required]
        [StringLength(100)]
        public string FormLastName { get; set; } = string.Empty;

        /// <summary>
        /// Text extracted from the document using Azure Document Intelligence
        /// </summary>
        [Required]
        public string ExtractedText { get; set; } = string.Empty;

        /// <summary>
        /// Optional document type for context
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// Optional form ID for reference
        /// </summary>
        public Guid? FormId { get; set; }
    }
}