using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class Boutique
    {
        [Key]
        [JsonPropertyName("idBoutique")]
        public int Id_boutique { get; set; }

        [JsonPropertyName("boutiqueAddress")]
        public string Boutique_address { get; set; }

        // В API используется TimeOnly, в модели TimeSpan — убедитесь в соответствии или конвертации
        [JsonPropertyName("openingTime")]
        public TimeSpan Opening_time { get; set; }

        [JsonPropertyName("closingTime")]
        public TimeSpan Closing_time { get; set; }

        [JsonPropertyName("products")]
        public ICollection<Product> Products { get; set; }
    }
}
