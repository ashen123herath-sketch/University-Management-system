namespace UniManage.Models.ViewModels
{
    public class StudentDashboardViewModel
    {
        public User? Student { get; set; }
        public List<Enrollment>? EnrolledCourses { get; set; }
        public List<Assignment>? UpcomingAssignments { get; set; }
        public List<Submission>? RecentSubmissions { get; set; }
        public int TotalEnrollments { get; set; }
        public int PendingAssignments { get; set; }
        public double AverageGrade { get; set; }
        public int UnreadMessages { get; set; }
    }
}
