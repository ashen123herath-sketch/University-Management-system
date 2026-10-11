using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;

namespace UniManage.Controllers
{
    public class EnrollmentController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<EnrollmentController> _logger;

        public EnrollmentController(UniManageDbContext context, ILogger<EnrollmentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Enrollment/Browse
        public async Task<IActionResult> Browse(string department, string semester)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                TempData["ErrorMessage"] = "Only students can browse courses for enrollment.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Get all active courses
                var coursesQuery = _context.Courses
                    .Include(c => c.Lecturer)
                    .Include(c => c.Enrollments)
                    .Where(c => c.IsActive);

                // Apply filters
                if (!string.IsNullOrEmpty(department))
                {
                    coursesQuery = coursesQuery.Where(c => c.Department == department);
                }

                if (!string.IsNullOrEmpty(semester))
                {
                    coursesQuery = coursesQuery.Where(c => c.Semester == semester);
                }

                var courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();

                // Get student's enrolled course IDs
                var enrolledCourseIds = await _context.Enrollments
                    .Where(e => e.StudentId == userId && (e.Status == "Active" || e.Status == "Completed"))
                    .Select(e => e.CourseId)
                    .ToListAsync();

                ViewBag.EnrolledCourseIds = enrolledCourseIds;

                // Check for active enrollment to block further enrollment on the view
                var hasActiveEnrollment = await _context.Enrollments
                    .AnyAsync(e => e.StudentId == userId && e.Status == "Active");
                ViewBag.HasActiveEnrollment = hasActiveEnrollment;

                // Get distinct departments and semesters for filters
                ViewBag.Departments = await _context.Courses
                    .Where(c => c.IsActive)
                    .Select(c => c.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                ViewBag.Semesters = await _context.Courses
                    .Where(c => c.IsActive && c.Semester != null)
                    .Select(c => c.Semester)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync();

                return View(courses);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Browse courses error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading courses.";
                return RedirectToAction("StudentDashboard", "Dashboard");
            }
        }

        // POST: Enrollment/Enroll/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll(int courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                TempData["ErrorMessage"] = "Only students can enroll in courses.";
                return RedirectToAction("Browse");
            }

            try
            {
                // Check if student has any active enrollments
                var hasActiveEnrollment = await _context.Enrollments
                    .AnyAsync(e => e.StudentId == userId && e.Status == "Active");

                if (hasActiveEnrollment)
                {
                    TempData["ErrorMessage"] = "You must complete your current enrolled course before enrolling in another course.";
                    return RedirectToAction("Browse");
                }

                // Check if course exists
                var course = await _context.Courses
                    .Include(c => c.Enrollments)
                    .FirstOrDefaultAsync(c => c.CourseId == courseId && c.IsActive);

                if (course == null)
                {
                    TempData["ErrorMessage"] = "Course not found.";
                    return RedirectToAction("Browse");
                }

                // Check if already enrolled (in any status)
                var existingEnrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == userId);

                if (existingEnrollment != null)
                {
                    if (existingEnrollment.Status == "Active")
                    {
                        TempData["WarningMessage"] = "You are already enrolled in this course.";
                    }
                    else if (existingEnrollment.Status == "Completed")
                    {
                        TempData["WarningMessage"] = "You have already completed this course.";
                    }
                    else
                    {
                        TempData["WarningMessage"] = "You previously dropped this course. You cannot re-enroll.";
                    }
                    return RedirectToAction("Browse");
                }

                // Check enrollment limit
                var currentEnrollments = course.Enrollments?.Count(e => e.Status == "Active") ?? 0;
                if (currentEnrollments >= course.MaxStudents)
                {
                    TempData["ErrorMessage"] = "This course is full.";
                    return RedirectToAction("Browse");
                }

                // Create enrollment
                var enrollment = new Enrollment
                {
                    CourseId = courseId,
                    StudentId = userId.Value,
                    EnrollmentDate = DateTime.Now,
                    Status = "Active"
                };

                _context.Enrollments.Add(enrollment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Student {userId} enrolled in course {courseId}");
                TempData["SuccessMessage"] = "Successfully enrolled in the course!";
                return RedirectToAction("Browse");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Enrollment error: {ex.Message}");
                TempData["ErrorMessage"] = "Error enrolling in course.";
                return RedirectToAction("Browse");
            }
        }

        // POST: Enrollment/Drop/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Drop(int enrollmentId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                return Json(new { success = false, message = "Authentication required" });
            }

            try
            {
                var enrollment = await _context.Enrollments
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId && e.StudentId == userId);

                if (enrollment == null)
                {
                    return Json(new { success = false, message = "Enrollment not found" });
                }

                if (enrollment.Status == "Completed")
                {
                    return Json(new { success = false, message = "Cannot drop completed course" });
                }

                // Mark as dropped
                enrollment.Status = "Dropped";
                _context.Update(enrollment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Student {userId} dropped course {enrollment.CourseId}");
                return Json(new { success = true, message = "Course dropped successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Drop course error: {ex.Message}");
                return Json(new { success = false, message = "Error dropping course" });
            }
        }

        // GET: Enrollment/MyEnrollments
        public async Task<IActionResult> MyEnrollments()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var enrollments = await _context.Enrollments
                    .Include(e => e.Course)
                    .ThenInclude(c => c.Lecturer)
                    .Where(e => e.StudentId == userId)
                    .OrderByDescending(e => e.EnrollmentDate)
                    .ToListAsync();

                return View(enrollments);
            }
            catch (Exception ex)
            {
                _logger.LogError($"My enrollments error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading enrollments.";
                return RedirectToAction("StudentDashboard", "Dashboard");
            }
        }

        // GET: Enrollment/CourseStudents/5 (for lecturers)
        public async Task<IActionResult> CourseStudents(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                var course = await _context.Courses
                    .Include(c => c.Lecturer)
                    .FirstOrDefaultAsync(c => c.CourseId == id);

                if (course == null)
                {
                    return NotFound();
                }

                // Lecturers can only see their own courses
                if (userRole == "Lecturer" && course.LecturerId != userId)
                {
                    TempData["ErrorMessage"] = "Access denied.";
                    return RedirectToAction("Index", "Course");
                }

                var enrollments = await _context.Enrollments
                    .Include(e => e.Student)
                    .Where(e => e.CourseId == id)
                    .OrderBy(e => e.Student.FullName)
                    .ToListAsync();

                ViewBag.Course = course;
                return View(enrollments);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Course students error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading course students.";
                return RedirectToAction("Index", "Course");
            }
        }

        // GET: Enrollment/AddStudent/5
        public async Task<IActionResult> AddStudent(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("Index", "Home");
            }

            if (courseId == null)
            {
                return NotFound();
            }

            var course = await _context.Courses
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null)
            {
                return NotFound();
            }

            // For lecturers, verify they own this course
            if (userRole == "Lecturer" && course.LecturerId != userId)
            {
                TempData["ErrorMessage"] = "You can only add students to your own courses.";
                return RedirectToAction("Index", "Course");
            }

            ViewBag.Course = course;

            // Get students who are NOT already enrolled in this course
            var enrolledStudentIds = course.Enrollments?
                .Where(e => e.Status != "Dropped")
                .Select(e => e.StudentId)
                .ToList() ?? new List<int>();

            var availableStudents = await _context.Users
                .Where(u => u.Role == "Student" && u.IsActive && !enrolledStudentIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Students = availableStudents;

            return View();
        }

        // POST: Enrollment/AddStudent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(int courseId, int studentId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var course = await _context.Courses
                    .Include(c => c.Enrollments)
                    .FirstOrDefaultAsync(c => c.CourseId == courseId);

                if (course == null)
                {
                    TempData["ErrorMessage"] = "Course not found.";
                    return RedirectToAction("Index", "Course");
                }

                // For lecturers, verify they own this course
                if (userRole == "Lecturer" && course.LecturerId != userId)
                {
                    TempData["ErrorMessage"] = "Unauthorized access.";
                    return RedirectToAction("Index", "Course");
                }

                // Check if student already enrolled
                var existingEnrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);

                if (existingEnrollment != null && existingEnrollment.Status != "Dropped")
                {
                    TempData["WarningMessage"] = "Student is already enrolled in this course.";
                    return RedirectToAction("CourseStudents", new { id = courseId });
                }

                // Check enrollment limit
                var currentEnrollments = course.Enrollments?.Count(e => e.Status == "Active") ?? 0;
                if (currentEnrollments >= course.MaxStudents)
                {
                    TempData["ErrorMessage"] = "Course is at maximum capacity.";
                    return RedirectToAction("CourseStudents", new { id = courseId });
                }

                // Create or update enrollment
                if (existingEnrollment != null)
                {
                    // Re-activate dropped enrollment
                    existingEnrollment.Status = "Active";
                    existingEnrollment.EnrollmentDate = DateTime.Now;
                    _context.Update(existingEnrollment);
                }
                else
                {
                    // Create new enrollment
                    var enrollment = new Enrollment
                    {
                        CourseId = courseId,
                        StudentId = studentId,
                        EnrollmentDate = DateTime.Now,
                        Status = "Active"
                    };
                    _context.Enrollments.Add(enrollment);
                }

                await _context.SaveChangesAsync();

                var student = await _context.Users.FindAsync(studentId);
                _logger.LogInformation($"Student {student?.FullName} added to course {courseId} by {userRole} {userId}");
                TempData["SuccessMessage"] = $"Student {student?.FullName} successfully added to the course!";

                return RedirectToAction("CourseStudents", new { id = courseId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Add student error: {ex.Message}");
                TempData["ErrorMessage"] = "Error adding student to course.";
                return RedirectToAction("Index", "Course");
            }
        }

        // POST: Enrollment/AddStudentFromAdmin
        // Admin enrolls a student directly from the User Management page (bypasses active-course restriction)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudentFromAdmin(int studentId, int courseId)
        {
            var adminId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (adminId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index", "UserManagement", new { role = "Student" });
            }

            try
            {
                var course = await _context.Courses
                    .Include(c => c.Enrollments)
                    .FirstOrDefaultAsync(c => c.CourseId == courseId && c.IsActive);

                if (course == null)
                {
                    TempData["ErrorMessage"] = "Course not found or inactive.";
                    return RedirectToAction("Index", "UserManagement", new { role = "Student" });
                }

                var student = await _context.Users.FindAsync(studentId);
                if (student == null || student.Role != "Student")
                {
                    TempData["ErrorMessage"] = "Student not found.";
                    return RedirectToAction("Index", "UserManagement", new { role = "Student" });
                }

                // Check if already enrolled (active or completed)
                var existingEnrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);

                if (existingEnrollment != null && existingEnrollment.Status != "Dropped")
                {
                    TempData["WarningMessage"] = $"{student.FullName} is already enrolled (or has completed) this course.";
                    return RedirectToAction("Details", "UserManagement", new { id = studentId });
                }

                // Check capacity
                var activeCount = course.Enrollments?.Count(e => e.Status == "Active") ?? 0;
                if (activeCount >= course.MaxStudents)
                {
                    TempData["ErrorMessage"] = $"Course '{course.CourseName}' is at maximum capacity.";
                    return RedirectToAction("Details", "UserManagement", new { id = studentId });
                }

                // Admin can enroll even if student has an active enrollment elsewhere
                // (admin override — no restriction)
                if (existingEnrollment != null)
                {
                    existingEnrollment.Status = "Active";
                    existingEnrollment.EnrollmentDate = DateTime.Now;
                    _context.Update(existingEnrollment);
                }
                else
                {
                    _context.Enrollments.Add(new Enrollment
                    {
                        CourseId = courseId,
                        StudentId = studentId,
                        EnrollmentDate = DateTime.Now,
                        Status = "Active"
                    });
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin enrolled student {student.FullName} in course {course.CourseName}");
                TempData["SuccessMessage"] = $"{student.FullName} enrolled in '{course.CourseName}' successfully!";
                return RedirectToAction("Details", "UserManagement", new { id = studentId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddStudentFromAdmin error: {ex.Message}");
                TempData["ErrorMessage"] = "Error enrolling student.";
                return RedirectToAction("Index", "UserManagement", new { role = "Student" });
            }
        }
    }
}