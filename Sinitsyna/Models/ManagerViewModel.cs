namespace Sinitsyna.Models
{
    public class ManagerViewModel
    {
        public IEnumerable<Product> Products { get; set; }
        public IEnumerable<Boutique> Boutiques { get; set; }
        public IEnumerable<ProductMaterial> Materials { get; set; }
        public IEnumerable<ProductType> Types { get; set; }
        public decimal TotalSales { get; set; }
        public List<SalesDistribution> SalesDistributions { get; set; }
        public List<decimal> SalesOverTime { get; set; }
    }

    public class SalesDistribution
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int TotalQuantity { get; set; }
    }
}
