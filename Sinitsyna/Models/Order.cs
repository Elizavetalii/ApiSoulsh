using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class Order
    {
        [Key]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("orderDate")]
        public DateTime OrderDate { get; set; }

        [JsonPropertyName("totalPrice")]
        public decimal TotalPrice { get; set; }

        [JsonPropertyName("userId")]
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        [JsonPropertyName("user")]
        public virtual User User { get; set; }

        [JsonPropertyName("orderItems")]
        public virtual ICollection<OrderItem> OrderItems { get; set; } // Связь с элементами заказа
    }

    public class OrderItem
    {
        [Key]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("productId")]
        public int ProductId { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("product")]
        public virtual Product Product { get; set; } // Связь с продуктом
    }
    public class SalesAnalytics
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

}
