using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Dtos;
using NewComputerStore.Models;

namespace NewComputerStore.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ReviewsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReviewsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequestDto model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Token không chứa thông tin định danh người dùng hợp lệ.",
                    "UNAUTHORIZED");
            }

            if (model.Rating < 1 || model.Rating > 5)
            {
                return CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Đánh giá không hợp lệ",
                    "Điểm đánh giá (rating) phải từ 1 đến 5.",
                    "INVALID_RATING");
            }

            if (string.IsNullOrWhiteSpace(model.Comment))
            {
                return CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Đánh giá không hợp lệ",
                    "Nội dung bình luận không được để trống.",
                    "INVALID_COMMENT");
            }

            var productExists = await _context.Products
                .AsNoTracking()
                .AnyAsync(p => p.ProductId == model.ProductId && !p.IsDeleted && !p.IsHidden);

            if (!productExists)
            {
                return CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Không tìm thấy sản phẩm",
                    $"Không tìm thấy sản phẩm #{model.ProductId} hoặc sản phẩm đã ngừng kinh doanh.",
                    "PRODUCT_NOT_FOUND");
            }

            var review = new Review
            {
                ProductId = model.ProductId,
                UserId = userId,
                Rating = model.Rating,
                Comment = model.Comment.Trim()
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            var userFullName = User.FindFirstValue(ClaimTypes.Name)
                               ?? await _context.Users
                                   .AsNoTracking()
                                   .Where(u => u.UserId == userId)
                                   .Select(u => u.FullName)
                                   .FirstOrDefaultAsync()
                               ?? "Khách hàng";

            var dto = new ReviewDto
            {
                ReviewId = review.ReviewId,
                ProductId = review.ProductId,
                UserId = review.UserId,
                UserFullName = userFullName,
                Rating = review.Rating,
                Comment = review.Comment
            };

            return StatusCode(StatusCodes.Status201Created, dto);
        }

        private ObjectResult CreateProblem(int statusCode, string title, string detail, string code)
        {
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = Request.Path
            };
            problem.Extensions["code"] = code;

            return new ObjectResult(problem)
            {
                StatusCode = statusCode
            };
        }
    }
}
