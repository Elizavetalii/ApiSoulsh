namespace Sinitsyna.Models
{
    public class ReviewViewModel
    {
        public string UserName { get; set; } // Имя пользователя, оставившего отзыв
        public int Rating { get; set; } // Оценка товара (звёзды)
        public string Text { get; set; } // Текст отзыва
        public DateTime CreatedDate { get; set; } // Дата создания отзыва
    }
}
