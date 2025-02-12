using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sinitsyna;
using Sinitsyna.Models;
using System.Threading.Tasks;

namespace Sinitsyna.Components
{
    public class AverageRatingViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;

        public AverageRatingViewComponent(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(int productId)
        {
            // Вычисляем средний рейтинг товара
            decimal averageRating = 0;
            if (await _context.Reviews.AnyAsync(r => r.Id_product == productId))
            {
                averageRating = (decimal)await _context.Reviews
                    .Where(r => r.Id_product == productId)
                    .AverageAsync(r => r.Rating);
            }

            // Передаем средний рейтинг в представление
            return View("Default", averageRating);
        }
    }
}
