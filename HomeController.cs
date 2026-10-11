using Microsoft.AspNetCore.Mvc;
using UniManage.Models;
using System.Diagnostics;

namespace UniManage.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // GET: Home page
        public IActionResult Index()
        {
            // Check if user is logged in
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId != null)
            {
                var userRole = HttpContext.Session.GetString("UserRole");
                
                // Redirect to appropriate dashboard based on role
                return userRole switch
                {
                    "Student" => RedirectToAction("StudentDashboard", "Dashboard"),
                    "Lecturer" => RedirectToAction("LecturerDashboard", "Dashboard"),
                    "Administrator" => RedirectToAction("AdminDashboard", "Dashboard"),
                    _ => View()
                };
            }

            return View();
        }

        // GET: About page
        public IActionResult About()
        {
            return View();
        }

        // GET: Contact page
        public IActionResult Contact()
        {
            return View();
        }

        // GET: Privacy page
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
