using System.ComponentModel.DataAnnotations;

namespace UniManage.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required")]
        [StringLength(20)]
        public string Role { get; set; } = string.Empty; // Student, Lecturer, Administrator

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(50)]
        public string? Department { get; set; }

        public DateTime RegisteredDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}
