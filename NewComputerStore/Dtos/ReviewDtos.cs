using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NewComputerStore.Dtos
{
    public class CreateReviewRequestDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Điểm đánh giá phải từ 1 đến 5 sao.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Nội dung đánh giá không được để trống.")]
        [StringLength(1000, ErrorMessage = "Nội dung đánh giá tối đa 1000 ký tự.")]
        public string Comment { get; set; } = string.Empty;
    }

    public class ReviewDto
    {
        public int ReviewId { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class ProductReviewsResponseDto
    {
        public int ProductId { get; set; }
        public int TotalReviews { get; set; }
        public double AverageRating { get; set; }
        public List<ReviewDto> Items { get; set; } = new List<ReviewDto>();
    }
}
