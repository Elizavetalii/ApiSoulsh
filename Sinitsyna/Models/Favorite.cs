using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class Favorite
    {
        [Key]
        [JsonPropertyName("idFavorite")]
        public int Id_favorite { get; set; }

        [JsonPropertyName("idUser")]
        public int? Id_user { get; set; }

        [JsonPropertyName("idProduct")]
        public int? Id_product { get; set; }

        [JsonPropertyName("idProductNavigation")]
        public Product Id_product_navigation { get; set; }

        [JsonPropertyName("idUserNavigation")]
        public User Id_user_navigation { get; set; }
    }
}
