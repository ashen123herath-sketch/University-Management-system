using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniManage.Models
{
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        [Required(ErrorMessage = "Course code is required")]
        [StringLength(20)]
        public string CourseCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Course name is required")]
        [StringLength(200)]
        public string CourseName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [Range(1, 10, ErrorMessage = "Credits must be between 1 and 10")]
        public int Credits { get; set; }

        [Required]
        [StringLength(50)]
        public string Department { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Semester { get; set; }

        [Required]
        [Range(1, 500, ErrorMessage = "Max students must be between 1 and 500")]
        public int MaxStudents { get; set; } = 50;

        public int? LecturerId { get; set; }

        [ForeignKey("LecturerId")]
        public User? Lecturer { get; set; }

        [StringLength(100)]
        public string? Prerequisites { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<Enrollment>? Enrollments { get; set; }
        public ICollection<Assignment>? Assignments { get; set; }
        public ICollection<CourseMaterial>? CourseMaterials { get; set; }
    }
}
