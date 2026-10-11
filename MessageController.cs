using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;

namespace UniManage.Controllers
{
    public class MessageController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<MessageController> _logger;

        public MessageController(UniManageDbContext context, ILogger<MessageController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Message/Index (Inbox)
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var messages = await _context.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m => m.ReceiverId == userId)
                    .OrderByDescending(m => m.SentDate)
                    .ToListAsync();

                return View(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Inbox error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading messages.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Message/Sent
        public async Task<IActionResult> Sent()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var messages = await _context.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m => m.SenderId == userId)
                    .OrderByDescending(m => m.SentDate)
                    .ToListAsync();

                return View(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Sent messages error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading sent messages.";
                return RedirectToAction("Index");
            }
        }

        // GET: Message/Compose
        public async Task<IActionResult> Compose(int? receiverId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get list of users based on role
            var usersQuery = _context.Users.Where(u => u.UserId != userId && u.IsActive);

            if (userRole == "Student")
            {
                // Students can message lecturers and admins
                usersQuery = usersQuery.Where(u => u.Role == "Lecturer" || u.Role == "Administrator");
            }
            else if (userRole == "Lecturer")
            {
                // Lecturers can message students (in their courses) and admins
                var lecturerCourseIds = await _context.Courses
                    .Where(c => c.LecturerId == userId)
                    .Select(c => c.CourseId)
                    .ToListAsync();

                var studentIds = await _context.Enrollments
                    .Where(e => lecturerCourseIds.Contains(e.CourseId) && e.Status == "Active")
                    .Select(e => e.StudentId)
                    .Distinct()
                    .ToListAsync();

                usersQuery = usersQuery.Where(u => 
                    studentIds.Contains(u.UserId) || u.Role == "Administrator");
            }

            ViewBag.Users = await usersQuery.OrderBy(u => u.FullName).ToListAsync();

            var message = new Message();
            if (receiverId.HasValue)
            {
                message.ReceiverId = receiverId.Value;
            }

            return View(message);
        }

        // POST: Message/Compose
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Compose(Message message)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                ModelState.Remove("Sender");
                ModelState.Remove("Receiver");

                if (ModelState.IsValid)
                {
                    message.SenderId = userId.Value;
                    message.SentDate = DateTime.Now;
                    message.IsRead = false;

                    _context.Add(message);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Message sent from {userId} to {message.ReceiverId}");
                    TempData["SuccessMessage"] = "Message sent successfully!";
                    return RedirectToAction("Sent");
                }

                var userRole = HttpContext.Session.GetString("UserRole");
                var usersQuery = _context.Users.Where(u => u.UserId != userId && u.IsActive);

                if (userRole == "Student")
                {
                    usersQuery = usersQuery.Where(u => u.Role == "Lecturer" || u.Role == "Administrator");
                }

                ViewBag.Users = await usersQuery.OrderBy(u => u.FullName).ToListAsync();
                return View(message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Send message error: {ex.Message}");
                TempData["ErrorMessage"] = "Error sending message.";
                return RedirectToAction("Compose");
            }
        }

        // GET: Message/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .FirstOrDefaultAsync(m => m.MessageId == id);

            if (message == null)
            {
                return NotFound();
            }

            // Verify user has access to this message
            if (message.SenderId != userId && message.ReceiverId != userId)
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index");
            }

            // Mark as read if receiver is viewing
            if (message.ReceiverId == userId && !message.IsRead)
            {
                message.IsRead = true;
                message.ReadDate = DateTime.Now;
                _context.Update(message);
                await _context.SaveChangesAsync();
            }

            return View(message);
        }

        // POST: Message/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var message = await _context.Messages.FindAsync(id);
                if (message != null && (message.SenderId == userId || message.ReceiverId == userId))
                {
                    _context.Messages.Remove(message);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Message deleted successfully!";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Delete message error: {ex.Message}");
                TempData["ErrorMessage"] = "Error deleting message.";
                return RedirectToAction("Index");
            }
        }

        // GET: Message/Reply/5
        public async Task<IActionResult> Reply(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var originalMessage = await _context.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.MessageId == id && m.ReceiverId == userId);

            if (originalMessage == null)
            {
                return NotFound();
            }

            var reply = new Message
            {
                ReceiverId = originalMessage.SenderId,
                Subject = "Re: " + originalMessage.Subject
            };

            ViewBag.OriginalMessage = originalMessage;
            ViewBag.ReceiverName = originalMessage.Sender.FullName;

            return View(reply);
        }

        // POST: Message/Reply/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, Message message)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                ModelState.Remove("Sender");
                ModelState.Remove("Receiver");

                if (ModelState.IsValid)
                {
                    message.SenderId = userId.Value;
                    message.SentDate = DateTime.Now;
                    message.IsRead = false;

                    _context.Add(message);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Reply sent successfully!";
                    return RedirectToAction("Sent");
                }

                return RedirectToAction("Reply", new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reply error: {ex.Message}");
                TempData["ErrorMessage"] = "Error sending reply.";
                return RedirectToAction("Reply", new { id = id });
            }
        }
    }
}
