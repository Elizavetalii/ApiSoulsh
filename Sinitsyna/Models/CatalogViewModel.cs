namespace Sinitsyna.Models
{
    public class CatalogViewModel
    {
        public IEnumerable<Product> Products { get; set; }
        public IEnumerable<ProductMaterial> Materials { get; set; }
        public IEnumerable<ProductType> Types { get; set; }

        public IEnumerable<ProductImage> Url_image { get; set; }

        public ShoppingCart ShoppingCart { get; set; }
        public Dictionary<int, int> ReviewCounts { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, decimal> AverageRatings { get; set; } = new Dictionary<int, decimal>();

        public bool IsFavorite { get; set; }



    }
}
