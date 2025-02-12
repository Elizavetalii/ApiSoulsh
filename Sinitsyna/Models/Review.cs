using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sinitsyna.Models
{
    public class Review
    {
        [Key]
        public int Id_review { get; set; }
        public int Id_user { get; set; }
        public int Id_product { get; set; }
        public int Rating { get; set; }
        public string Text_reviews { get; set; }
        public DateTime Created_date { get; set; }

        // Navigation properties
        [ForeignKey("Id_user")]
        public User User { get; set; }

        [ForeignKey("Id_product")]
        public Product Product { get; set; }
    }
}
