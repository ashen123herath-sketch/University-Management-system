using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;

namespace UniManage.Controllers
{
    public class AssignmentController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<AssignmentController> _logger;
        private readonly IWebHostEnvironment _environment;

        public AssignmentController(UniManageDbContext context, ILogger<AssignmentController> logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // GET: Assignment/Index
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
                IQueryable<Assignment> assignmentsQuery = _context.Assignments.Include(a => a.Course);

                if (userRole == "Lecturer")
                {
                    // Show assignments for lecturer's courses
                    var lecturerCourseIds = await _context.Courses
                        .Where(c => c.LecturerId == userId)
                        .Select(c => c.CourseId)
                        .ToListAsync();
                    assignmentsQuery = assignmentsQuery.Where(a => lecturerCourseIds.Contains(a.CourseId));
                }
                else if (userRole == "Student")
                {
                    // Show assignments for enrolled courses
                    var enrolledCourseIds = await _context.Enrollments
                        .Where(e => e.StudentId == userId && e.Status == "Active")
                        .Select(e => e.CourseId)
                        .ToListAsync();
                    assignmentsQuery = assignmentsQuery.Where(a => enrolledCourseIds.Contains(a.CourseId));
                }
                // Admin sees all assignments

                if (courseId.HasValue)
                {
                    assignmentsQuery = assignmentsQuery.Where(a => a.CourseId == courseId);
                }

                var assignments = await assignmentsQuery
                    .OrderByDescending(a => a.CreatedDate)
                    .ToListAsync();

                return View(assignments);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Assignment index error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading assignments.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Assignment/Create
        public async Task<IActionResult> Create(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
            {
                TempData["ErrorMessage"] = "Only lecturers and administrators can create assignments.";
                return RedirectToAction("Index");
            }

            // Get relevant courses
            IQueryable<Course> coursesQuery = _context.Courses.Where(c => c.IsActive);
            if (userRole == "Lecturer")
            {
                coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
            }
            ViewBag.Courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();

            var assignment = new Assignment();
            if (courseId.HasValue)
            {
                assignment.CourseId = courseId.Value;
            }

            return View(assignment);
        }

        // POST: Assignment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Assignment assignment, IFormFile? file)
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
                ModelState.Remove("Submissions");
                ModelState.Remove("FilePath");
                ModelState.Remove("FileType");

                // For admin, allow any active course; for lecturer, verify ownership
                var course = await _context.Courses.FindAsync(assignment.CourseId);
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
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "assignments");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    assignment.FilePath = $"/uploads/assignments/{uniqueFileName}";
                    assignment.FileType = Path.GetExtension(file.FileName);
                }

                if (ModelState.IsValid)
                {
                    assignment.CreatedDate = DateTime.Now;
                    assignment.IsActive = true;

                    _context.Add(assignment);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Assignment created: {assignment.Title}");
                    TempData["SuccessMessage"] = "Assignment created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                IQueryable<Course> coursesQuery = _context.Courses.Where(c => c.IsActive);
                if (userRole == "Lecturer")
                {
                    coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
                }
                ViewBag.Courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();

                return View(assignment);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Assignment creation error: {ex.Message}");
                TempData["ErrorMessage"] = "Error creating assignment.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Assignment/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignment = await _context.Assignments
                .Include(a => a.Course)
                .ThenInclude(c => c.Lecturer)
                .Include(a => a.Submissions)
                .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(m => m.AssignmentId == id);

            if (assignment == null)
            {
                return NotFound();
            }

            return View(assignment);
        }

        // GET: Assignment/DownloadAssignmentFile/5  – download lecturer-attached file
        public async Task<IActionResult> DownloadAssignmentFile(int? id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            if (id == null) return NotFound();

            var assignment = await _context.Assignments.FindAsync(id);

            if (assignment == null || string.IsNullOrEmpty(assignment.FilePath))
            {
                TempData["ErrorMessage"] = "File not found.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var filePath = Path.Combine(_environment.WebRootPath, assignment.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                TempData["ErrorMessage"] = "File not found on server.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/octet-stream", Path.GetFileName(filePath));
        }

        // GET: Assignment/DownloadSubmissionFile/5  – download student submission file
        public async Task<IActionResult> DownloadSubmissionFile(int? id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null) return RedirectToAction("Login", "Account");

            // Only lecturer, admin, or the submitting student can download
            if (userRole != "Lecturer" && userRole != "Administrator" && userRole != "Student")
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index");
            }

            if (id == null) return NotFound();

            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a.Course)
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null || string.IsNullOrEmpty(submission.FilePath))
            {
                TempData["ErrorMessage"] = "File not found.";
                return RedirectToAction("Index");
            }

            // Students can only download their own submission files
            if (userRole == "Student" && submission.StudentId != userId)
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index");
            }

            var filePath = Path.Combine(_environment.WebRootPath, submission.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                TempData["ErrorMessage"] = "File not found on server.";
                return RedirectToAction("Index");
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/octet-stream", Path.GetFileName(filePath));
        }

        // GET: Assignment/Submit/5
        public async Task<IActionResult> Submit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                TempData["ErrorMessage"] = "Only students can submit assignments.";
                return RedirectToAction("Index");
            }

            var assignment = await _context.Assignments
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null)
            {
                return NotFound();
            }

            // Check if already submitted
            var existingSubmission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == userId);

            if (existingSubmission != null)
            {
                TempData["WarningMessage"] = "You have already submitted this assignment.";
                return RedirectToAction("Details", new { id = id });
            }

            ViewBag.Assignment = assignment;
            return View();
        }

        // POST: Assignment/Submit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id, string comments, IFormFile? file)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var assignment = await _context.Assignments.FindAsync(id);
                if (assignment == null)
                {
                    return NotFound();
                }

                // Check if already submitted
                var existingSubmission = await _context.Submissions
                    .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == userId);

                if (existingSubmission != null)
                {
                    TempData["ErrorMessage"] = "You have already submitted this assignment.";
                    return RedirectToAction("Details", new { id = id });
                }

                var submission = new Submission
                {
                    AssignmentId = id,
                    StudentId = userId.Value,
                    SubmissionDate = DateTime.Now,
                    Comments = comments,
                    Status = DateTime.Now > assignment.DueDate ? "Late" : "Submitted"
                };

                // Handle file upload
                if (file != null && file.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "submissions");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    submission.FilePath = $"/uploads/submissions/{uniqueFileName}";
                }

                _context.Submissions.Add(submission);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Assignment submitted: {id} by student {userId}");
                TempData["SuccessMessage"] = "Assignment submitted successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Submission error: {ex.Message}");
                TempData["ErrorMessage"] = "Error submitting assignment.";
                return RedirectToAction("Submit", new { id = id });
            }
        }

        // GET: Assignment/Grade/5
        public async Task<IActionResult> Grade(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
            {
                TempData["ErrorMessage"] = "Only lecturers and administrators can grade assignments.";
                return RedirectToAction("Index");
            }

            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a.Course)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null)
            {
                return NotFound();
            }

            // Lecturers can only grade their own courses; admin can grade any
            if (userRole == "Lecturer" && submission.Assignment.Course.LecturerId != userId)
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index");
            }

            return View(submission);
        }

        // POST: Assignment/Grade/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grade(int id, decimal marksObtained, string? feedback)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
                {
                    return RedirectToAction("Login", "Account");
                }

                var submission = await _context.Submissions
                    .Include(s => s.Assignment)
                        .ThenInclude(a => a.Course)
                    .Include(s => s.Student)
                    .FirstOrDefaultAsync(s => s.SubmissionId == id);

                if (submission == null)
                {
                    TempData["ErrorMessage"] = "Submission not found.";
                    return RedirectToAction("Index");
                }

                // Lecturers can only grade their own courses; admin can grade any
                if (userRole == "Lecturer" && submission.Assignment.Course.LecturerId != userId)
                {
                    TempData["ErrorMessage"] = "Unauthorized access.";
                    return RedirectToAction("Index");
                }

                // Update submission with grade
                submission.MarksObtained = (double)marksObtained;
                submission.Feedback = feedback;
                submission.Status = "Graded";

                await _context.SaveChangesAsync();

                // Update enrollment final grade
                await UpdateEnrollmentGrade(submission.StudentId, submission.Assignment.CourseId);

                _logger.LogInformation($"Assignment graded: {submission.SubmissionId}");
                TempData["SuccessMessage"] = "Grade submitted successfully!";
                return RedirectToAction("Details", new { id = submission.AssignmentId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Grading error: {ex.Message}");
                TempData["ErrorMessage"] = "Error submitting grade.";
                return RedirectToAction("Index");
            }
        }

        // Helper method to update enrollment final grade
        private async Task UpdateEnrollmentGrade(int studentId, int courseId)
        {
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId);

            if (enrollment != null)
            {
                var submissions = await _context.Submissions
                    .Include(s => s.Assignment)
                    .Where(s => s.StudentId == studentId &&
                               s.Assignment.CourseId == courseId &&
                               s.MarksObtained.HasValue)
                    .ToListAsync();

                if (submissions.Any())
                {
                    var totalPercentage = submissions.Sum(s =>
                        (s.MarksObtained.Value / s.Assignment.MaxMarks) * 100);
                    var averageGrade = totalPercentage / submissions.Count;

                    enrollment.FinalGrade = averageGrade;

                    enrollment.LetterGrade = averageGrade >= 90 ? "A" :
                                            averageGrade >= 80 ? "B" :
                                            averageGrade >= 70 ? "C" :
                                            averageGrade >= 60 ? "D" : "F";

                    await _context.SaveChangesAsync();
                }
            }
        }

        // GET: Assignment/MySubmissions
        public async Task<IActionResult> MySubmissions()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Student")
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var submissions = await _context.Submissions
                    .Include(s => s.Assignment)
                    .ThenInclude(a => a.Course)
                    .Where(s => s.StudentId == userId)
                    .OrderByDescending(s => s.SubmissionDate)
                    .ToListAsync();

                return View(submissions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"My submissions error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading submissions.";
                return RedirectToAction("StudentDashboard", "Dashboard");
            }
        }

        // GET: Assignment/AllSubmissions — Admin view of all submissions
        public async Task<IActionResult> AllSubmissions(int? assignmentId, int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index");
            }

            try
            {
                IQueryable<Submission> submissionsQuery = _context.Submissions
                    .Include(s => s.Assignment)
                    .ThenInclude(a => a.Course)
                    .Include(s => s.Student);

                if (assignmentId.HasValue)
                {
                    submissionsQuery = submissionsQuery.Where(s => s.AssignmentId == assignmentId.Value);
                }

                if (courseId.HasValue)
                {
                    submissionsQuery = submissionsQuery.Where(s => s.Assignment.CourseId == courseId.Value);
                }

                var submissions = await submissionsQuery
                    .OrderByDescending(s => s.SubmissionDate)
                    .ToListAsync();

                ViewBag.Courses = await _context.Courses
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.CourseName)
                    .ToListAsync();

                ViewBag.Assignments = await _context.Assignments
                    .Include(a => a.Course)
                    .OrderByDescending(a => a.CreatedDate)
                    .ToListAsync();

                ViewBag.SelectedAssignmentId = assignmentId;
                ViewBag.SelectedCourseId = courseId;

                return View(submissions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"All submissions error: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading submissions.";
                return RedirectToAction("Index");
            }
        }

        // GET: Assignment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
            {
                TempData["ErrorMessage"] = "Only lecturers and administrators can edit assignments.";
                return RedirectToAction("Index");
            }

            var assignment = await _context.Assignments
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null)
            {
                return NotFound();
            }

            if (userRole == "Lecturer" && assignment.Course.LecturerId != userId)
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return RedirectToAction("Index");
            }

            return View(assignment);
        }

        // POST: Assignment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Assignment assignment, IFormFile? file)
        {
            if (id != assignment.AssignmentId)
            {
                return NotFound();
            }

            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");

                if (userId == null || (userRole != "Lecturer" && userRole != "Administrator"))
                {
                    return RedirectToAction("Login", "Account");
                }

                ModelState.Remove("Course");
                ModelState.Remove("Submissions");
                ModelState.Remove("FilePath");
                ModelState.Remove("FileType");

                if (ModelState.IsValid)
                {
                    var existingAssignment = await _context.Assignments
                        .Include(a => a.Course)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.AssignmentId == id);

                    if (existingAssignment == null)
                    {
                        TempData["ErrorMessage"] = "Assignment not found.";
                        return RedirectToAction("Index");
                    }

                    if (userRole == "Lecturer" && existingAssignment.Course.LecturerId != userId)
                    {
                        TempData["ErrorMessage"] = "Unauthorized access.";
                        return RedirectToAction("Index");
                    }

                    // Preserve created date and existing file if no new file uploaded
                    assignment.CreatedDate = existingAssignment.CreatedDate;
                    assignment.FilePath = existingAssignment.FilePath;
                    assignment.FileType = existingAssignment.FileType;

                    // Handle new file upload
                    if (file != null && file.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "assignments");
                        Directory.CreateDirectory(uploadsFolder);

                        var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        assignment.FilePath = $"/uploads/assignments/{uniqueFileName}";
                        assignment.FileType = Path.GetExtension(file.FileName);
                    }

                    _context.Update(assignment);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Assignment updated: {assignment.Title}");
                    TempData["SuccessMessage"] = "Assignment updated successfully!";
                    return RedirectToAction("Details", new { id = assignment.AssignmentId });
                }

                assignment.Course = await _context.Courses.FindAsync(assignment.CourseId);
                return View(assignment);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Assignment update error: {ex.Message}");
                TempData["ErrorMessage"] = "Error updating assignment.";
                return RedirectToAction("Index");
            }
        }
    }
}
