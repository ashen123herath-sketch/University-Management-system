using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniManage.Models
{
    public class CourseMaterial
    {
        [Key]
        public int MaterialId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [ForeignKey("CourseId")]
        public Course? Course { get; set; }

        [Required(ErrorMessage = "Material title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? FilePath { get; set; }

        [StringLength(50)]
        public string? FileType { get; set; }

        public DateTime UploadedDate { get; set; } = DateTime.Now;

        [Required]
        public int UploadedBy { get; set; }

        [ForeignKey("UploadedBy")]
        public User? Uploader { get; set; }
    }
}
