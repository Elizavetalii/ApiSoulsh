using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class Role
    {
        [Key]
        [JsonPropertyName("idRole")]
        public int Id_role { get; set; }

        [Required(ErrorMessage = "Название роли обязательно для заполнения.")]
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s]+$", ErrorMessage = "Название роли должно содержать только буквы.")]
        [JsonPropertyName("roleName")]
        public string Role_name { get; set; }

        [JsonPropertyName("users")]
        public ICollection<User> Users { get; set; }

    }
}
