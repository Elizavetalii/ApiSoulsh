using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class ProductImage
    {
        [Key]
        [JsonPropertyName("idImage")]
        public int Id_image { get; set; }

        [JsonPropertyName("urlImage")]
        public string Url_image { get; set; }

        [JsonPropertyName("idProduct")]
        public int Id_product { get; set; }

        [ForeignKey("Id_product")]
        [JsonPropertyName("idProductNavigation")]
        public Product Product { get; set; }
    }
}
