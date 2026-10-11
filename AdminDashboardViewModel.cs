namespace UniManage.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public User? Admin { get; set; }
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalCourses { get; set; }
        public int ActiveEnrollments { get; set; }
        public int TotalAssignments { get; set; }
        public List<Course>? RecentCourses { get; set; }
        public List<Enrollment>? RecentEnrollments { get; set; }
        public Dictionary<string, int>? EnrollmentsByDepartment { get; set; }
        public Dictionary<string, int>? CoursePopularity { get; set; }
    }
}
