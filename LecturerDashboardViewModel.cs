namespace UniManage.Models.ViewModels
{
    public class LecturerDashboardViewModel
    {
        public User? Lecturer { get; set; }
        public List<Course>? TeachingCourses { get; set; }
        public List<Assignment>? RecentAssignments { get; set; }
        public List<Submission>? PendingGrading { get; set; }
        public int TotalCourses { get; set; }
        public int TotalStudents { get; set; }
        public int PendingSubmissions { get; set; }
        public int UnreadMessages { get; set; }
    }
}
