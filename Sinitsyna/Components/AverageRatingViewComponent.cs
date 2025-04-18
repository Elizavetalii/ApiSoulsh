using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Sinitsyna.Components
{
    public class AverageRatingViewComponent : ViewComponent
    {

        private readonly HttpClient _httpClient;

        public AverageRatingViewComponent()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7147/api/") // адрес вашего API
            };
        }

    
        public async Task<IViewComponentResult> InvokeAsync(int productId)
        {
            decimal averageRating = 0;

            try
            {
                // Предполагается, что API возвращает средний рейтинг по адресу: api/reviews/average/{productId}
                var response = await _httpClient.GetAsync($"reviews/average/{productId}");
                if (response.IsSuccessStatusCode)
                {
                    averageRating = await response.Content.ReadFromJsonAsync<decimal>();
                }
            }
            catch (Exception ex)
            {
                // Логируйте ошибку или обработайте её по необходимости
                // Например, averageRating останется 0
            }

            return View("Default", averageRating);
        }
    }
}
