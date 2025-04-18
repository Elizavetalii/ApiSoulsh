using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class Review
    {
        [Key]
        [JsonPropertyName("idReview")]
        public int Id_review { get; set; }

        [JsonPropertyName("idUser")]
        public int Id_user { get; set; }

        [JsonPropertyName("idProduct")]
        public int Id_product { get; set; }

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("textReviews")]
        public string Text_reviews { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime Created_date { get; set; }

        // Навигационные свойства
        [ForeignKey("Id_user")]
        [JsonPropertyName("idUserNavigation")]
        [ValidateNever]
        public User User { get; set; }

        [ForeignKey("Id_product")]
        [JsonPropertyName("idProductNavigation")]
        [ValidateNever]
        public Product Product { get; set; }
    }
}
