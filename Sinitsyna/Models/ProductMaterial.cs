using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class ProductMaterial
    {
        [Key]
        [JsonPropertyName("idMaterial")]
        public int Id_material { get; set; }

        [JsonPropertyName("materialName")]
        public string Material_name { get; set; }
    }
}
