using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Dtos;

namespace NewComputerStore.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MeController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMe()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return UnauthorizedProblem("Token không chứa thông tin định danh hợp lệ.");
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);

            if (user == null || !user.IsActive)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy người dùng",
                    Detail = "Tài khoản không tồn tại hoặc đã bị vô hiệu hóa.",
                    Instance = Request.Path
                };
                problem.Extensions["code"] = "USER_NOT_FOUND";
                return NotFound(problem);
            }

            return Ok(new UserDto
            {
                Id = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            });
        }

        private ObjectResult UnauthorizedProblem(string detail)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = detail,
                Instance = Request.Path
            };
            problem.Extensions["code"] = "UNAUTHORIZED";
            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
    }
}
