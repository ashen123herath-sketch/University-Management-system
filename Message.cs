using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniManage.Models
{
    public class Message
    {
        [Key]
        public int MessageId { get; set; }

        [Required]
        public int SenderId { get; set; }

        [ForeignKey("SenderId")]
        public User? Sender { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        [ForeignKey("ReceiverId")]
        public User? Receiver { get; set; }

        [Required(ErrorMessage = "Subject is required")]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message body is required")]
        [StringLength(2000)]
        public string MessageBody { get; set; } = string.Empty;

        public DateTime SentDate { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        public DateTime? ReadDate { get; set; }
    }
}
