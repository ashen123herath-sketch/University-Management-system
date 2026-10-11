using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniManage.Models
{
    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public User? Student { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.Now;

        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Completed, Dropped

        [Range(0, 100)]
        public double? FinalGrade { get; set; }

        [StringLength(2)]
        public string? LetterGrade { get; set; }

        public DateTime? CompletionDate { get; set; }
    }
}
