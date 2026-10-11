using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;

namespace UniManage.Controllers
{
    public class CourseController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<CourseController> _logger;

        public CourseController(UniManageDbContext context, ILogger<CourseController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Course/Index
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userRole = HttpContext.Session.GetString("UserRole");

            // Always filter to show only active courses
            IQueryable<Course> coursesQuery = _context.Courses
                .Include(c => c.Lecturer)
                .Include(c => c.Enrollments)
                .Where(c => c.IsActive); // Only show active courses

            // Additional filtering based on role
            if (userRole == "Lecturer")
            {
                coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
            }

            var courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();
            return View(courses);
        }

        // GET: Course/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var course = await _context.Courses
                .Include(c => c.Lecturer)
                .Include(c => c.Enrollments)
                .Include(c => c.Assignments)
                .Include(c => c.CourseMaterials)
                    .ThenInclude(cm => cm.Uploader)
                .FirstOrDefaultAsync(m => m.CourseId == id);

            if (course == null)
            {
                return NotFound();
            }

            return View(course);
        }

        // GET: Course/Create
        public async Task<IActionResult> Create()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Only administrators can create courses.";
                return RedirectToAction("Index");
            }

            // Get all lecturers for dropdown
            ViewBag.Lecturers = await _context.Users
                .Where(u => u.Role == "Lecturer" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View();
        }

        // POST: Course/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Course course)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                if (userId == null || userRole != "Administrator")
                {
                    return RedirectToAction("Login", "Account");
                }

                // Remove validation errors for navigation properties
                ModelState.Remove("Lecturer");
                ModelState.Remove("Enrollments");
                ModelState.Remove("Assignments");
                ModelState.Remove("CourseMaterials");

                if (ModelState.IsValid)
                {
                    // Check if course code already exists
                    var existingCourse = await _context.Courses
                        .FirstOrDefaultAsync(c => c.CourseCode == course.CourseCode);
                    
                    if (existingCourse != null)
                    {
                        ModelState.AddModelError("CourseCode", "Course code already exists");
                        ViewBag.Lecturers = await _context.Users
                            .Where(u => u.Role == "Lecturer" && u.IsActive)
                            .OrderBy(u => u.FullName)
                            .ToListAsync();
                        return View(course);
                    }

                    course.CreatedDate = DateTime.Now;
                    course.IsActive = true;

                    _context.Add(course);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Course created: {course.CourseName}");
                    TempData["SuccessMessage"] = "Course created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Lecturers = await _context.Users
                    .Where(u => u.Role == "Lecturer" && u.IsActive)
                    .OrderBy(u => u.FullName)
                    .ToListAsync();
                return View(course);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Course creation error: {ex.Message}");
                TempData["ErrorMessage"] = "Error creating course.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Course/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Only administrators can edit courses.";
                return RedirectToAction("Index");
            }

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }

            ViewBag.Lecturers = await _context.Users
                .Where(u => u.Role == "Lecturer" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(course);
        }

        // POST: Course/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Course course)
        {
            if (id != course.CourseId)
            {
                return NotFound();
            }

            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                if (userId == null || userRole != "Administrator")
                {
                    return RedirectToAction("Login", "Account");
                }

                ModelState.Remove("Lecturer");
                ModelState.Remove("Enrollments");
                ModelState.Remove("Assignments");
                ModelState.Remove("CourseMaterials");
                ModelState.Remove("CourseCode"); // Remove CourseCode validation

                if (ModelState.IsValid)
                {
                    // Get the existing course from database
                    var existingCourse = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == id);
                    if (existingCourse == null)
                    {
                        TempData["ErrorMessage"] = "Course not found.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Preserve the original course code, created date, and active status
                    course.CourseCode = existingCourse.CourseCode;
                    course.CreatedDate = existingCourse.CreatedDate;
                    course.IsActive = existingCourse.IsActive;

                    _context.Update(course);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Course updated: {course.CourseName}");
                    TempData["SuccessMessage"] = "Course updated successfully!";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Lecturers = await _context.Users
                    .Where(u => u.Role == "Lecturer" && u.IsActive)
                    .OrderBy(u => u.FullName)
                    .ToListAsync();
                return View(course);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CourseExists(course.CourseId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Course update error: {ex.Message}");
                TempData["ErrorMessage"] = "Error updating course.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Course/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Only administrators can delete courses.";
                return RedirectToAction("Index");
            }

            var course = await _context.Courses
                .Include(c => c.Lecturer)
                .FirstOrDefaultAsync(m => m.CourseId == id);

            if (course == null)
            {
                return NotFound();
            }

            return View(course);
        }

        // POST: Course/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                if (userId == null || userRole != "Administrator")
                {
                    return RedirectToAction("Login", "Account");
                }

                var course = await _context.Courses.FindAsync(id);
                if (course != null)
                {
                    // Soft delete - mark as inactive
                    course.IsActive = false;
                    _context.Update(course);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Course deleted: {course.CourseName}");
                    TempData["SuccessMessage"] = "Course deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Course deletion error: {ex.Message}");
                TempData["ErrorMessage"] = "Error deleting course.";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool CourseExists(int id)
        {
            return _context.Courses.Any(e => e.CourseId == id);
        }

        // GET: Course/MyCourses (for students)
        public async Task<IActionResult> MyCourses()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                return RedirectToAction("Login", "Account");
            }

            var enrolledCourses = await _context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Lecturer)
                .Where(e => e.StudentId == userId)
                .OrderByDescending(e => e.EnrollmentDate)
                .ToListAsync();

            return View(enrolledCourses);
        }
    }
}
