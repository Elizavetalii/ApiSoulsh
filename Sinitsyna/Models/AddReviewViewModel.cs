namespace Sinitsyna.Models
{
    public class AddReviewViewModel
    {
        public int ProductId { get; set; } // ID товара, к которому относится отзыв
        public int Rating { get; set; } // Оценка (1-5)
        public string Text { get; set; }
    }
}
