using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Models;
using UniManage.Models.Data;
using UniManage.Models.ViewModels;

namespace UniManage.Controllers
{
    public class AccountController : Controller
    {
        private readonly UniManageDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(UniManageDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Account/Login
        public IActionResult Login()
        {
            // If already logged in, redirect to dashboard
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Find user by email and password
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email == model.Email && u.Password == model.Password && u.IsActive);

                    if (user != null)
                    {
                        // Set session variables
                        HttpContext.Session.SetInt32("UserId", user.UserId);
                        HttpContext.Session.SetString("UserName", user.FullName);
                        HttpContext.Session.SetString("UserEmail", user.Email);
                        HttpContext.Session.SetString("UserRole", user.Role);

                        _logger.LogInformation($"User {user.Email} logged in successfully");

                        // Redirect based on role
                        return user.Role switch
                        {
                            "Student" => RedirectToAction("StudentDashboard", "Dashboard"),
                            "Lecturer" => RedirectToAction("LecturerDashboard", "Dashboard"),
                            "Administrator" => RedirectToAction("AdminDashboard", "Dashboard"),
                            _ => RedirectToAction("Index", "Home")
                        };
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid email or password");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login error: {ex.Message}");
                ModelState.AddModelError("", "An error occurred during login. Please try again.");
            }

            return View(model);
        }

        // GET: Account/Register
        public IActionResult Register()
        {
            // If already logged in, redirect to dashboard
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if email already exists
                    var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "This email is already registered");
                        return View(model);
                    }

                    // Validate role
                    if (model.Role != "Student" && model.Role != "Lecturer" && model.Role != "Administrator")
                    {
                        ModelState.AddModelError("Role", "Invalid role selected");
                        return View(model);
                    }

                    // Create new user
                    var user = new User
                    {
                        FullName = model.FullName,
                        Email = model.Email,
                        Password = model.Password, // In production, this should be hashed
                        Role = model.Role,
                        PhoneNumber = model.PhoneNumber,
                        Department = model.Department,
                        RegisteredDate = DateTime.Now,
                        IsActive = true
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"New user registered: {user.Email} as {user.Role}");

                    TempData["SuccessMessage"] = "Registration successful! Please login to continue.";
                    return RedirectToAction("Login");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Registration error: {ex.Message}");
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
            }

            return View(model);
        }

        // GET: Account/Logout
        public IActionResult Logout()
        {
            // Clear session
            HttpContext.Session.Clear();
            
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        // GET: Account/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Account/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(User model)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToAction("Login");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                // Update user details
                user.FullName = model.FullName;
                user.PhoneNumber = model.PhoneNumber;
                user.Department = model.Department;

                _context.Update(user);
                await _context.SaveChangesAsync();

                // Update session
                HttpContext.Session.SetString("UserName", user.FullName);

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Profile update error: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while updating profile.";
                return RedirectToAction("Profile");
            }
        }

        // GET: Account/ChangePassword
        public IActionResult ChangePassword()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToAction("Login");
                }

                if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
                {
                    TempData["ErrorMessage"] = "All fields are required.";
                    return View();
                }

                if (newPassword != confirmPassword)
                {
                    TempData["ErrorMessage"] = "New password and confirm password do not match.";
                    return View();
                }

                if (newPassword.Length < 6)
                {
                    TempData["ErrorMessage"] = "Password must be at least 6 characters long.";
                    return View();
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null || user.Password != currentPassword)
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return View();
                }

                user.Password = newPassword;
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password changed successfully!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Password change error: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while changing password.";
                return View();
            }
        }
    }
}
