namespace Sinitsyna.Models
{
    public class DetailsViewModel
    {
        public Product Product { get; set; }
        public List<ReviewViewModel> Reviews { get; set; }
        public AddReviewViewModel AddReview { get; set; }
        public decimal AverageRating { get; set; }

        public bool IsFavorite { get; set; }

        public int ReviewsCount { get; set; }

    }
}