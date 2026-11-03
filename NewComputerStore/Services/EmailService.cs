using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewComputerStore.Data;
using NewComputerStore.Models;
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace NewComputerStore.Services
{
    public class EmailService : IEmailService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _senderName;
        private readonly string _password;
        private readonly bool _enableEmail;

        public EmailService(IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<EmailService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var emailSettings = configuration.GetSection("EmailSettings");
            _smtpServer = emailSettings.GetValue<string>("SmtpServer") ?? "smtp.gmail.com";
            _smtpPort = emailSettings.GetValue<int>("SmtpPort");
            _smtpPort = _smtpPort == 0 ? 587 : _smtpPort;
            _senderEmail = emailSettings.GetValue<string>("SenderEmail") ?? "";
            _senderName = emailSettings.GetValue<string>("SenderName") ?? "NewComputerStore";
            _password = emailSettings.GetValue<string>("Password") ?? "";
            _enableEmail = emailSettings.GetValue<bool>("EnableEmail");
        }

        // ---------------------------------------------------------------
        // SMTP Status & Test
        // ---------------------------------------------------------------

        /// <summary>Returns a snapshot of SMTP settings for the diagnostics page (password masked).</summary>
        public EmailTestResult GetSmtpStatus()
        {
            bool isConfigured = !string.IsNullOrWhiteSpace(_senderEmail)
                                && !string.IsNullOrWhiteSpace(_password)
                                && _password != "xxxx xxxx xxxx xxxx"
                                && _password.Length >= 8;

            return new EmailTestResult
            {
                Success = false, // Will be set by SendTestEmailAsync
                SmtpHost = _smtpServer,
                SmtpPort = _smtpPort,
                SenderEmail = _senderEmail,
                SenderName = _senderName,
                IsConfigured = isConfigured,
                IsEnabled = _enableEmail
            };
        }

        /// <summary>
        /// Sends a diagnostic test email. Returns a rich result with exception details on failure.
        /// This method intentionally surfaces the full exception (unlike business-flow methods).
        /// </summary>
        public async Task<EmailTestResult> SendTestEmailAsync(string toEmail)
        {
            var status = GetSmtpStatus();

            if (!status.IsConfigured)
            {
                var notConfiguredResult = new EmailTestResult
                {
                    Success = false,
                    SmtpHost = status.SmtpHost,
                    SmtpPort = status.SmtpPort,
                    SenderEmail = status.SenderEmail,
                    SenderName = status.SenderName,
                    IsConfigured = false,
                    IsEnabled = status.IsEnabled,
                    ErrorMessage = "SMTP chưa được cấu hình. Vui lòng điền SenderEmail và Password hợp lệ trong appsettings.json."
                };
                await LogEmailAsync(toEmail, "[TEST] SMTP Diagnostic", "N/A", "DiagnosticTest", "Failed",
                    notConfiguredResult.ErrorMessage);
                return notConfiguredResult;
            }

            var subject = $"[TEST] NewComputerStore SMTP Diagnostic — {DateTime.Now:dd/MM/yyyy HH:mm}";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<body style=""font-family:'Segoe UI',Arial,sans-serif;background:#f4f6f9;padding:40px 0;"">
  <table align='center' width='500' style='background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,.08);'>
    <tr><td style='background:linear-gradient(135deg,#1a1a2e,#16213e);padding:30px;text-align:center;'>
      <h2 style='color:#fff;margin:0;'>✅ SMTP Test Thành Công</h2>
    </td></tr>
    <tr><td style='padding:30px;'>
      <p style='font-size:15px;color:#444;'>Email test được gửi lúc <strong>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</strong>.</p>
      <p style='font-size:14px;color:#666;'>Nếu bạn nhận được email này, cấu hình SMTP Gmail App Password của bạn đang hoạt động chính xác.</p>
      <hr style='border:none;border-top:1px solid #eee;margin:20px 0;'/>
      <table style='font-size:13px;color:#555;width:100%;'>
        <tr><td style='padding:4px 0;color:#888;'>SMTP Host:</td><td><strong>{_smtpServer}:{_smtpPort}</strong></td></tr>
        <tr><td style='padding:4px 0;color:#888;'>Sender:</td><td><strong>{_senderEmail}</strong></td></tr>
        <tr><td style='padding:4px 0;color:#888;'>Receiver:</td><td><strong>{toEmail}</strong></td></tr>
      </table>
    </td></tr>
    <tr><td style='background:#fafbfc;padding:16px;text-align:center;font-size:12px;color:#aaa;border-top:1px solid #f1f2f4;'>
      &copy; {DateTime.Now.Year} NewComputerStore
    </td></tr>
  </table>
</body>
</html>";

            try
            {
                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(_senderEmail, _senderName);
                    mailMessage.To.Add(new MailAddress(toEmail));
                    mailMessage.Subject = subject;
                    mailMessage.Body = htmlBody;
                    mailMessage.IsBodyHtml = true;
                    mailMessage.BodyEncoding = Encoding.UTF8;

                    using (var smtpClient = new SmtpClient(_smtpServer, _smtpPort))
                    {
                        smtpClient.UseDefaultCredentials = false;
                        smtpClient.Credentials = new NetworkCredential(_senderEmail, _password);
                        smtpClient.EnableSsl = true;
                        smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtpClient.Timeout = 15000;

                        await smtpClient.SendMailAsync(mailMessage);
                    }
                }

                await LogEmailAsync(toEmail, subject, htmlBody, "DiagnosticTest", "Success");

                return new EmailTestResult
                {
                    Success = true,
                    SmtpHost = _smtpServer,
                    SmtpPort = _smtpPort,
                    SenderEmail = _senderEmail,
                    SenderName = _senderName,
                    IsConfigured = true,
                    IsEnabled = _enableEmail
                };
            }
            catch (SmtpException smtpEx)
            {
                var errMsg = $"SmtpException [{smtpEx.StatusCode}]: {smtpEx.Message}";
                var innerMsg = smtpEx.InnerException?.Message;
                _logger.LogError(smtpEx, "SMTP Diagnostic test failed for {email}", toEmail);
                await LogEmailAsync(toEmail, subject, htmlBody, "DiagnosticTest", "Failed",
                    $"{errMsg}\n{innerMsg}\n{smtpEx.StackTrace}");

                return new EmailTestResult
                {
                    Success = false,
                    SmtpHost = _smtpServer,
                    SmtpPort = _smtpPort,
                    SenderEmail = _senderEmail,
                    SenderName = _senderName,
                    IsConfigured = true,
                    IsEnabled = _enableEmail,
                    ErrorMessage = errMsg,
                    InnerErrorMessage = innerMsg,
                    StackTrace = smtpEx.StackTrace,
                    SmtpErrorCode = smtpEx.StatusCode.ToString()
                };
            }
            catch (Exception ex)
            {
                var errMsg = ex.Message;
                var innerMsg = ex.InnerException?.Message;
                _logger.LogError(ex, "SMTP Diagnostic test failed (general) for {email}", toEmail);
                await LogEmailAsync(toEmail, subject, htmlBody, "DiagnosticTest", "Failed",
                    $"{errMsg}\n{innerMsg}\n{ex.StackTrace}");

                return new EmailTestResult
                {
                    Success = false,
                    SmtpHost = _smtpServer,
                    SmtpPort = _smtpPort,
                    SenderEmail = _senderEmail,
                    SenderName = _senderName,
                    IsConfigured = true,
                    IsEnabled = _enableEmail,
                    ErrorMessage = errMsg,
                    InnerErrorMessage = innerMsg,
                    StackTrace = ex.StackTrace
                };
            }
        }

        private async Task LogEmailAsync(string receiver, string subject, string body, string emailType, string status, string? errorMessage = null)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var log = new EmailLog
                    {
                        Receiver = receiver,
                        Subject = subject,
                        Body = body,
                        EmailType = emailType,
                        SentTime = DateTime.Now,
                        Status = status,
                        ErrorMessage = errorMessage
                    };
                    context.EmailLogs.Add(log);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log email to database.");
            }
        }

        private async Task SendEmailInternalAsync(string receiverEmail, string subject, string htmlBody, string emailType)
        {
            if (!_enableEmail)
            {
                _logger.LogInformation("Email sending is disabled. Skipping email to {receiverEmail}", receiverEmail);
                // Log as Skipped (not Success) so the EmailLogs dashboard correctly reflects reality
                await LogEmailAsync(receiverEmail, subject, htmlBody, emailType, "Skipped",
                    "EnableEmail=false in configuration. Email was not sent.");
                return;
            }

            // Detect placeholder or missing credentials before attempting SMTP
            bool credentialsMissing = string.IsNullOrWhiteSpace(_senderEmail)
                || string.IsNullOrWhiteSpace(_password)
                || _password == "xxxx xxxx xxxx xxxx"
                || _password.Length < 8;

            if (credentialsMissing)
            {
                _logger.LogWarning("Email credentials are not configured or are placeholder values. Email to {receiverEmail} skipped.", receiverEmail);
                await LogEmailAsync(receiverEmail, subject, htmlBody, emailType, "Failed",
                    "SenderEmail or Password is empty / placeholder in appsettings.json. Configure a valid Gmail App Password.");
                return;
            }

            try
            {
                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(_senderEmail, _senderName);
                    mailMessage.To.Add(new MailAddress(receiverEmail));
                    mailMessage.Subject = subject;
                    mailMessage.Body = htmlBody;
                    mailMessage.IsBodyHtml = true;
                    mailMessage.BodyEncoding = Encoding.UTF8;
                    mailMessage.SubjectEncoding = Encoding.UTF8;

                    using (var smtpClient = new SmtpClient(_smtpServer, _smtpPort))
                    {
                        smtpClient.UseDefaultCredentials = false;
                        smtpClient.Credentials = new NetworkCredential(_senderEmail, _password);
                        smtpClient.EnableSsl = true;
                        smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtpClient.Timeout = 15000; // 15 seconds

                        await smtpClient.SendMailAsync(mailMessage);
                    }
                }

                await LogEmailAsync(receiverEmail, subject, htmlBody, emailType, "Success");
            }
            catch (SmtpException smtpEx)
            {
                var detail = $"SmtpException [{smtpEx.StatusCode}]: {smtpEx.Message}";
                if (smtpEx.InnerException != null)
                    detail += $" | Inner: {smtpEx.InnerException.Message}";
                _logger.LogError(smtpEx, "SMTP error sending email to {receiverEmail}.", receiverEmail);
                await LogEmailAsync(receiverEmail, subject, htmlBody, emailType, "Failed", detail);
                // DO NOT rethrow — must not disrupt business flow
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email to {receiverEmail}.", receiverEmail);
                await LogEmailAsync(receiverEmail, subject, htmlBody, emailType, "Failed",
                    $"{ex.GetType().Name}: {ex.Message}" + (ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : ""));
                // DO NOT rethrow — must not disrupt business flow
            }
        }

        public async Task SendOtpEmailAsync(string receiverEmail, string otp)
        {
            var subject = "Xác thực tài khoản NewComputerStore";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Xác thực tài khoản</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; color: #333;"">
    <table align='center' border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 8px 30px rgba(0,0,0,0.06);'>
        <tr>
            <td style='background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); padding: 40px 20px; text-align: center;'>
                <h1 style='color: #ffffff; margin: 0; font-size: 26px; font-weight: 700; letter-spacing: 0.5px;'>NewComputerStore</h1>
            </td>
        </tr>
        <tr>
            <td style='padding: 40px 30px;'>
                <h2 style='color: #1a1a2e; margin-top: 0; font-size: 20px; font-weight: 600;'>Xác thực tài khoản của bạn</h2>
                <p style='font-size: 16px; line-height: 1.6; color: #555; margin-bottom: 24px;'>
                    Cảm ơn bạn đã đăng ký tài khoản tại <strong>NewComputerStore</strong>. Vui lòng sử dụng mã OTP dưới đây để xác thực email của bạn:
                </p>
                <div style='text-align: center; margin: 35px 0;'>
                    <div style='background-color: #f8f9fa; border: 2px dashed #e94560; color: #e94560; font-size: 36px; font-weight: 800; letter-spacing: 6px; padding: 20px; border-radius: 12px; display: inline-block; min-width: 200px; text-align: center; font-family: monospace;'>
                        {otp}
                    </div>
                </div>
                <p style='font-size: 15px; line-height: 1.6; color: #333; font-weight: 500;'>
                    Mã này sẽ hết hạn sau 5 phút.
                </p>
                <p style='font-size: 14px; line-height: 1.6; color: #777;'>
                    Vì lý do bảo mật, <strong>vui lòng tuyệt đối không chia sẻ mã này cho bất kỳ ai</strong>.
                </p>
                <hr style='border: none; border-top: 1px solid #eaeaea; margin: 30px 0;'>
                <p style='font-size: 12px; line-height: 1.5; color: #a0a0a0;'>
                    Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này hoặc liên hệ bộ phận hỗ trợ khách hàng của chúng tôi.
                </p>
            </td>
        </tr>
        <tr>
            <td style='background-color: #fafbfc; padding: 20px; text-align: center; font-size: 12px; color: #999; border-top: 1px solid #f1f2f4;'>
                © {DateTime.Now.Year} NewComputerStore. Bảo lưu mọi quyền.<br>
                Địa chỉ: 123 Đường Công Nghệ, Quận 1, TP. Hồ Chí Minh
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailInternalAsync(receiverEmail, subject, body, "ConfirmEmail");
        }

        public async Task SendWelcomeEmailAsync(string receiverEmail, string fullName)
        {
            var subject = "Chào mừng bạn đến với NewComputerStore!";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Chào mừng bạn</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; color: #333;"">
    <table align='center' border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 8px 30px rgba(0,0,0,0.06);'>
        <tr>
            <td style='background: linear-gradient(135deg, #198754 0%, #146c43 100%); padding: 40px 20px; text-align: center;'>
                <h1 style='color: #ffffff; margin: 0; font-size: 26px; font-weight: 700; letter-spacing: 0.5px;'>Xác thực thành công!</h1>
            </td>
        </tr>
        <tr>
            <td style='padding: 40px 30px;'>
                <h2 style='color: #146c43; margin-top: 0; font-size: 22px; font-weight: 600;'>Chào mừng {fullName},</h2>
                <p style='font-size: 16px; line-height: 1.6; color: #555;'>
                    Chúc mừng bạn đã xác thực email thành công! Tài khoản của bạn đã được kích hoạt hoàn toàn. Từ bây giờ bạn có thể trải nghiệm toàn bộ mua sắm, nhận mã giảm giá và theo dõi đơn hàng của mình tại cửa hàng của chúng tôi.
                </p>
                <div style='background-color: #f8faf9; border-left: 4px solid #198754; padding: 15px; margin: 25px 0; border-radius: 0 8px 8px 0;'>
                    <h4 style='margin: 0 0 5px 0; color: #146c43; font-size: 15px;'>Mẹo bảo mật tài khoản:</h4>
                    <p style='margin: 0; font-size: 13px; color: #666; line-height: 1.4;'>
                        Không chia sẻ mật khẩu của bạn với bất kỳ ai. Hệ thống của chúng tôi được bảo vệ nghiêm ngặt và không bao giờ yêu cầu bạn cung cấp mật khẩu qua email.
                    </p>
                </div>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='https://localhost:5001' style='background-color: #198754; color: #ffffff; text-decoration: none; padding: 14px 30px; font-size: 16px; font-weight: 600; border-radius: 30px; display: inline-block; box-shadow: 0 4px 15px rgba(25, 135, 84, 0.3);'>
                        Khám phá Cửa hàng ngay
                    </a>
                </div>
            </td>
        </tr>
        <tr>
            <td style='background-color: #fafbfc; padding: 20px; text-align: center; font-size: 12px; color: #999; border-top: 1px solid #f1f2f4;'>
                © {DateTime.Now.Year} NewComputerStore. Bảo lưu mọi quyền.
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailInternalAsync(receiverEmail, subject, body, "WelcomeEmail");
        }

        public async Task SendPasswordResetEmailAsync(string receiverEmail, string resetLink)
        {
            var subject = "Yêu cầu khôi phục mật khẩu - NewComputerStore";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Khôi phục mật khẩu</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; color: #333;"">
    <table align='center' border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 8px 30px rgba(0,0,0,0.06);'>
        <tr>
            <td style='background: linear-gradient(135deg, #0d6efd 0%, #0a58ca 100%); padding: 40px 20px; text-align: center;'>
                <h1 style='color: #ffffff; margin: 0; font-size: 26px; font-weight: 700; letter-spacing: 0.5px;'>Khôi phục mật khẩu</h1>
            </td>
        </tr>
        <tr>
            <td style='padding: 40px 30px;'>
                <h2 style='color: #0d6efd; margin-top: 0; font-size: 20px; font-weight: 600;'>Chào bạn,</h2>
                <p style='font-size: 16px; line-height: 1.6; color: #555; margin-bottom: 24px;'>
                    Chúng tôi nhận được yêu cầu khôi phục mật khẩu cho tài khoản liên kết với địa chỉ email này. Để tiến hành đặt lại mật khẩu mới, vui lòng bấm vào nút bên dưới:
                </p>
                <div style='text-align: center; margin: 35px 0;'>
                    <a href='{resetLink}' style='background-color: #0d6efd; color: #ffffff; text-decoration: none; padding: 15px 35px; font-size: 16px; font-weight: 600; border-radius: 30px; display: inline-block; box-shadow: 0 4px 15px rgba(13, 110, 253, 0.4);'>
                        Đặt lại mật khẩu mới
                    </a>
                </div>
                <p style='font-size: 14px; line-height: 1.6; color: #ff6b6b; margin-top: 30px; font-weight: 500;'>
                    * Liên kết khôi phục này có hiệu lực trong vòng <strong>15 phút</strong>.
                </p>
                <p style='font-size: 14px; line-height: 1.6; color: #888;'>
                    Nếu bạn không yêu cầu đổi mật khẩu, vui lòng bỏ qua email này hoặc liên hệ ngay với bộ phận hỗ trợ khách hàng để được đảm bảo an toàn.
                </p>
                <hr style='border: none; border-top: 1px solid #eaeaea; margin: 30px 0;'>
                <p style='font-size: 12px; line-height: 1.5; color: #a0a0a0;'>
                    Nếu nút bấm không hoạt động, vui lòng dán đường link sau vào thanh địa chỉ của trình duyệt:<br>
                    <a href='{resetLink}' style='color: #0d6efd; text-decoration: none; word-break: break-all;'>{resetLink}</a>
                </p>
            </td>
        </tr>
        <tr>
            <td style='background-color: #fafbfc; padding: 20px; text-align: center; font-size: 12px; color: #999; border-top: 1px solid #f1f2f4;'>
                © {DateTime.Now.Year} NewComputerStore. Bảo lưu mọi quyền.
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailInternalAsync(receiverEmail, subject, body, "ResetPassword");
        }

        public async Task SendOrderStatusEmailAsync(string receiverEmail, Order order, string status)
        {
            var statusVietnamese = status switch
            {
                "Pending" => "ĐANG CHỜ DUYỆT",
                "Processing" => "ĐÃ XÁC NHẬN (ĐANG XỬ LÝ)",
                "Shipped" => "ĐANG GIAO HÀNG",
                "Delivered" => "ĐÃ GIAO HÀNG THÀNH CÔNG",
                "Cancelled" => "ĐÃ HỦY ĐƠN",
                "Refunded" => "ĐÃ HOÀN TIỀN THÀNH CÔNG",
                _ => status.ToUpper()
            };

            var emailType = status switch
            {
                "Pending" => "OrderCreated",
                "Processing" => "OrderConfirmed",
                "Shipped" => "OrderShipping",
                "Delivered" => "OrderDelivered",
                "Cancelled" => "OrderCancelled",
                "Refunded" => "OrderCancelled", // Fallback logic or separate
                _ => "OrderCreated"
            };

            var accentColor = status switch
            {
                "Pending" => "#ffc107",
                "Processing" => "#0d6efd",
                "Shipped" => "#17a2b8",
                "Delivered" => "#198754",
                "Cancelled" => "#dc3545",
                "Refunded" => "#6f42c1",
                _ => "#1a1a2e"
            };

            var textColor = status == "Pending" ? "#333333" : "#ffffff";

            var subject = $"Cập nhật đơn hàng #{order.OrderId} - NewComputerStore";

            // Generate order details HTML
            var itemsHtml = new StringBuilder();
            if (order.OrderDetails != null)
            {
                foreach (var item in order.OrderDetails)
                {
                    var colorText = !string.IsNullOrEmpty(item.ColorName) ? $" (Màu: {item.ColorName})" : "";
                    itemsHtml.Append($@"
                    <tr>
                        <td style='padding: 12px; border-bottom: 1px solid #eeeeee; font-size: 14px;'>
                            {item.Product?.ProductName ?? "Sản phẩm"}{colorText}
                        </td>
                        <td style='padding: 12px; border-bottom: 1px solid #eeeeee; font-size: 14px; text-align: center;'>
                            {item.Quantity}
                        </td>
                        <td style='padding: 12px; border-bottom: 1px solid #eeeeee; font-size: 14px; text-align: right; font-weight: 600;'>
                            {item.FinalPrice.ToString("#,##0")}đ
                        </td>
                    </tr>");
                }
            }

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Cập nhật trạng thái đơn hàng</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; color: #333;"">
    <table align='center' border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 8px 30px rgba(0,0,0,0.06);'>
        <tr>
            <td style='background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); padding: 30px 20px; text-align: center;'>
                <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: 700; letter-spacing: 0.5px;'>Cập Nhật Đơn Hàng #{order.OrderId}</h1>
            </td>
        </tr>
        <tr>
            <td style='padding: 30px;'>
                <p style='font-size: 16px; color: #555; margin-top: 0;'>Chào <strong>{order.FullName}</strong>,</p>
                <p style='font-size: 15px; color: #555; line-height: 1.6;'>
                    Đơn hàng <strong>#{order.OrderId}</strong> của bạn đặt lúc {order.OrderDate.ToString("dd/MM/yyyy HH:mm")} vừa được cập nhật sang trạng thái:
                </p>
                
                <div style='background-color: {accentColor}; color: {textColor}; text-align: center; font-size: 18px; font-weight: 700; padding: 15px; border-radius: 8px; margin: 25px 0; letter-spacing: 0.5px; box-shadow: 0 4px 10px rgba(0,0,0,0.05);'>
                    {statusVietnamese}
                </div>

                <h3 style='color: #1a1a2e; border-bottom: 2px solid #1a1a2e; padding-bottom: 8px; font-size: 16px; font-weight: 600; margin-top: 30px;'>THÔNG TIN GIAO HÀNG</h3>
                <table width='100%' cellpadding='0' cellspacing='0' style='font-size: 14px; line-height: 1.6; margin-bottom: 25px;'>
                    <tr><td width='120' style='color: #888; padding: 4px 0;'>Người nhận:</td><td style='font-weight: 600;'>{order.FullName}</td></tr>
                    <tr><td style='color: #888; padding: 4px 0;'>Điện thoại:</td><td>{order.Phone}</td></tr>
                    <tr><td style='color: #888; padding: 4px 0;'>Địa chỉ giao:</td><td>{order.Address}</td></tr>
                    <tr><td style='color: #888; padding: 4px 0;'>Thanh toán:</td><td><span style='background-color: #f1f2f4; padding: 2px 8px; border-radius: 4px; font-size: 12px; font-weight: 600;'>{order.PaymentMethod} - {order.PaymentStatus}</span></td></tr>
                </table>

                <h3 style='color: #1a1a2e; border-bottom: 2px solid #1a1a2e; padding-bottom: 8px; font-size: 16px; font-weight: 600; margin-top: 30px;'>CHI TIẾT ĐƠN HÀNG</h3>
                <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse: collapse; margin-bottom: 20px;'>
                    <thead>
                        <tr style='background-color: #f8fafc;'>
                            <th style='padding: 12px; border-bottom: 2px solid #eaeaea; font-size: 13px; text-align: left; color: #555;'>Sản phẩm</th>
                            <th style='padding: 12px; border-bottom: 2px solid #eaeaea; font-size: 13px; text-align: center; color: #555; width: 60px;'>SL</th>
                            <th style='padding: 12px; border-bottom: 2px solid #eaeaea; font-size: 13px; text-align: right; color: #555; width: 100px;'>Thành tiền</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtml}
                        <tr>
                            <td colspan='2' style='padding: 15px 12px; font-size: 16px; font-weight: 700; text-align: right; color: #1a1a2e;'>TỔNG TIỀN:</td>
                            <td style='padding: 15px 12px; font-size: 18px; font-weight: 700; text-align: right; color: #dc3545;'>
                                {order.TotalPrice.ToString("#,##0")}đ
                            </td>
                        </tr>
                    </tbody>
                </table>

                <p style='font-size: 14px; color: #666; line-height: 1.6; margin-top: 30px;'>
                    Bạn có thể truy cập website để kiểm tra hành trình vận chuyển bất cứ lúc nào trong trang tài khoản cá nhân. Cảm ơn bạn đã đồng hành cùng NewComputerStore!
                </p>
            </td>
        </tr>
        <tr>
            <td style='background-color: #fafbfc; padding: 20px; text-align: center; font-size: 12px; color: #999; border-top: 1px solid #f1f2f4;'>
                © {DateTime.Now.Year} NewComputerStore. Bảo lưu mọi quyền.
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailInternalAsync(receiverEmail, subject, body, emailType);
        }

        public async Task SendSystemNotificationAsync(string receiverEmail, string subject, string message)
        {
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thông báo từ hệ thống</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; color: #333;"">
    <table align='center' border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 8px 30px rgba(0,0,0,0.06);'>
        <tr>
            <td style='background: linear-gradient(135deg, #222831 0%, #393e46 100%); padding: 30px 20px; text-align: center;'>
                <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: 700; letter-spacing: 0.5px;'>Thông Báo Sự Cố</h1>
            </td>
        </tr>
        <tr>
            <td style='padding: 40px 30px;'>
                <h2 style='color: #e94560; margin-top: 0; font-size: 18px; font-weight: 600;'>Thông tin chi tiết sự cố:</h2>
                <div style='background-color: #fff6f6; border-left: 4px solid #dc3545; padding: 20px; border-radius: 0 8px 8px 0; margin: 20px 0;'>
                    <p style='margin: 0; font-size: 15px; color: #dc3545; line-height: 1.6; font-weight: 500;'>
                        {message}
                    </p>
                </div>
                <p style='font-size: 14px; line-height: 1.6; color: #666; margin-top: 30px;'>
                    Chúng tôi chân thành xin lỗi vì sự bất tiện này gây ra. Nếu bạn cần hỗ trợ thêm, vui lòng liên hệ ngay hotline chăm sóc khách hàng của NewComputerStore để được xử lý ưu tiên.
                </p>
            </td>
        </tr>
        <tr>
            <td style='background-color: #fafbfc; padding: 20px; text-align: center; font-size: 12px; color: #999; border-top: 1px solid #f1f2f4;'>
                © {DateTime.Now.Year} NewComputerStore. Bảo lưu mọi quyền.
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailInternalAsync(receiverEmail, subject, body, "SecurityAlert"); // or system notifications
        }

        public async Task SendSecurityAlertAsync(string receiverEmail, string alertSubject, string alertMessage)
        {
            var subject = $"Cảnh báo bảo mật: {alertSubject} - NewComputerStore";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Cảnh báo bảo mật</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; color: #333;"">
    <table align='center' border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 8px 30px rgba(0,0,0,0.06);'>
        <tr>
            <td style='background: #e94560; padding: 30px 20px; text-align: center;'>
                <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: 700; letter-spacing: 0.5px;'>CẢNH BÁO BẢO MẬT</h1>
            </td>
        </tr>
        <tr>
            <td style='padding: 40px 30px;'>
                <h2 style='color: #1a1a2e; margin-top: 0; font-size: 18px; font-weight: 600;'>Thông tin thay đổi bảo mật:</h2>
                <p style='font-size: 15px; color: #333; line-height: 1.6; margin-bottom: 24px;'>
                    Chúng tôi phát hiện tài khoản của bạn vừa thực hiện hành động: <strong>{alertMessage}</strong> vào lúc {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}.
                </p>
                <div style='background-color: #fff9e6; border-left: 4px solid #ffc107; padding: 15px; border-radius: 0 8px 8px 0;'>
                    <p style='margin: 0; font-size: 13px; color: #666; line-height: 1.5;'>
                        <strong>Quan trọng:</strong> Nếu bạn không phải người thực hiện hành động này, rất có thể tài khoản của bạn đang bị xâm nhập trái phép. Hãy lập tức đổi mật khẩu hoặc liên hệ với ban quản trị website ngay để được tạm khóa tài khoản.
                    </p>
                </div>
            </td>
        </tr>
        <tr>
            <td style='background-color: #fafbfc; padding: 20px; text-align: center; font-size: 12px; color: #999; border-top: 1px solid #f1f2f4;'>
                © {DateTime.Now.Year} NewComputerStore. Bảo lưu mọi quyền.
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailInternalAsync(receiverEmail, subject, body, "SecurityAlert");
        }
    }
}
