using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Sinitsyna.Models
{
    public class Product
    {
        [Key]
        [JsonPropertyName("idProduct")]
        public int Id_product { get; set; }

        [Required(ErrorMessage = "Название товара обязательно.")]
        [JsonPropertyName("productName")]
        public string Product_name { get; set; }

        [Required(ErrorMessage = "Цена обязательна.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Цена должна быть больше нуля.")]
        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Количество обязательно.")]
        [Range(1, int.MaxValue, ErrorMessage = "Количество должно быть больше нуля.")]
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Описание товара обязательно.")]
        [JsonPropertyName("productDescription")]
        public string Product_description { get; set; }

        [JsonPropertyName("idBoutique")]
        [ValidateNever]
        public int? Id_boutique { get; set; }

        [ForeignKey("Id_boutique")]
        [JsonPropertyName("idBoutiqueNavigation")]
        public Boutique Boutique { get; set; }

        [JsonPropertyName("idType")]
        [ValidateNever]
        public int? Id_type { get; set; }

        [ForeignKey("Id_type")]
        [JsonPropertyName("idTypeNavigation")]
        public ProductType ProductType { get; set; }

        [JsonPropertyName("idMaterial")]
        [ValidateNever]
        public int? Id_material { get; set; }

        [ForeignKey("Id_material")]
        [JsonPropertyName("idMaterialNavigation")]
        public ProductMaterial ProductMaterial { get; set; }

        [JsonPropertyName("productImages")]
        public ICollection<ProductImage> ProductImages { get; set; }
    }
}
