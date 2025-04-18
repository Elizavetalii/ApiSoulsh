using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class BoutiquesImage
    {
        [Key]
        [JsonPropertyName("idImage")]
        public int Id_image { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("idBoutique")]
        public int? Id_boutique { get; set; }

        [JsonPropertyName("idBoutiqueNavigation")]
        public Boutique Id_boutique_navigation { get; set; }
    }
}
