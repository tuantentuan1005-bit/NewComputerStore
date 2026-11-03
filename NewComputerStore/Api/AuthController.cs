using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewComputerStore.Dtos;
using NewComputerStore.Models;
using NewComputerStore.Services;

namespace NewComputerStore.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAccountAuthService _accountAuthService;
        private readonly ITokenService _tokenService;

        public AuthController(IAccountAuthService accountAuthService, ITokenService tokenService)
        {
            _accountAuthService = accountAuthService;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto model)
        {
            var result = await _accountAuthService.RegisterCustomerAsync(model.FullName, model.Email, model.Password);
            if (!result.Succeeded)
            {
                int status = result.ErrorCode == "EMAIL_ALREADY_EXISTS"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;

                return CreateProblem(status, "Đăng ký thất bại", result.Message, result.ErrorCode ?? "REGISTER_FAILED");
            }

            return Ok(new AuthMessageResponseDto
            {
                Message = result.Message,
                Email = result.User?.Email ?? model.Email
            });
        }

        [HttpPost("verify-otp")]
        [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDto model)
        {
            var result = await _accountAuthService.VerifyOtpAsync(model.Email, model.Otp);
            if (!result.Succeeded)
            {
                int status = result.ErrorCode == "USER_NOT_FOUND"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;

                return CreateProblem(status, "Xác thực OTP thất bại", result.Message, result.ErrorCode ?? "OTP_VERIFY_FAILED");
            }

            return Ok(new AuthMessageResponseDto
            {
                Message = result.Message,
                Email = model.Email
            });
        }

        [HttpPost("resend-otp")]
        [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequestDto model)
        {
            var result = await _accountAuthService.ResendOtpAsync(model.Email);
            if (!result.Succeeded)
            {
                int status = result.ErrorCode switch
                {
                    "USER_NOT_FOUND" => StatusCodes.Status404NotFound,
                    "OTP_THROTTLED" => StatusCodes.Status429TooManyRequests,
                    _ => StatusCodes.Status400BadRequest
                };

                var extras = result.RetryAfterSeconds.HasValue
                    ? new Dictionary<string, object?> { ["retryAfterSeconds"] = result.RetryAfterSeconds.Value }
                    : null;

                return CreateProblem(status, "Gửi lại OTP thất bại", result.Message, result.ErrorCode ?? "RESEND_OTP_FAILED", extras);
            }

            return Ok(new AuthMessageResponseDto
            {
                Message = result.Message,
                Email = model.Email
            });
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
        {
            var result = await _accountAuthService.AuthenticateAsync(model.Email, model.Password);

            if (!result.Succeeded)
            {
                if (result.ErrorCode == "EMAIL_NOT_VERIFIED")
                {
                    return CreateProblem(
                        StatusCodes.Status403Forbidden,
                        "Tài khoản chưa xác thực email",
                        result.Message,
                        "EMAIL_NOT_VERIFIED",
                        new Dictionary<string, object?> { ["email"] = model.Email });
                }

                if (result.ErrorCode == "ACCOUNT_LOCKED")
                {
                    return CreateProblem(
                        StatusCodes.Status403Forbidden,
                        "Tài khoản đã bị khóa",
                        result.Message,
                        "ACCOUNT_LOCKED");
                }

                return CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Đăng nhập thất bại",
                    result.Message,
                    "INVALID_CREDENTIALS");
            }

            var user = result.User!;
            var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user);

            return Ok(new LoginResponseDto
            {
                AccessToken = accessToken,
                ExpiresAt = expiresAt,
                User = MapToUserDto(user)
            });
        }

        [HttpPost("google")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto model)
        {
            var result = await _accountAuthService.GoogleLoginWithIdTokenAsync(model.IdToken);
            if (!result.Succeeded || result.User == null)
            {
                int status = result.ErrorCode == "INVALID_GOOGLE_TOKEN"
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status400BadRequest;

                return CreateProblem(
                    status,
                    "Đăng nhập Google thất bại",
                    result.Message,
                    result.ErrorCode ?? "GOOGLE_LOGIN_FAILED");
            }

            var user = result.User;
            var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user);

            return Ok(new LoginResponseDto
            {
                AccessToken = accessToken,
                ExpiresAt = expiresAt,
                User = MapToUserDto(user)
            });
        }

        [HttpPost("forgot-password")]
        [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto model)
        {
            await _accountAuthService.ForgotPasswordAsync(
                model.Email,
                (userEmail, token) => Url.Action("ResetPassword", "Account", new { email = userEmail, token = token }, Request.Scheme)!);

            return Ok(new AuthMessageResponseDto
            {
                Message = "Nếu email trên tồn tại trong hệ thống của chúng tôi, một liên kết đặt lại mật khẩu đã được gửi đến hộp thư của bạn (hiệu lực trong 15 phút).",
                Email = model.Email
            });
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto model)
        {
            var result = await _accountAuthService.ResetPasswordAsync(model.Email, model.Token, model.NewPassword);
            if (!result.Succeeded)
            {
                int status = result.ErrorCode == "USER_NOT_FOUND"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;

                return CreateProblem(status, "Đặt lại mật khẩu thất bại", result.Message, result.ErrorCode ?? "RESET_PASSWORD_FAILED");
            }

            return Ok(new AuthMessageResponseDto
            {
                Message = result.Message,
                Email = model.Email
            });
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            };
        }

        private ObjectResult CreateProblem(
            int statusCode,
            string title,
            string detail,
            string code,
            IDictionary<string, object?>? extensions = null)
        {
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = Request.Path
            };
            problem.Extensions["code"] = code;

            if (extensions != null)
            {
                foreach (var kvp in extensions)
                {
                    problem.Extensions[kvp.Key] = kvp.Value;
                }
            }

            return new ObjectResult(problem)
            {
                StatusCode = statusCode
            };
        }
    }
}
