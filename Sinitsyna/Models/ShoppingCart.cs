namespace Sinitsyna.Models
{
    public class CartLine
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public int ReservedQuantity { get; set; }
        public string BoutiqueAddress { get; set; }
        public TimeSpan OpeningTime { get; set; }
        public TimeSpan ClosingTime { get; set; }
    }

    public class ShoppingCart
    {
        public ShoppingCart()
        {
            CartLines = new List<CartLine>();
        }

        public List<CartLine> CartLines { get; set; }

        public decimal FinalPrice
        {
            get
            {
                decimal price = 0;
                foreach (var cartLine in CartLines)
                {
                    price += cartLine.Price * cartLine.Quantity;
                }
                return price;
            }
        }
    }
}
