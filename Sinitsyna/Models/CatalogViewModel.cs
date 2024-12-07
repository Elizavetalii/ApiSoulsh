namespace Sinitsyna.Models
{
    public class CatalogViewModel
    {
        public IEnumerable<Product> Products { get; set; }
        public IEnumerable<ProductMaterial> Materials { get; set; }
        public IEnumerable<ProductType> Types { get; set; }

        public IEnumerable<ProductImage> Url_image { get; set; }

        public ShoppingCart ShoppingCart { get; set; } // Добавляем корзину
    }
}
