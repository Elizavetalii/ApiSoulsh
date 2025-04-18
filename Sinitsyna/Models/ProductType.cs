using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class ProductType
    {
        [Key]
        [JsonPropertyName("idType")]
        public int Id_type { get; set; }

        [JsonPropertyName("productTypeName")]
        public string Product_type_name { get; set; }
    }
}
