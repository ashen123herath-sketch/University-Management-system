using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniManage.Models
{
    public class Submission
    {
        [Key]
        public int SubmissionId { get; set; }

        [Required]
        public int AssignmentId { get; set; }

        [ForeignKey("AssignmentId")]
        public Assignment? Assignment { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public User? Student { get; set; }

        public DateTime SubmissionDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? FilePath { get; set; }

        [StringLength(1000)]
        public string? Comments { get; set; }

        [Range(0, 100)]
        public double? MarksObtained { get; set; }

        [StringLength(500)]
        public string? Feedback { get; set; }

        public DateTime? GradedDate { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Submitted"; // Submitted, Graded, Late
    }
}
