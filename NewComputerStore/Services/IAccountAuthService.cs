using System;
using System.Threading.Tasks;
using NewComputerStore.Models;

namespace NewComputerStore.Services
{
    public class AuthOperationResult
    {
        public bool Succeeded { get; set; }
        public bool AlreadyVerified { get; set; }
        public string? ErrorCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? RetryAfterSeconds { get; set; }
        public User? User { get; set; }
    }

    public interface IAccountAuthService
    {
        string HashOtp(string otp);
        Task<AuthOperationResult> RegisterCustomerAsync(string fullName, string email, string password);
        Task<AuthOperationResult> VerifyOtpAsync(string email, string otp);
        Task<AuthOperationResult> ResendOtpAsync(string email);
        Task<AuthOperationResult> AuthenticateAsync(string email, string password);
        Task<User> SignInOrRegisterGoogleUserAsync(string email, string? fullName);
        Task<AuthOperationResult> GoogleLoginWithIdTokenAsync(string idToken);
        Task ForgotPasswordAsync(string email, Func<string, string, string> resetLinkBuilder);
        (bool IsValid, string? Identifier) ValidatePasswordResetToken(string token);
        Task<AuthOperationResult> ResetPasswordAsync(string email, string token, string newPassword);
    }
}
