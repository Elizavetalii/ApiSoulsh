using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class User
    {
        [Key]
        [JsonPropertyName("idUser")]
        public int Id_user { get; set; }

        [Required]
        [JsonPropertyName("firstName")]
        public string First_name { get; set; }

        [Required]
        [JsonPropertyName("lastName")]
        public string Last_name { get; set; }

        [JsonPropertyName("middleName")]
        public string Middle_name { get; set; }

        [Required]
        [EmailAddress]
        [JsonPropertyName("userLogin")]
        public string User_login { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [JsonPropertyName("userPassword")]
        public string User_password { get; set; }

        [Required]
        [JsonPropertyName("idRole")]
        public int Id_role { get; set; } = 1;

        [ForeignKey("Id_role")]
        public Role Role { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
    }
}
