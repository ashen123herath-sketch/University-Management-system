using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniManage.Models
{
    public class Assignment
    {
        [Key]
        public int AssignmentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        [Required(ErrorMessage = "Assignment title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Range(0, 100)]
        public int MaxMarks { get; set; } = 100;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        // File attachment uploaded by lecturer/admin
        [StringLength(500)]
        public string? FilePath { get; set; }

        [StringLength(50)]
        public string? FileType { get; set; }

        // Navigation properties
        public ICollection<Submission>? Submissions { get; set; }
    }
}
