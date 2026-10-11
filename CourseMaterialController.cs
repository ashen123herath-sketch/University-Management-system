using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;

namespace UniManage.Controllers
{
    public class CourseMaterialController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<CourseMaterialController> _logger;
        private readonly IWebHostEnvironment _environment;

        public CourseMaterialController(UniManageDbContext context, ILogger<CourseMaterialController> logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // GET: CourseMaterial/Index
        public async Task<IActionResult> Index(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                IQueryable<CourseMaterial> materialsQuery = _context.CourseMaterials
                    .Include(m => m.Course)
                    .Include(m => m.Uploader);

                if (userRole == "Lecturer")
                {
                    // Show materials for lecturer's courses
                    var lecturerCourseIds = await _context.Courses
                        .Where(c => c.LecturerId == userId)
                        .Select(c => c.CourseId)
                        .ToListAsync();
                    materialsQuery = materialsQuery.Where(m => lecturerCourseIds.Contains(m.CourseId));
                }
                else if (userRole == "Student")
                {
                    // Show materials for enrolled courses
                    var enrolledCourseIds = await _context.Enrollments
                        .Where(e => e.StudentId == userId && e.Status == "Active")
                        .Select(e => e.CourseId)
                        .ToListAsync();
                    materialsQuery = materialsQuery.Where(m => enrolledCourseIds.Contains(m.CourseId));
                }
                // Administrator sees all materials

                if (courseId.HasValue)
                {
                    materialsQuery = materialsQuery.Where(m => m.CourseId == courseId.Value);
                }

                var materials = await materialsQuery
                    .OrderByDescending(m => m.UploadedDate)
                    .ToListAsync();

                // Get courses for filter dropdown
                var coursesQuery = _context.Courses.Where(c => c.IsActive);
                if (userRole == "Lecturer")
                {
                    coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
                }
                else if (userRole == "Student")
                {
                    var enrolledIds = await _context.Enrollments
                        .Where(e => e.StudentId == userId && e.Status == "Active")
                        .Select(e => e.CourseId)
                        .ToListAsync();
                    coursesQuery = coursesQuery.Where(c => enrolledIds.Contains(c.CourseId));
                }
                ViewBag.Courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();
                ViewBag.SelectedCourseId = courseId;

                return View(materials);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Course materials index error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading course materials.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: CourseMaterial/Create
        public async Task<IActionResult> Create(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
            {
                TempData["ErrorMessage"] = "Only lecturers and administrators can upload course materials.";
                return RedirectToAction("Index");
            }

            // Get relevant courses
            IQueryable<Course> coursesQuery = _context.Courses.Where(c => c.IsActive);
            if (userRole == "Lecturer")
            {
                coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
            }
            ViewBag.Courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();

            var material = new CourseMaterial();
            if (courseId.HasValue)
            {
                material.CourseId = courseId.Value;
            }

            ViewBag.PreselectedCourseId = courseId;
            return View(material);
        }

        // POST: CourseMaterial/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CourseMaterial material, IFormFile? file)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                ModelState.Remove("Course");
                ModelState.Remove("Uploader");
                ModelState.Remove("FilePath");
                ModelState.Remove("FileType");

                // For lecturer, verify ownership; admin can upload to any course
                var course = await _context.Courses.FindAsync(material.CourseId);
                if (course == null)
                {
                    TempData["ErrorMessage"] = "Invalid course selected.";
                    return RedirectToAction("Index");
                }
                if (userRole == "Lecturer" && course.LecturerId != userId)
                {
                    TempData["ErrorMessage"] = "Invalid course selected.";
                    return RedirectToAction("Index");
                }

                // Handle file upload
                if (file != null && file.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "materials");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    material.FilePath = $"/uploads/materials/{uniqueFileName}";
                    material.FileType = Path.GetExtension(file.FileName);
                }

                if (ModelState.IsValid)
                {
                    material.UploadedBy = userId.Value;
                    material.UploadedDate = DateTime.Now;

                    _context.Add(material);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Course material uploaded: {material.Title}");
                    TempData["SuccessMessage"] = "Course material uploaded successfully!";
                    return RedirectToAction(nameof(Index), new { courseId = material.CourseId });
                }

                IQueryable<Course> coursesQuery = _context.Courses.Where(c => c.IsActive);
                if (userRole == "Lecturer")
                {
                    coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
                }
                ViewBag.Courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();

                return View(material);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Course material upload error: {ex.Message}");
                TempData["ErrorMessage"] = "Error uploading course material.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: CourseMaterial/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var material = await _context.CourseMaterials
                .Include(m => m.Course)
                .Include(m => m.Uploader)
                .FirstOrDefaultAsync(m => m.MaterialId == id);

            if (material == null)
            {
                return NotFound();
            }

            return View(material);
        }

        // POST: CourseMaterial/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var material = await _context.CourseMaterials
                    .Include(m => m.Course)
                    .FirstOrDefaultAsync(m => m.MaterialId == id);

                if (material != null)
                {
                    // Lecturers can only delete their own course materials; admin can delete any
                    bool canDelete = userRole == "Administrator" || material.Course.LecturerId == userId;

                    if (canDelete)
                    {
                        // Delete file from server
                        if (!string.IsNullOrEmpty(material.FilePath))
                        {
                            var filePath = Path.Combine(_environment.WebRootPath, material.FilePath.TrimStart('/'));
                            if (System.IO.File.Exists(filePath))
                            {
                                System.IO.File.Delete(filePath);
                            }
                        }

                        _context.CourseMaterials.Remove(material);
                        await _context.SaveChangesAsync();

                        TempData["SuccessMessage"] = "Course material deleted successfully!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "You are not authorized to delete this material.";
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Delete course material error: {ex.Message}");
                TempData["ErrorMessage"] = "Error deleting course material.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: CourseMaterial/Download/5
        public async Task<IActionResult> Download(int? id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null)
            {
                return NotFound();
            }

            var material = await _context.CourseMaterials.FindAsync(id);

            if (material == null || string.IsNullOrEmpty(material.FilePath))
            {
                TempData["ErrorMessage"] = "File not found.";
                return RedirectToAction(nameof(Index));
            }

            var filePath = Path.Combine(_environment.WebRootPath, material.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                TempData["ErrorMessage"] = "File not found on server.";
                return RedirectToAction(nameof(Index));
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            var contentType = "application/octet-stream";
            var fileName = Path.GetFileName(filePath);

            return File(memory, contentType, fileName);
        }
    }
}
