using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;

namespace UniManage.Controllers
{
    public class UserManagementController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(UniManageDbContext context, ILogger<UserManagementController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("UserRole") == "Administrator"
                   && HttpContext.Session.GetInt32("UserId") != null;
        }

        // GET: UserManagement/Index?role=Lecturer|Student
        public async Task<IActionResult> Index(string role = "Student")
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            role = role == "Lecturer" ? "Lecturer" : "Student";

            var users = await _context.Users
                .Where(u => u.Role == role)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Role = role;
            ViewBag.TotalActive = users.Count(u => u.IsActive);
            ViewBag.TotalInactive = users.Count(u => !u.IsActive);

            // Pass all active courses for the enroll-in-course modal
            ViewBag.AllCourses = await _context.Courses
                .Where(c => c.IsActive)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            return View(users);
        }

        // GET: UserManagement/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Load enrollment info for students
            if (user.Role == "Student")
            {
                var enrollments = await _context.Enrollments
                    .Include(e => e.Course)
                    .Where(e => e.StudentId == id)
                    .OrderByDescending(e => e.EnrollmentDate)
                    .ToListAsync();
                ViewBag.Enrollments = enrollments;
            }

            // Load courses for lecturers
            if (user.Role == "Lecturer")
            {
                var courses = await _context.Courses
                    .Where(c => c.LecturerId == id)
                    .OrderBy(c => c.CourseName)
                    .ToListAsync();
                ViewBag.Courses = courses;
            }

            // Pass all active courses for the enroll modal (student details page)
            ViewBag.AllCourses = await _context.Courses
                .Where(c => c.IsActive)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            return View(user);
        }

        // GET: UserManagement/Create?role=Lecturer|Student
        public IActionResult Create(string role = "Student")
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            role = role == "Lecturer" ? "Lecturer" : "Student";
            ViewBag.Role = role;

            return View(new User { Role = role, IsActive = true });
        }

        // POST: UserManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user, string confirmPassword)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            // Only allow creating Lecturer or Student
            if (user.Role != "Lecturer" && user.Role != "Student")
            {
                ModelState.AddModelError("Role", "Invalid role. Only Lecturer or Student can be created here.");
            }

            // Check email uniqueness
            var emailExists = await _context.Users.AnyAsync(u => u.Email == user.Email);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "This email is already registered.");
            }

            // Validate password match
            if (string.IsNullOrEmpty(confirmPassword) || user.Password != confirmPassword)
            {
                ModelState.AddModelError("Password", "Password and confirm password do not match.");
            }

            if (user.Password?.Length < 6)
            {
                ModelState.AddModelError("Password", "Password must be at least 6 characters.");
            }

            if (ModelState.IsValid)
            {
                user.RegisteredDate = DateTime.Now;
                user.IsActive = true;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin created {user.Role}: {user.Email}");
                TempData["SuccessMessage"] = $"{user.Role} '{user.FullName}' created successfully!";
                return RedirectToAction(nameof(Index), new { role = user.Role });
            }

            ViewBag.Role = user.Role;
            return View(user);
        }

        // GET: UserManagement/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role != "Lecturer" && user.Role != "Student")
            {
                TempData["ErrorMessage"] = "You can only edit lecturers and students.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Role = user.Role;
            return View(user);
        }

        // POST: UserManagement/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User model, string? newPassword, string? confirmPassword)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role != "Lecturer" && user.Role != "Student")
            {
                TempData["ErrorMessage"] = "You can only edit lecturers and students.";
                return RedirectToAction(nameof(Index));
            }

            // Check email uniqueness (excluding current user)
            var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != id);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "This email is already registered by another user.");
            }

            // Password change is optional — only validate if provided
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (newPassword.Length < 6)
                {
                    ModelState.AddModelError("", "New password must be at least 6 characters.");
                }
                else if (newPassword != confirmPassword)
                {
                    ModelState.AddModelError("", "New password and confirm password do not match.");
                }
            }

            if (ModelState.IsValid)
            {
                user.FullName = model.FullName;
                user.Email = model.Email;
                user.PhoneNumber = model.PhoneNumber;
                user.Department = model.Department;
                user.IsActive = model.IsActive;

                if (!string.IsNullOrEmpty(newPassword))
                {
                    user.Password = newPassword;
                }

                _context.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin updated {user.Role}: {user.Email}");
                TempData["SuccessMessage"] = $"{user.Role} '{user.FullName}' updated successfully!";
                return RedirectToAction(nameof(Index), new { role = user.Role });
            }

            ViewBag.Role = user.Role;
            return View(model);
        }

        // POST: UserManagement/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role != "Lecturer" && user.Role != "Student")
            {
                TempData["ErrorMessage"] = "You can only manage lecturers and students.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;
            _context.Update(user);
            await _context.SaveChangesAsync();

            var status = user.IsActive ? "activated" : "deactivated";
            _logger.LogInformation($"Admin {status} {user.Role}: {user.Email}");
            TempData["SuccessMessage"] = $"{user.Role} '{user.FullName}' has been {status}.";
            return RedirectToAction(nameof(Index), new { role = user.Role });
        }

        // POST: UserManagement/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Role != "Lecturer" && user.Role != "Student")
            {
                TempData["ErrorMessage"] = "You can only delete lecturers and students.";
                return RedirectToAction(nameof(Index));
            }

            var role = user.Role;

            try
            {
                // Check for dependencies before deleting
                if (user.Role == "Student")
                {
                    var hasEnrollments = await _context.Enrollments.AnyAsync(e => e.StudentId == id);
                    if (hasEnrollments)
                    {
                        TempData["ErrorMessage"] = $"Cannot delete '{user.FullName}' — they have enrollment records. Deactivate them instead.";
                        return RedirectToAction(nameof(Index), new { role });
                    }
                }

                if (user.Role == "Lecturer")
                {
                    var hasCourses = await _context.Courses.AnyAsync(c => c.LecturerId == id);
                    if (hasCourses)
                    {
                        TempData["ErrorMessage"] = $"Cannot delete '{user.FullName}' — they are assigned to courses. Deactivate them instead.";
                        return RedirectToAction(nameof(Index), new { role });
                    }
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin deleted {role}: {user.Email}");
                TempData["SuccessMessage"] = $"{role} '{user.FullName}' deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Delete user error: {ex.Message}");
                TempData["ErrorMessage"] = $"Error deleting user. Deactivate them instead.";
            }

            return RedirectToAction(nameof(Index), new { role });
        }
    }
}