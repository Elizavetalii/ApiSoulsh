namespace Sinitsyna.Models
{
    public class CartLine
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public int ReservedQuantity { get; set; }

        // Новые свойства для хранения информации о бутике
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
                    price += cartLine.Product.Price * cartLine.Quantity;
                }
                return price;
            }
        }
    }
}