using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WonderWatch.Application.Interfaces;
using WonderWatch.Domain.Enums;
using WonderWatch.Domain.Identity;
using WonderWatch.Web.ViewModels;

namespace WonderWatch.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _logger = logger;
        }

        // =====================================================================
        // LOGIN
        // =====================================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return LocalRedirect(returnUrl ?? "/vault");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // Check if user exists and verify password manually first
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                    if (passwordValid)
                    {
                        // Intercept: if email not confirmed, generate OTP and redirect
                        if (!user.EmailConfirmed)
                        {
                            _logger.LogInformation("Unverified user {Email} attempted login. Redirecting to OTP verification.", model.Email);

                            var otp = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                            try
                            {
                                await _emailService.SendOtpAsync(user.Email!, otp, "verification");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to send OTP email to {Email}", user.Email);
                            }

                            TempData["OtpEmail"] = user.Email;
                            TempData["OtpPurpose"] = "verification";
                            TempData["ReturnUrl"] = returnUrl;
                            return RedirectToAction(nameof(VerifyOtp));
                        }

                        // Normal sign-in for verified users
                        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                        if (result.Succeeded)
                        {
                            _logger.LogInformation("User {Email} logged in successfully.", model.Email);
                            return LocalRedirect(returnUrl ?? "/vault");
                        }
                        if (result.IsLockedOut)
                        {
                            _logger.LogWarning("User account {Email} locked out.", model.Email);
                            ModelState.AddModelError(string.Empty, "Account locked due to multiple failed attempts. Please try again later.");
                            return View(model);
                        }
                    }
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt. Please verify your credentials.");
                return View(model);
            }

            return View(model);
        }

        // =====================================================================
        // REGISTER
        // =====================================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return LocalRedirect(returnUrl ?? "/vault");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    DisplayName = model.FullName.Split(' ')[0], // Default display name to first name
                    MembershipTier = MembershipTier.Silver, // Default tier for new registrations
                    MemberSince = DateTime.UtcNow,
                    Nationality = model.Nationality,
                    DateOfBirth = model.DateOfBirth,
                    EmailConfirmed = false // Explicitly unverified until OTP confirmed
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} created a new account. Sending OTP for verification.", user.Email);

                    // Assign default Customer role
                    await _userManager.AddToRoleAsync(user, "Customer");

                    // Generate OTP and send verification email — do NOT auto sign-in
                    var otp = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                    try
                    {
                        await _emailService.SendOtpAsync(user.Email!, otp, "verification");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send OTP email to {Email}. User can resend from verify page.", user.Email);
                    }

                    TempData["OtpEmail"] = user.Email;
                    TempData["OtpPurpose"] = "verification";
                    TempData["ReturnUrl"] = returnUrl;
                    return RedirectToAction(nameof(VerifyOtp));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // =====================================================================
        // VERIFY OTP (Shared: Registration Verification + Password Reset Gate)
        // =====================================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyOtp()
        {
            var email = TempData["OtpEmail"]?.ToString();
            var purpose = TempData["OtpPurpose"]?.ToString() ?? "verification";

            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction(nameof(Login));
            }

            // Persist TempData for the POST
            TempData.Keep("ReturnUrl");

            return View(new VerifyOtpViewModel { Email = email, Purpose = purpose });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal user existence — generic error
                ModelState.AddModelError(string.Empty, "Verification failed. Please try again.");
                return View(model);
            }

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, model.Otp);

            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired code. Please try again or request a new code.");
                return View(model);
            }

            if (model.Purpose == "verification")
            {
                // Mark email as confirmed
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("User {Email} verified successfully via OTP.", model.Email);

                // Auto sign-in after verification
                await _signInManager.SignInAsync(user, isPersistent: false);
                var returnUrl = TempData["ReturnUrl"]?.ToString();
                return LocalRedirect(returnUrl ?? "/vault");
            }
            else if (model.Purpose == "password-reset")
            {
                // OTP verified — redirect to ResetPassword with a server-generated token
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                TempData["ResetToken"] = resetToken;
                TempData["ResetEmail"] = model.Email;
                return RedirectToAction(nameof(ResetPassword));
            }

            return RedirectToAction(nameof(Login));
        }

        // =====================================================================
        // RESEND OTP
        // =====================================================================

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp([FromForm] string email, [FromForm] string purpose)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var otp = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                try
                {
                    await _emailService.SendOtpAsync(user.Email!, otp, purpose);
                    _logger.LogInformation("OTP resent to {Email} for {Purpose}.", email, purpose);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resend OTP email to {Email}", email);
                }
            }
            // Don't reveal whether user exists — always redirect back

            TempData["OtpEmail"] = email;
            TempData["OtpPurpose"] = purpose;
            TempData["ResendSuccess"] = "A new code has been sent to your email.";
            return RedirectToAction(nameof(VerifyOtp));
        }

        // =====================================================================
        // FORGOT PASSWORD
        // =====================================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var otp = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                try
                {
                    await _emailService.SendOtpAsync(user.Email!, otp, "password-reset");
                    _logger.LogInformation("Password reset OTP sent to {Email}.", model.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset OTP to {Email}", model.Email);
                }
            }

            // Always redirect — don't reveal user existence
            TempData["OtpEmail"] = model.Email;
            TempData["OtpPurpose"] = "password-reset";
            return RedirectToAction(nameof(VerifyOtp));
        }

        // =====================================================================
        // RESET PASSWORD (After OTP verification)
        // =====================================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword()
        {
            var email = TempData["ResetEmail"]?.ToString();
            var token = TempData["ResetToken"]?.ToString();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction(nameof(Login));
            }

            return View(new ResetPasswordViewModel { Email = email, Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                TempData["ResetSuccess"] = true;
                return RedirectToAction(nameof(Login));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} successfully reset their password.", model.Email);
                TempData["ResetSuccess"] = "true";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // =====================================================================
        // LOGOUT
        // =====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");

            // Clear any session data (like the cart) upon logout for security
            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

namespace WonderWatch.Web.ViewModels
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty; [Required(ErrorMessage = "Nationality is required.")]
        public string Nationality { get; set; } = string.Empty; [Required(ErrorMessage = "Date of Birth is required.")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class VerifyOtpViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Verification code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be exactly 6 digits.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be 6 digits.")]
        public string Otp { get; set; } = string.Empty;

        public string Purpose { get; set; } = "verification";
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}