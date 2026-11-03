using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NewComputerStore.Data;
using NewComputerStore.Models;
using NewComputerStore.Services;
using NewComputerStore.ViewModels;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
//test

namespace NewComputerStore.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly IAccountAuthService _accountAuthService;
        private readonly IConfiguration _configuration;

        public AccountController(
            AppDbContext context,
            IPasswordHasher<User> passwordHasher,
            IEmailService emailService,
            IAccountAuthService accountAuthService,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _accountAuthService = accountAuthService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _accountAuthService.RegisterCustomerAsync(model.FullName, model.Email, model.Password);
                if (!result.Succeeded)
                {
                    ModelState.AddModelError("Email", result.Message);
                    return View(model);
                }

                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("VerifyOtp", new { email = result.User!.Email });
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var authResult = await _accountAuthService.AuthenticateAsync(model.Email, model.Password);

                if (authResult.ErrorCode == "ACCOUNT_LOCKED")
                {
                    ModelState.AddModelError("", authResult.Message);
                    ViewData["ReturnUrl"] = returnUrl;
                    return View(model);
                }

                if (authResult.ErrorCode == "EMAIL_NOT_VERIFIED")
                {
                    TempData["ErrorMessage"] = authResult.Message;
                    return RedirectToAction("VerifyOtp", new { email = authResult.User!.Email, returnUrl = returnUrl });
                }

                if (authResult.Succeeded && authResult.User != null)
                {
                    var user = authResult.User;
                    await SignInUser(user, model.RememberMe);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    if (user.Role == UserRoles.Admin)
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else if (user.Role == UserRoles.WarehouseManager)
                    {
                        return RedirectToAction("Index", "Category");
                    }
                    else if (user.Role == UserRoles.EmployeeManager)
                    {
                        return RedirectToAction("Index", "StaffManagement");
                    }
                    else if (user.Role == UserRoles.Staff)
                    {
                        return RedirectToAction("AdminIndex", "Order");
                    }
                    else if (user.Role == UserRoles.CustomerSupport)
                    {
                        return RedirectToAction("Dashboard", "CustomerSupport");
                    }

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid email or password.");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
                return RedirectToAction(nameof(Login));
            }

            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (!result.Succeeded)
            {
                return RedirectToAction(nameof(Login));
            }

            // Extract claims from Google
            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var name  = result.Principal.FindFirstValue(ClaimTypes.Name);

            if (email == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _accountAuthService.SignInOrRegisterGoogleUserAsync(email, name);

            await SignInUser(user, false);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // Redirect based on role
            if (user.Role == UserRoles.Admin)
                return RedirectToAction("Index", "Admin");
            if (user.Role == UserRoles.WarehouseManager)
                return RedirectToAction("Index", "Category");
            if (user.Role == UserRoles.EmployeeManager)
                return RedirectToAction("Index", "StaffManagement");
            if (user.Role == UserRoles.Staff)
                return RedirectToAction("AdminIndex", "Order");
            if (user.Role == UserRoles.CustomerSupport)
                return RedirectToAction("Dashboard", "CustomerSupport");

            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUser(User user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        [Authorize]
        public async Task<IActionResult> MyAccount()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
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

        [HttpGet]
        public IActionResult RegisterSuccess()
        {
            var email = TempData["RegisterEmail"] as string;
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.Email = email;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> VerifyOtp(string email, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (user.IsEmailVerified)
            {
                TempData["SuccessMessage"] = "Tài khoản của bạn đã được xác minh trước đó. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            int countdownSeconds = 0;
            if (user.VerificationExpiry.HasValue && user.VerificationExpiry.Value > DateTime.Now)
            {
                countdownSeconds = (int)(user.VerificationExpiry.Value - DateTime.Now).TotalSeconds;
            }

            int resendCooldownSeconds = 0;
            if (user.LastOtpSentAt.HasValue)
            {
                int elapsed = (int)(DateTime.Now - user.LastOtpSentAt.Value).TotalSeconds;
                if (elapsed < 60)
                {
                    resendCooldownSeconds = 60 - elapsed;
                }
            }

            ViewBag.Email = email;
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.CountdownSeconds = countdownSeconds;
            ViewBag.ResendCooldownSeconds = resendCooldownSeconds;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string email, string otp, string returnUrl = null)
        {
            var result = await _accountAuthService.VerifyOtpAsync(email, otp);
            if (!result.Succeeded)
            {
                return Json(new { success = false, message = result.Message });
            }

            if (result.AlreadyVerified)
            {
                return Json(new { success = true, redirectUrl = Url.Action("Login") });
            }

            // Log in the user
            await SignInUser(result.User!, false);

            string targetUrl = Url.Action("Index", "Home");
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                targetUrl = returnUrl;
            }

            return Json(new { success = true, redirectUrl = targetUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string email)
        {
            var result = await _accountAuthService.ResendOtpAsync(email);
            return Json(new { success = result.Succeeded, message = result.Message });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Vui lòng nhập địa chỉ Email.");
                return View();
            }

            await _accountAuthService.ForgotPasswordAsync(
                email,
                (userEmail, token) => Url.Action("ResetPassword", "Account", new { email = userEmail, token = token }, Request.Scheme)!);

            // Standard safety response to prevent user enumeration
            ViewData["SuccessMessage"] = "Nếu email trên tồn tại trong hệ thống của chúng tôi, một liên kết đặt lại mật khẩu đã được gửi đến hộp thư của bạn (hiệu lực trong 15 phút).";
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            var validation = _accountAuthService.ValidatePasswordResetToken(token);
            if (!validation.IsValid || validation.Identifier != email)
            {
                ViewData["ErrorMessage"] = "Đường dẫn khôi phục mật khẩu không hợp lệ hoặc đã hết hạn (15 phút). Vui lòng yêu cầu lại.";
                return View("ResetPasswordError");
            }

            var model = new ResetPasswordViewModel { Email = email, Token = token };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accountAuthService.ResetPasswordAsync(model.Email, model.Token, model.NewPassword);
            if (!result.Succeeded)
            {
                if (result.ErrorCode == "USER_NOT_FOUND")
                {
                    return NotFound();
                }

                ModelState.AddModelError("", result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Login");
        }
    }
}
