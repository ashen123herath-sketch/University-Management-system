using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;
using UniManage.Models.ViewModels;

namespace UniManage.Controllers
{
    public class DashboardController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(UniManageDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Dashboard/StudentDashboard
        public async Task<IActionResult> StudentDashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var student = await _context.Users.FindAsync(userId);
                
                // Get enrolled courses
                var enrolledCourses = await _context.Enrollments
                    .Include(e => e.Course)
                    .ThenInclude(c => c.Lecturer)
                    .Where(e => e.StudentId == userId && e.Status == "Active")
                    .ToListAsync();

                // Get upcoming assignments
                var courseIds = enrolledCourses.Select(e => e.CourseId).ToList();
                var upcomingAssignments = await _context.Assignments
                    .Include(a => a.Course)
                    .Where(a => courseIds.Contains(a.CourseId) && a.DueDate > DateTime.Now && a.IsActive)
                    .OrderBy(a => a.DueDate)
                    .Take(5)
                    .ToListAsync();

                // Get recent submissions
                var recentSubmissions = await _context.Submissions
                    .Include(s => s.Assignment)
                    .ThenInclude(a => a.Course)
                    .Where(s => s.StudentId == userId)
                    .OrderByDescending(s => s.SubmissionDate)
                    .Take(5)
                    .ToListAsync();

                // Calculate average grade
                var gradedEnrollments = enrolledCourses.Where(e => e.FinalGrade.HasValue).ToList();
                var averageGrade = gradedEnrollments.Any() 
                    ? gradedEnrollments.Average(e => e.FinalGrade.Value) 
                    : 0;

                // Count unread messages
                var unreadMessages = await _context.Messages
                    .CountAsync(m => m.ReceiverId == userId && !m.IsRead);

                // Count pending assignments
                var submittedAssignmentIds = await _context.Submissions
                    .Where(s => s.StudentId == userId)
                    .Select(s => s.AssignmentId)
                    .ToListAsync();

                var pendingAssignments = await _context.Assignments
                    .CountAsync(a => courseIds.Contains(a.CourseId) 
                        && !submittedAssignmentIds.Contains(a.AssignmentId) 
                        && a.DueDate > DateTime.Now 
                        && a.IsActive);

                var viewModel = new StudentDashboardViewModel
                {
                    Student = student,
                    EnrolledCourses = enrolledCourses,
                    UpcomingAssignments = upcomingAssignments,
                    RecentSubmissions = recentSubmissions,
                    TotalEnrollments = enrolledCourses.Count,
                    PendingAssignments = pendingAssignments,
                    AverageGrade = Math.Round(averageGrade, 2),
                    UnreadMessages = unreadMessages
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Student dashboard error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading dashboard";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Dashboard/LecturerDashboard
        public async Task<IActionResult> LecturerDashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Lecturer")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var lecturer = await _context.Users.FindAsync(userId);

                // Get teaching courses
                var teachingCourses = await _context.Courses
                    .Include(c => c.Enrollments)
                    .Include(c => c.Assignments)
                        .ThenInclude(a => a.Submissions)
                    .Where(c => c.LecturerId == userId && c.IsActive)
                    .ToListAsync();

                var courseIds = teachingCourses.Select(c => c.CourseId).ToList();

                // Get recent assignments
                var recentAssignments = await _context.Assignments
                    .Include(a => a.Course)
                    .Where(a => courseIds.Contains(a.CourseId) && a.IsActive)
                    .OrderByDescending(a => a.CreatedDate)
                    .Take(5)
                    .ToListAsync();

                // Get pending grading submissions
                var pendingGrading = await _context.Submissions
                    .Include(s => s.Assignment)
                    .ThenInclude(a => a.Course)
                    .Include(s => s.Student)
                    .Where(s => courseIds.Contains(s.Assignment.CourseId) 
                        && !s.MarksObtained.HasValue 
                        && s.Status == "Submitted")
                    .OrderBy(s => s.SubmissionDate)
                    .Take(10)
                    .ToListAsync();

                // Count total students across all courses
                var totalStudents = await _context.Enrollments
                    .Where(e => courseIds.Contains(e.CourseId) && e.Status == "Active")
                    .Select(e => e.StudentId)
                    .Distinct()
                    .CountAsync();

                // Count unread messages
                var unreadMessages = await _context.Messages
                    .CountAsync(m => m.ReceiverId == userId && !m.IsRead);

                var viewModel = new LecturerDashboardViewModel
                {
                    Lecturer = lecturer,
                    TeachingCourses = teachingCourses,
                    RecentAssignments = recentAssignments,
                    PendingGrading = pendingGrading,
                    TotalCourses = teachingCourses.Count,
                    TotalStudents = totalStudents,
                    PendingSubmissions = pendingGrading.Count,
                    UnreadMessages = unreadMessages
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lecturer dashboard error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading dashboard";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Dashboard/AdminDashboard
        public async Task<IActionResult> AdminDashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var admin = await _context.Users.FindAsync(userId);

                // Get statistics
                var totalUsers = await _context.Users.Where(u => u.IsActive).CountAsync();
                var totalStudents = await _context.Users.Where(u => u.Role == "Student" && u.IsActive).CountAsync();
                var totalLecturers = await _context.Users.Where(u => u.Role == "Lecturer" && u.IsActive).CountAsync();
                var totalCourses = await _context.Courses.Where(c => c.IsActive).CountAsync();
                var activeEnrollments = await _context.Enrollments.Where(e => e.Status == "Active").CountAsync();
                var totalAssignments = await _context.Assignments.Where(a => a.IsActive).CountAsync();

                // Get recent courses
                var recentCourses = await _context.Courses
                    .Include(c => c.Lecturer)
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(5)
                    .ToListAsync();

                // Get recent enrollments
                var recentEnrollments = await _context.Enrollments
                    .Include(e => e.Course)
                    .Include(e => e.Student)
                    .OrderByDescending(e => e.EnrollmentDate)
                    .Take(5)
                    .ToListAsync();

                // Get enrollments by department
                var enrollmentsByDepartment = await _context.Enrollments
                    .Include(e => e.Course)
                    .Where(e => e.Status == "Active")
                    .GroupBy(e => e.Course.Department)
                    .Select(g => new { Department = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Department, x => x.Count);

                // Get course popularity (top 5 courses by enrollment)
                var coursePopularity = await _context.Enrollments
                    .Include(e => e.Course)
                    .Where(e => e.Status == "Active")
                    .GroupBy(e => e.Course.CourseName)
                    .Select(g => new { CourseName = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToDictionaryAsync(x => x.CourseName, x => x.Count);

                var viewModel = new AdminDashboardViewModel
                {
                    Admin = admin,
                    TotalUsers = totalUsers,
                    TotalStudents = totalStudents,
                    TotalLecturers = totalLecturers,
                    TotalCourses = totalCourses,
                    ActiveEnrollments = activeEnrollments,
                    TotalAssignments = totalAssignments,
                    RecentCourses = recentCourses,
                    RecentEnrollments = recentEnrollments,
                    EnrollmentsByDepartment = enrollmentsByDepartment,
                    CoursePopularity = coursePopularity
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Admin dashboard error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading dashboard";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
