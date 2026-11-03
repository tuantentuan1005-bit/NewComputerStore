using System;

namespace NewComputerStore.Services
{
    /// <summary>Result DTO returned by SendTestEmailAsync — carries full error detail.</summary>
    public class EmailTestResult
    {
        public bool Success { get; set; }

        // SMTP config snapshot (for display)
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
        public bool IsEnabled { get; set; }

        // Error details (populated only when Success == false)
        public string? ErrorMessage { get; set; }
        public string? InnerErrorMessage { get; set; }
        public string? StackTrace { get; set; }
        public string? SmtpErrorCode { get; set; }   // e.g. "5.7.8 Username and Password not accepted"
    }
}
