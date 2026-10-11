using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models.Data;
using ClosedXML.Excel;

namespace UniManage.Controllers
{
    public class ReportController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<ReportController> _logger;

        public ReportController(UniManageDbContext context, ILogger<ReportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Report/Index
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (userRole != "Administrator" && userRole != "Lecturer")
            {
                TempData["ErrorMessage"] = "Access denied.";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // GET: Report/CoursePopularity
        public async Task<IActionResult> CoursePopularity()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var courseStats = await _context.Courses
                    .Include(c => c.Lecturer)
                    .Include(c => c.Enrollments)
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        Course = c,
                        EnrollmentCount = c.Enrollments.Count(e => e.Status == "Active"),
                        CompletedCount = c.Enrollments.Count(e => e.Status == "Completed"),
                        DroppedCount = c.Enrollments.Count(e => e.Status == "Dropped"),
                        AvailableSeats = c.MaxStudents - c.Enrollments.Count(e => e.Status == "Active")
                    })
                    .OrderByDescending(x => x.EnrollmentCount)
                    .ToListAsync();

                return View(courseStats);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Course popularity report error: {ex.Message}");
                TempData["ErrorMessage"] = "Error generating report.";
                return RedirectToAction("Index");
            }
        }

        // GET: Report/StudentPerformance
        public async Task<IActionResult> StudentPerformance(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                IQueryable<Models.Enrollment> enrollmentsQuery = _context.Enrollments
                    .Include(e => e.Student)
                    .Include(e => e.Course);

                if (userRole == "Lecturer")
                {
                    // Show only lecturer's courses
                    var lecturerCourseIds = await _context.Courses
                        .Where(c => c.LecturerId == userId)
                        .Select(c => c.CourseId)
                        .ToListAsync();
                    enrollmentsQuery = enrollmentsQuery.Where(e => lecturerCourseIds.Contains(e.CourseId));
                }

                if (courseId.HasValue)
                {
                    enrollmentsQuery = enrollmentsQuery.Where(e => e.CourseId == courseId);
                }

                var enrollments = await enrollmentsQuery
                    .Where(e => e.FinalGrade.HasValue)
                    .OrderByDescending(e => e.FinalGrade)
                    .ToListAsync();

                // Get courses for filter
                var coursesQuery = _context.Courses.Where(c => c.IsActive);
                if (userRole == "Lecturer")
                {
                    coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
                }
                ViewBag.Courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();

                return View(enrollments);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Student performance report error: {ex.Message}");
                TempData["ErrorMessage"] = "Error generating report.";
                return RedirectToAction("Index");
            }
        }

        // GET: Report/LecturerWorkload
        public async Task<IActionResult> LecturerWorkload()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Only administrators can view this report.";
                return RedirectToAction("Index");
            }

            try
            {
                var lecturers = await _context.Users
                    .Where(u => u.Role == "Lecturer" && u.IsActive)
                    .ToListAsync();

                var workloadData = new List<dynamic>();

                foreach (var lecturer in lecturers)
                {
                    var courses = await _context.Courses
                        .Include(c => c.Enrollments)
                        .Where(c => c.LecturerId == lecturer.UserId && c.IsActive)
                        .ToListAsync();

                    var totalStudents = courses.Sum(c => c.Enrollments.Count(e => e.Status == "Active"));
                    var totalAssignments = await _context.Assignments
                        .CountAsync(a => courses.Select(c => c.CourseId).Contains(a.CourseId) && a.IsActive);

                    var pendingGrading = await _context.Submissions
                        .Include(s => s.Assignment)
                        .CountAsync(s => courses.Select(c => c.CourseId).Contains(s.Assignment.CourseId) 
                            && !s.MarksObtained.HasValue 
                            && s.Status == "Submitted");

                    workloadData.Add(new
                    {
                        Lecturer = lecturer,
                        TotalCourses = courses.Count,
                        TotalStudents = totalStudents,
                        TotalAssignments = totalAssignments,
                        PendingGrading = pendingGrading
                    });
                }

                return View(workloadData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lecturer workload report error: {ex.Message}");
                TempData["ErrorMessage"] = "Error generating report.";
                return RedirectToAction("Index");
            }
        }

        // GET: Report/EnrollmentTrends
        public async Task<IActionResult> EnrollmentTrends()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Only administrators can view this report.";
                return RedirectToAction("Index");
            }

            try
            {
                // Get enrollments by month for the last 6 months
                var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                var enrollments = await _context.Enrollments
                    .Where(e => e.EnrollmentDate >= sixMonthsAgo)
                    .ToListAsync();

                var monthlyData = enrollments
                    .GroupBy(e => new { e.EnrollmentDate.Year, e.EnrollmentDate.Month })
                    .Select(g => new
                    {
                        Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Count = g.Count(),
                        Active = g.Count(e => e.Status == "Active"),
                        Completed = g.Count(e => e.Status == "Completed"),
                        Dropped = g.Count(e => e.Status == "Dropped")
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                // Get enrollments by department
                var departmentData = await _context.Enrollments
                    .Include(e => e.Course)
                    .Where(e => e.Status == "Active")
                    .GroupBy(e => e.Course.Department)
                    .Select(g => new
                    {
                        Department = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                ViewBag.MonthlyData = monthlyData;
                ViewBag.DepartmentData = departmentData;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Enrollment trends report error: {ex.Message}");
                TempData["ErrorMessage"] = "Error generating report.";
                return RedirectToAction("Index");
            }
        }

        // GET: Report/AssignmentAnalytics
        public async Task<IActionResult> AssignmentAnalytics(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                IQueryable<Models.Assignment> assignmentsQuery = _context.Assignments
                    .Include(a => a.Course)
                    .Include(a => a.Submissions)
                    .Where(a => a.IsActive);

                if (userRole == "Lecturer")
                {
                    var lecturerCourseIds = await _context.Courses
                        .Where(c => c.LecturerId == userId)
                        .Select(c => c.CourseId)
                        .ToListAsync();
                    assignmentsQuery = assignmentsQuery.Where(a => lecturerCourseIds.Contains(a.CourseId));
                }

                if (courseId.HasValue)
                {
                    assignmentsQuery = assignmentsQuery.Where(a => a.CourseId == courseId);
                }

                var assignments = await assignmentsQuery.ToListAsync();

                var analyticsData = assignments.Select(a => new
                {
                    Assignment = a,
                    TotalSubmissions = a.Submissions?.Count ?? 0,
                    GradedSubmissions = a.Submissions?.Count(s => s.MarksObtained.HasValue) ?? 0,
                    AverageScore = a.Submissions?.Where(s => s.MarksObtained.HasValue).Any() == true
                        ? a.Submissions.Where(s => s.MarksObtained.HasValue).Average(s => s.MarksObtained.Value)
                        : 0,
                    LateSubmissions = a.Submissions?.Count(s => s.Status == "Late") ?? 0,
                    PendingGrading = a.Submissions?.Count(s => !s.MarksObtained.HasValue && s.Status == "Submitted") ?? 0
                }).ToList();

                // Get courses for filter
                var coursesQuery = _context.Courses.Where(c => c.IsActive);
                if (userRole == "Lecturer")
                {
                    coursesQuery = coursesQuery.Where(c => c.LecturerId == userId);
                }
                ViewBag.Courses = await coursesQuery.OrderBy(c => c.CourseName).ToListAsync();

                return View(analyticsData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Assignment analytics report error: {ex.Message}");
                TempData["ErrorMessage"] = "Error generating report.";
                return RedirectToAction("Index");
            }
        }

        // Export Course Popularity Report
        public async Task<IActionResult> ExportCoursePopularity()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var courseStats = await _context.Courses
                    .Include(c => c.Lecturer)
                    .Include(c => c.Enrollments)
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        Course = c,
                        EnrollmentCount = c.Enrollments.Count(e => e.Status == "Active"),
                        CompletedCount = c.Enrollments.Count(e => e.Status == "Completed"),
                        DroppedCount = c.Enrollments.Count(e => e.Status == "Dropped"),
                        AvailableSeats = c.MaxStudents - c.Enrollments.Count(e => e.Status == "Active")
                    })
                    .OrderByDescending(x => x.EnrollmentCount)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Course Popularity");

                    // Headers
                    worksheet.Cell(1, 1).Value = "Course Code";
                    worksheet.Cell(1, 2).Value = "Course Name";
                    worksheet.Cell(1, 3).Value = "Department";
                    worksheet.Cell(1, 4).Value = "Lecturer";
                    worksheet.Cell(1, 5).Value = "Max Students";
                    worksheet.Cell(1, 6).Value = "Active Enrollments";
                    worksheet.Cell(1, 7).Value = "Completed";
                    worksheet.Cell(1, 8).Value = "Dropped";
                    worksheet.Cell(1, 9).Value = "Available Seats";
                    worksheet.Cell(1, 10).Value = "Fill Rate (%)";

                    // Style headers
                    var headerRange = worksheet.Range(1, 1, 1, 10);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Data
                    int row = 2;
                    foreach (var stat in courseStats)
                    {
                        var fillRate = stat.Course.MaxStudents > 0
                            ? (stat.EnrollmentCount * 100.0 / stat.Course.MaxStudents)
                            : 0;

                        worksheet.Cell(row, 1).Value = stat.Course.CourseCode;
                        worksheet.Cell(row, 2).Value = stat.Course.CourseName;
                        worksheet.Cell(row, 3).Value = stat.Course.Department;
                        worksheet.Cell(row, 4).Value = stat.Course.Lecturer?.FullName ?? "N/A";
                        worksheet.Cell(row, 5).Value = stat.Course.MaxStudents;
                        worksheet.Cell(row, 6).Value = stat.EnrollmentCount;
                        worksheet.Cell(row, 7).Value = stat.CompletedCount;
                        worksheet.Cell(row, 8).Value = stat.DroppedCount;
                        worksheet.Cell(row, 9).Value = stat.AvailableSeats;
                        worksheet.Cell(row, 10).Value = $"{fillRate:F1}%";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"CoursePopularity_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export course popularity error: {ex.Message}");
                TempData["ErrorMessage"] = "Error exporting report.";
                return RedirectToAction("CoursePopularity");
            }
        }

        // Export Student Performance Report
        public async Task<IActionResult> ExportStudentPerformance(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                IQueryable<Models.Enrollment> enrollmentsQuery = _context.Enrollments
                    .Include(e => e.Student)
                    .Include(e => e.Course);

                if (userRole == "Lecturer")
                {
                    var lecturerCourseIds = await _context.Courses
                        .Where(c => c.LecturerId == userId)
                        .Select(c => c.CourseId)
                        .ToListAsync();
                    enrollmentsQuery = enrollmentsQuery.Where(e => lecturerCourseIds.Contains(e.CourseId));
                }

                if (courseId.HasValue)
                {
                    enrollmentsQuery = enrollmentsQuery.Where(e => e.CourseId == courseId);
                }

                var enrollments = await enrollmentsQuery
                    .Where(e => e.FinalGrade.HasValue)
                    .OrderByDescending(e => e.FinalGrade)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Student Performance");

                    // Headers
                    worksheet.Cell(1, 1).Value = "Student Name";
                    worksheet.Cell(1, 2).Value = "Email";
                    worksheet.Cell(1, 3).Value = "Course Code";
                    worksheet.Cell(1, 4).Value = "Course Name";
                    worksheet.Cell(1, 5).Value = "Final Grade (%)";
                    worksheet.Cell(1, 6).Value = "Letter Grade";
                    worksheet.Cell(1, 7).Value = "Status";
                    worksheet.Cell(1, 8).Value = "Enrollment Date";

                    // Style headers
                    var headerRange = worksheet.Range(1, 1, 1, 8);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Data
                    int row = 2;
                    foreach (var enrollment in enrollments)
                    {
                        worksheet.Cell(row, 1).Value = enrollment.Student?.FullName ?? "N/A";
                        worksheet.Cell(row, 2).Value = enrollment.Student?.Email ?? "N/A";
                        worksheet.Cell(row, 3).Value = enrollment.Course?.CourseCode ?? "N/A";
                        worksheet.Cell(row, 4).Value = enrollment.Course?.CourseName ?? "N/A";
                        worksheet.Cell(row, 5).Value = enrollment.FinalGrade?.ToString("F1") ?? "N/A";
                        worksheet.Cell(row, 6).Value = enrollment.LetterGrade ?? "N/A";
                        worksheet.Cell(row, 7).Value = enrollment.Status;
                        worksheet.Cell(row, 8).Value = enrollment.EnrollmentDate.ToString("yyyy-MM-dd");
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"StudentPerformance_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export student performance error: {ex.Message}");
                TempData["ErrorMessage"] = "Error exporting report.";
                return RedirectToAction("StudentPerformance");
            }
        }

        // Export Lecturer Workload Report
        public async Task<IActionResult> ExportLecturerWorkload()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || userRole != "Administrator")
            {
                TempData["ErrorMessage"] = "Only administrators can export this report.";
                return RedirectToAction("Index");
            }

            try
            {
                var lecturers = await _context.Users
                    .Where(u => u.Role == "Lecturer" && u.IsActive)
                    .ToListAsync();

                var workloadData = new List<dynamic>();

                foreach (var lecturer in lecturers)
                {
                    var courses = await _context.Courses
                        .Include(c => c.Enrollments)
                        .Where(c => c.LecturerId == lecturer.UserId && c.IsActive)
                        .ToListAsync();

                    var totalStudents = courses.Sum(c => c.Enrollments.Count(e => e.Status == "Active"));
                    var totalAssignments = await _context.Assignments
                        .CountAsync(a => courses.Select(c => c.CourseId).Contains(a.CourseId) && a.IsActive);

                    var pendingGrading = await _context.Submissions
                        .Include(s => s.Assignment)
                        .CountAsync(s => courses.Select(c => c.CourseId).Contains(s.Assignment.CourseId)
                            && !s.MarksObtained.HasValue
                            && s.Status == "Submitted");

                    workloadData.Add(new
                    {
                        Lecturer = lecturer,
                        TotalCourses = courses.Count,
                        TotalStudents = totalStudents,
                        TotalAssignments = totalAssignments,
                        PendingGrading = pendingGrading
                    });
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Lecturer Workload");

                    // Headers
                    worksheet.Cell(1, 1).Value = "Lecturer Name";
                    worksheet.Cell(1, 2).Value = "Email";
                    worksheet.Cell(1, 3).Value = "Department";
                    worksheet.Cell(1, 4).Value = "Total Courses";
                    worksheet.Cell(1, 5).Value = "Total Students";
                    worksheet.Cell(1, 6).Value = "Total Assignments";
                    worksheet.Cell(1, 7).Value = "Pending Grading";
                    worksheet.Cell(1, 8).Value = "Avg Students/Course";

                    // Style headers
                    var headerRange = worksheet.Range(1, 1, 1, 8);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Data
                    int row = 2;
                    foreach (dynamic data in workloadData)
                    {
                        var avgStudents = data.TotalCourses > 0
                            ? (double)data.TotalStudents / data.TotalCourses
                            : 0;

                        worksheet.Cell(row, 1).Value = data.Lecturer.FullName;
                        worksheet.Cell(row, 2).Value = data.Lecturer.Email;
                        worksheet.Cell(row, 3).Value = data.Lecturer.Department;
                        worksheet.Cell(row, 4).Value = data.TotalCourses;
                        worksheet.Cell(row, 5).Value = data.TotalStudents;
                        worksheet.Cell(row, 6).Value = data.TotalAssignments;
                        worksheet.Cell(row, 7).Value = data.PendingGrading;
                        worksheet.Cell(row, 8).Value = $"{avgStudents:F1}";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"LecturerWorkload_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export lecturer workload error: {ex.Message}");
                TempData["ErrorMessage"] = "Error exporting report.";
                return RedirectToAction("LecturerWorkload");
            }
        }

        // Export Assignment Analytics Report
        public async Task<IActionResult> ExportAssignmentAnalytics(int? courseId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userId == null || (userRole != "Administrator" && userRole != "Lecturer"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                IQueryable<Models.Assignment> assignmentsQuery = _context.Assignments
                    .Include(a => a.Course)
                    .Include(a => a.Submissions)
                    .Where(a => a.IsActive);

                if (userRole == "Lecturer")
                {
                    var lecturerCourseIds = await _context.Courses
                        .Where(c => c.LecturerId == userId)
                        .Select(c => c.CourseId)
                        .ToListAsync();
                    assignmentsQuery = assignmentsQuery.Where(a => lecturerCourseIds.Contains(a.CourseId));
                }

                if (courseId.HasValue)
                {
                    assignmentsQuery = assignmentsQuery.Where(a => a.CourseId == courseId);
                }

                var assignments = await assignmentsQuery.ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Assignment Analytics");

                    // Headers
                    worksheet.Cell(1, 1).Value = "Assignment Title";
                    worksheet.Cell(1, 2).Value = "Course";
                    worksheet.Cell(1, 3).Value = "Max Marks";
                    worksheet.Cell(1, 4).Value = "Due Date";
                    worksheet.Cell(1, 5).Value = "Total Submissions";
                    worksheet.Cell(1, 6).Value = "Graded";
                    worksheet.Cell(1, 7).Value = "Pending Grading";
                    worksheet.Cell(1, 8).Value = "Late Submissions";
                    worksheet.Cell(1, 9).Value = "Average Score";
                    worksheet.Cell(1, 10).Value = "Average %";

                    // Style headers
                    var headerRange = worksheet.Range(1, 1, 1, 10);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Data
                    int row = 2;
                    foreach (var assignment in assignments)
                    {
                        var totalSubmissions = assignment.Submissions?.Count ?? 0;
                        var gradedSubmissions = assignment.Submissions?.Count(s => s.MarksObtained.HasValue) ?? 0;
                        var pendingGrading = assignment.Submissions?.Count(s => !s.MarksObtained.HasValue && s.Status == "Submitted") ?? 0;
                        var lateSubmissions = assignment.Submissions?.Count(s => s.Status == "Late") ?? 0;
                        var averageScore = assignment.Submissions?.Where(s => s.MarksObtained.HasValue).Any() == true
                            ? assignment.Submissions.Where(s => s.MarksObtained.HasValue).Average(s => s.MarksObtained.Value)
                            : 0;
                        var averagePercentage = assignment.MaxMarks > 0 ? (averageScore / assignment.MaxMarks * 100) : 0;

                        worksheet.Cell(row, 1).Value = assignment.Title;
                        worksheet.Cell(row, 2).Value = assignment.Course?.CourseName ?? "N/A";
                        worksheet.Cell(row, 3).Value = assignment.MaxMarks;
                        worksheet.Cell(row, 4).Value = assignment.DueDate.ToString("yyyy-MM-dd");
                        worksheet.Cell(row, 5).Value = totalSubmissions;
                        worksheet.Cell(row, 6).Value = gradedSubmissions;
                        worksheet.Cell(row, 7).Value = pendingGrading;
                        worksheet.Cell(row, 8).Value = lateSubmissions;
                        worksheet.Cell(row, 9).Value = $"{averageScore:F1}";
                        worksheet.Cell(row, 10).Value = $"{averagePercentage:F1}%";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"AssignmentAnalytics_{DateTime.Now:yyyyMMdd}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export assignment analytics error: {ex.Message}");
                TempData["ErrorMessage"] = "Error exporting report.";
                return RedirectToAction("AssignmentAnalytics");
            }
        }
    }
}
