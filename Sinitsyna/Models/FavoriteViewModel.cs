namespace Sinitsyna.Models
{
    public class FavoriteViewModel
    {
        public int Id_product { get; set; }
        public string Product_name { get; set; }
        public decimal Price { get; set; }
        public string Url_image { get; set; } // URL первого изображения товара
    }
}
