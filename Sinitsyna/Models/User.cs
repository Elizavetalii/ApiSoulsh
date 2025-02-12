using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sinitsyna.Models
{

    public class User
    {
        [Key]
        public int Id_user { get; set; }
        [Required]
        public string First_name { get; set; }

        [Required]
        public string Last_name { get; set; }

        public string Middle_name { get; set; }

        [Required]
        [EmailAddress] // Проверка на корректный email
        public string User_login { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string User_password { get; set; }
        [Required]
        public int Id_role { get; set; } = 1;

        [ForeignKey("Id_role")]
        public Role Role { get; set; } 

        public virtual ICollection<Order> Orders { get; set; }
    }
    
}