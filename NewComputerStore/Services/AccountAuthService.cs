using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NewComputerStore.Data;
using NewComputerStore.Models;

namespace NewComputerStore.Services
{
    public class AccountAuthService : IAccountAuthService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly IDataProtector _protector;
        private readonly IConfiguration _configuration;

        public AccountAuthService(
            AppDbContext context,
            IPasswordHasher<User> passwordHasher,
            IEmailService emailService,
            IDataProtectionProvider dataProtectionProvider,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _protector = dataProtectionProvider.CreateProtector("AccountSecurity");
            _configuration = configuration;
        }

        public string HashOtp(string otp)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(otp);
            var hash = sha256.ComputeHash(bytes);
            var builder = new StringBuilder();
            foreach (var b in hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        public async Task<AuthOperationResult> RegisterCustomerAsync(string fullName, string email, string password)
        {
            var existingActiveUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

            if (existingActiveUser != null)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "EMAIL_ALREADY_EXISTS",
                    Message = "Email này đã được sử dụng bởi một tài khoản khác."
                };
            }

            var deletedUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.IsDeleted);

            User user;
            if (deletedUser != null)
            {
                user = deletedUser;
                user.FullName = fullName;
                user.Password = _passwordHasher.HashPassword(user, password);
                user.IsDeleted = false;
                user.IsActive = true;
                user.IsEmailVerified = false;
                user.Role = UserRoles.Customer;
            }
            else
            {
                user = new User
                {
                    FullName = fullName,
                    Email = email,
                    Role = UserRoles.Customer,
                    IsEmailVerified = false
                };
                user.Password = _passwordHasher.HashPassword(user, password);
                _context.Users.Add(user);
            }

            var otp = Random.Shared.Next(100000, 999999).ToString();
            user.VerificationCode = HashOtp(otp);
            user.VerificationExpiry = DateTime.Now.AddMinutes(5);
            user.VerificationFailedAttempts = 0;
            user.LastOtpSentAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await _emailService.SendOtpEmailAsync(user.Email, otp);

            return new AuthOperationResult
            {
                Succeeded = true,
                Message = "Đăng ký thành công! Mã xác thực OTP đã được gửi đến email của bạn.",
                User = user
            };
        }

        public async Task<AuthOperationResult> VerifyOtpAsync(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_INPUT",
                    Message = "Email và mã OTP không được để trống."
                };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "Không tìm thấy thông tin tài khoản."
                };
            }

            if (user.IsEmailVerified)
            {
                return new AuthOperationResult
                {
                    Succeeded = true,
                    AlreadyVerified = true,
                    Message = "Tài khoản của bạn đã được xác minh trước đó. Vui lòng đăng nhập.",
                    User = user
                };
            }

            if (user.VerificationCode == null || user.VerificationExpiry == null)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "OTP_INACTIVE",
                    Message = "Mã OTP đã bị vô hiệu hóa hoặc chưa được tạo. Vui lòng nhấn gửi lại mã mới."
                };
            }

            if (DateTime.Now > user.VerificationExpiry.Value)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "OTP_EXPIRED",
                    Message = "Mã OTP đã hết hạn (5 phút). Vui lòng nhấn gửi lại mã mới."
                };
            }

            if (user.VerificationFailedAttempts >= 5)
            {
                user.VerificationCode = null;
                user.VerificationExpiry = null;
                await _context.SaveChangesAsync();
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "OTP_LOCKED",
                    Message = "Mã OTP đã bị khóa do nhập sai quá 5 lần. Vui lòng gửi lại mã mới."
                };
            }

            string inputHash = HashOtp(otp);
            if (user.VerificationCode != inputHash)
            {
                user.VerificationFailedAttempts++;
                await _context.SaveChangesAsync();

                if (user.VerificationFailedAttempts >= 5)
                {
                    user.VerificationCode = null;
                    user.VerificationExpiry = null;
                    await _context.SaveChangesAsync();
                    return new AuthOperationResult
                    {
                        Succeeded = false,
                        ErrorCode = "OTP_LOCKED",
                        Message = "Bạn đã nhập sai 5 lần. Mã OTP hiện tại đã bị hủy. Vui lòng gửi lại mã mới."
                    };
                }

                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "OTP_INVALID",
                    Message = $"Mã OTP không chính xác. Bạn còn {5 - user.VerificationFailedAttempts} lần nhập sai."
                };
            }

            user.IsEmailVerified = true;
            user.VerificationCode = null;
            user.VerificationExpiry = null;
            user.VerificationFailedAttempts = 0;
            await _context.SaveChangesAsync();

            await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

            return new AuthOperationResult
            {
                Succeeded = true,
                Message = "Xác thực tài khoản thành công.",
                User = user
            };
        }

        public async Task<AuthOperationResult> ResendOtpAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_EMAIL",
                    Message = "Email không hợp lệ."
                };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "Không tìm thấy tài khoản."
                };
            }

            if (user.IsEmailVerified)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "EMAIL_ALREADY_VERIFIED",
                    Message = "Email của tài khoản này đã được xác thực trước đó."
                };
            }

            if (user.LastOtpSentAt.HasValue)
            {
                int elapsed = (int)(DateTime.Now - user.LastOtpSentAt.Value).TotalSeconds;
                if (elapsed < 60)
                {
                    int waitSeconds = 60 - elapsed;
                    return new AuthOperationResult
                    {
                        Succeeded = false,
                        ErrorCode = "OTP_THROTTLED",
                        RetryAfterSeconds = waitSeconds,
                        Message = $"Vui lòng đợi thêm {waitSeconds} giây trước khi gửi lại mã."
                    };
                }
            }

            var otp = Random.Shared.Next(100000, 999999).ToString();
            user.VerificationCode = HashOtp(otp);
            user.VerificationExpiry = DateTime.Now.AddMinutes(5);
            user.VerificationFailedAttempts = 0;
            user.LastOtpSentAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await _emailService.SendOtpEmailAsync(user.Email, otp);

            return new AuthOperationResult
            {
                Succeeded = true,
                Message = "Mã OTP mới đã được gửi thành công đến Gmail của bạn.",
                User = user
            };
        }

        public async Task<AuthOperationResult> AuthenticateAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_CREDENTIALS",
                    Message = "Invalid email or password."
                };
            }

            if (!user.IsActive)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "ACCOUNT_LOCKED",
                    Message = "Your account has been locked. Please contact the administrator."
                };
            }

            bool isPasswordValid = false;

            if ((user.Password != null && user.Password.StartsWith("AQAAAA")) || user.Password?.Length > 30)
            {
                var result = _passwordHasher.VerifyHashedPassword(user, user.Password!, password);
                if (result == PasswordVerificationResult.Success)
                {
                    isPasswordValid = true;
                }
                else if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    user.Password = _passwordHasher.HashPassword(user, password);
                    await _context.SaveChangesAsync();
                    isPasswordValid = true;
                }
            }

            if (!isPasswordValid && user.Password == password)
            {
                user.Password = _passwordHasher.HashPassword(user, password);
                await _context.SaveChangesAsync();
                isPasswordValid = true;
            }

            if (!isPasswordValid)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_CREDENTIALS",
                    Message = "Invalid email or password."
                };
            }

            bool isPrivilegedRole = user.Role == UserRoles.Admin
                || user.Role == UserRoles.WarehouseManager
                || user.Role == UserRoles.EmployeeManager
                || user.Role == UserRoles.Staff
                || user.Role == UserRoles.CustomerSupport;

            if (!user.IsEmailVerified && !isPrivilegedRole)
            {
                if (user.VerificationCode == null || user.VerificationExpiry == null || user.VerificationExpiry < DateTime.Now)
                {
                    var otp = Random.Shared.Next(100000, 999999).ToString();
                    user.VerificationCode = HashOtp(otp);
                    user.VerificationExpiry = DateTime.Now.AddMinutes(5);
                    user.VerificationFailedAttempts = 0;
                    user.LastOtpSentAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    await _emailService.SendOtpEmailAsync(user.Email, otp);
                }

                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "EMAIL_NOT_VERIFIED",
                    Message = "Tài khoản chưa được xác thực. Vui lòng nhập mã OTP đã gửi đến email của bạn.",
                    User = user
                };
            }

            return new AuthOperationResult
            {
                Succeeded = true,
                Message = "Đăng nhập thành công.",
                User = user
            };
        }

        public async Task<User> SignInOrRegisterGoogleUserAsync(string email, string? fullName)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted)
                       ?? await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsDeleted);

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    FullName = !string.IsNullOrWhiteSpace(fullName) ? fullName : email,
                    Role = UserRoles.Customer,
                    Password = null,
                    IsEmailVerified = true,
                    IsActive = true
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _ = _emailService.SendWelcomeEmailAsync(email, user.FullName);
            }
            else
            {
                bool changed = false;
                if (user.IsDeleted) { user.IsDeleted = false; changed = true; }
                if (!user.IsEmailVerified) { user.IsEmailVerified = true; changed = true; }
                if (!user.IsActive) { user.IsActive = true; changed = true; }
                if (changed) await _context.SaveChangesAsync();
            }

            return user;
        }

        public async Task<AuthOperationResult> GoogleLoginWithIdTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_GOOGLE_TOKEN",
                    Message = "Google idToken không được để trống."
                };
            }

            var clientId = _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "GOOGLE_AUTH_NOT_CONFIGURED",
                    Message = "Hệ thống chưa cấu hình Google ClientId."
                };
            }

            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            }
            catch (InvalidJwtException)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_GOOGLE_TOKEN",
                    Message = "Google idToken không hợp lệ hoặc đã hết hạn."
                };
            }
            catch (Exception)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "GOOGLE_AUTH_ERROR",
                    Message = "Không thể xác thực Google idToken."
                };
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_GOOGLE_TOKEN",
                    Message = "Không lấy được địa chỉ email từ Google idToken."
                };
            }

            var user = await SignInOrRegisterGoogleUserAsync(payload.Email, payload.Name);

            return new AuthOperationResult
            {
                Succeeded = true,
                Message = "Đăng nhập Google thành công.",
                User = user
            };
        }

        public async Task ForgotPasswordAsync(string email, Func<string, string, string> resetLinkBuilder)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user != null)
            {
                string token = GenerateToken("ResetPassword", user.Email, TimeSpan.FromMinutes(15));
                string resetLink = resetLinkBuilder(user.Email, token);
                await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
            }
        }

        public (bool IsValid, string? Identifier) ValidatePasswordResetToken(string token)
        {
            return ValidateToken("ResetPassword", token);
        }

        public async Task<AuthOperationResult> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var validation = ValidateToken("ResetPassword", token);
            if (!validation.IsValid || validation.Identifier != email)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_RESET_TOKEN",
                    Message = "Yêu cầu khôi phục mật khẩu không hợp lệ hoặc đã hết hạn."
                };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user == null)
            {
                return new AuthOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "Không tìm thấy tài khoản."
                };
            }

            user.Password = _passwordHasher.HashPassword(user, newPassword);
            await _context.SaveChangesAsync();

            await _emailService.SendSecurityAlertAsync(
                user.Email,
                "Thay đổi mật khẩu",
                "Mật khẩu tài khoản của bạn đã được thay đổi thành công bằng chức năng Quên mật khẩu.");

            return new AuthOperationResult
            {
                Succeeded = true,
                Message = "Mật khẩu đã được thay đổi thành công! Vui lòng đăng nhập bằng mật khẩu mới.",
                User = user
            };
        }

        private string GenerateToken(string purpose, string identifier, TimeSpan expiration)
        {
            var expiry = DateTime.UtcNow.Add(expiration);
            string payload = $"{purpose}|{identifier}|{expiry.Ticks}";
            string protectedData = _protector.Protect(payload);
            return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protectedData));
        }

        private (bool IsValid, string? Identifier) ValidateToken(string purpose, string token)
        {
            try
            {
                byte[] decodedBytes = WebEncoders.Base64UrlDecode(token);
                string protectedData = Encoding.UTF8.GetString(decodedBytes);
                string decrypted = _protector.Unprotect(protectedData);

                var parts = decrypted.Split('|');
                if (parts.Length != 3) return (false, null);

                string tokenPurpose = parts[0];
                string identifier = parts[1];
                long ticks = long.Parse(parts[2]);

                if (tokenPurpose != purpose) return (false, null);

                var expiry = new DateTime(ticks, DateTimeKind.Utc);
                if (DateTime.UtcNow > expiry)
                {
                    return (false, null);
                }

                return (true, identifier);
            }
            catch
            {
                return (false, null);
            }
        }
    }
}
