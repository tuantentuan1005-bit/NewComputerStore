using NewComputerStore.Models;
using System.Threading.Tasks;

namespace NewComputerStore.Services
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string receiverEmail, string otp);
        Task SendWelcomeEmailAsync(string receiverEmail, string fullName);
        Task SendPasswordResetEmailAsync(string receiverEmail, string resetLink);
        Task SendOrderStatusEmailAsync(string receiverEmail, Order order, string status);
        Task SendSystemNotificationAsync(string receiverEmail, string subject, string message);
        Task SendSecurityAlertAsync(string receiverEmail, string alertSubject, string alertMessage);

        /// <summary>
        /// Sends a test email and returns full diagnostic result including exception details.
        /// Unlike other send methods this does NOT silently swallow exceptions.
        /// </summary>
        Task<EmailTestResult> SendTestEmailAsync(string toEmail);

        /// <summary>Returns a snapshot of SMTP settings for display (no secrets leaked).</summary>
        EmailTestResult GetSmtpStatus();
    }
}

