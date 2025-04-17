using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Sinitsyna.Models;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using OfficeOpenXml; // Убедитесь, что у вас установлена библиотека EPPlus
using iTextSharp.text; // Убедитесь, что у вас установлена библиотека iTextSharp
using iTextSharp.text.pdf;
using System.Text;
using Newtonsoft.Json;
using System.Drawing;
using System.IO;
using Microsoft.AspNetCore.Authorization; // Import for [Authorize]
using System.Security.Claims; // Import for User.Identity.GetUserId()

namespace Sinitsyna.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

       
        [Authorize] // Требуется аутентификация пользователя
        [HttpPost]
        public async Task<IActionResult> AddFavorite(int productId)
        {
            // Получаем ID пользователя из Claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Не удалось получить ID пользователя.");
            }

            // Проверяем, не добавлен ли уже этот товар в избранное
            if (await _context.Favorites.AnyAsync(f => f.Id_user == userId && f.Id_product == productId))
            {
                return Ok(new { success = false, message = "Товар уже в избранном." });
            }

            // Добавляем товар в избранное
            var favorite = new Favorite
            {
                Id_user = userId,
                Id_product = productId
            };
            _context.Favorites.Add(favorite);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Товар добавлен в избранное." });
        }

        [Authorize] // Требуется аутентификация пользователя
        [HttpPost]
        public async Task<IActionResult> RemoveFavorite(int productId)
        {
            // Получаем ID пользователя из Claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Не удалось получить ID пользователя.");
            }

            // Удаляем товар из избранного
            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.Id_user == userId && f.Id_product == productId);

            if (favorite == null)
            {
                return Ok(new { success = false, message = "Товар не найден в избранном." });
            }

            _context.Favorites.Remove(favorite);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Товар удален из избранного." });
        }

        [Authorize] // Требуется аутентификация пользователя
        public async Task<IActionResult> Favorites()
        {
            // Получаем ID пользователя из Claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Не удалось получить ID пользователя.");
            }

            // Получаем список избранных товаров для пользователя
            var favorites = await _context.Favorites
                .Where(f => f.Id_user == userId)
                .Select(f => f.Id_product)
                .ToListAsync();

            // Получаем информацию о товарах из базы данных
            var favoriteProducts = await _context.Products
                .Where(p => favorites.Contains(p.Id_product))
                .Select(p => new FavoriteViewModel
                {
                    Id_product = p.Id_product,
                    Product_name = p.Product_name,
                    Price = p.Price,
                    Url_image = p.ProductImages.FirstOrDefault().Url_image // Получаем URL первого изображения
                })
                .ToListAsync();

            return View(favoriteProducts);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.Id_product == id);

            if (product == null)
            {
                return NotFound();
            }

            int userId = 0;
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out userId)){}else{}
            }

            bool isFavorite = false;
            if (userId > 0)
            {
                isFavorite = await _context.Favorites.AnyAsync(f => f.Id_user == userId && f.Id_product == id);
            }

            ShoppingCart cart = new ShoppingCart();

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));
            }
            else
            {
                cart = new ShoppingCart();
            }
            // Определяем, находится ли товар в корзине
            ViewBag.IsInCart = cart.CartLines.Any(cl => cl.ProductId == id);

            // Если товар в корзине, передаем количество
            ViewBag.QuantityInCart = cart.CartLines.FirstOrDefault(cl => cl.ProductId == id)?.Quantity;

            var reviews = await _context.Reviews
                .Where(r => r.Id_product == id)
                .Include(r => r.User) // Include User info for displaying the username
                .ToListAsync();

            var reviewViewModels = reviews.Select(r => new ReviewViewModel
            {
                UserName = r.User.First_name,
                Rating = r.Rating,
                Text = r.Text_reviews,
                CreatedDate = r.Created_date
            }).ToList();

            // Calculate Average Rating
            decimal averageRating = 0;
            if (reviewViewModels.Any())
            {
                averageRating = (decimal)reviewViewModels.Average(x => x.Rating);
            }

            var detailsViewModel = new DetailsViewModel
            {
                Product = product,
                Reviews = reviewViewModels,
                AddReview = new AddReviewViewModel { ProductId = id },
                AverageRating = averageRating,
                ReviewsCount = reviews.Count(),// Pass the average rating to the view model
                IsFavorite = isFavorite // передаем состояние избранного в ViewModel
            };

            return View(detailsViewModel);
        }

        private List<int> GetFavoritesFromSession()
        {
            var favoritesString = HttpContext.Session.GetString("favorites");
            if (string.IsNullOrEmpty(favoritesString))
            {
                return new List<int>();
            }
            else
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<int>>(favoritesString);
            }
        }
    
        [HttpPost]
        public IActionResult AddReview(AddReviewViewModel model)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("SignIn", "Home");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Не удалось получить ID пользователя.");
            }

            if (ModelState.IsValid)
            {
                var review = new Review
                {
                    Id_user = userId,
                    Id_product = model.ProductId,
                    Rating = model.Rating,
                    Text_reviews = model.Text,
                    Created_date = DateTime.Now
                };

                _context.Reviews.Add(review);
                _context.SaveChanges();

                return RedirectToAction("Details", new { id = model.ProductId });
            }

            // Если модель недействительна, возвращаемся к представлению Details
            return RedirectToAction("Details", new { id = model.ProductId });
        }

        [HttpGet]
        public async Task<IActionResult> AddToCart(int Id, int Quantity)
        {
            ShoppingCart cart;

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));
            }
            else
            {
                cart = new ShoppingCart();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id_product == Id);

            if (product == null)
            {
                return NotFound("Товар не найден.");
            }

            if (Quantity <= product.Quantity) // Проверка доступного количества
            {
                var existingCartLine = cart.CartLines.FirstOrDefault(cl => cl.ProductId == Id);
                if (existingCartLine != null)
                {
                    existingCartLine.Quantity += Quantity; // Увеличиваем количество
                }
                else
                {
                    // Получаем URL первого изображения
                    string imageUrl = null;
                    var productImage = await _context.ProductImages
                        .FirstOrDefaultAsync(pi => pi.Id_product == Id);

                    if (productImage != null)
                    {
                        imageUrl = productImage.Url_image;
                    }
                    else
                    {
                        imageUrl = "/media/default_image.png"; // URL изображения по умолчанию
                    }

                    cart.CartLines.Add(new CartLine
                    {
                        ProductId = product.Id_product,
                        ProductName = product.Product_name,
                        Price = product.Price,
                        ImageUrl = imageUrl,
                        Quantity = Quantity
                    });
                }

                HttpContext.Session.SetString("ShoppingCart", System.Text.Json.JsonSerializer.Serialize(cart));
                return Ok(cart); // Возвращаем обновленную корзину
            }
            else
            {
                return BadRequest("Недостаточно товара на складе.");
            }
        }

        [HttpPost]
        public IActionResult UpdateCart(int Id, int Quantity)
        {
            ShoppingCart cart;

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));

                var existingCartLine = cart.CartLines.FirstOrDefault(cl => cl.ProductId == Id);
                if (existingCartLine != null)
                {
                    existingCartLine.Quantity = Quantity; // Обновляем количество
                }

                HttpContext.Session.SetString("ShoppingCart", System.Text.Json.JsonSerializer.Serialize(cart));
                return Ok(cart); // Возвращаем обновленную корзину
            }

            return BadRequest("Корзина пуста.");
        }
    

        [HttpPost]
        public IActionResult UpdateFavorites(int productId, bool add)
        {
            // Получаем список "избранного" из сессии
            List<int> favorites = GetFavoritesFromSession();

            if (add)
            {
                if (!favorites.Contains(productId))
                {
                    favorites.Add(productId);
                }
            }
            else
            {
                favorites.Remove(productId);
            }

            // Сохраняем обновленный список в сессию
            SaveFavoritesToSession(favorites);

            return Ok();
        }
   
        private void SaveFavoritesToSession(List<int> favorites)
        {
            string favoritesJson = System.Text.Json.JsonSerializer.Serialize(favorites);
            HttpContext.Session.SetString("Favorites", favoritesJson);
        }

        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("SignIn", "Home");
            }

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.User_login == User.Identity.Name);

            if (user != null && user.Id_role == 3)
            {
                ViewBag.UserName = user.First_name + " " + user.Last_name;
                ViewBag.UserRole = user.Role?.Role_name;

                var boutiques = await _context.Boutiques.ToListAsync();
                var products = await _context.Products.Include(p => p.ProductType)
                                                       .Include(p => p.ProductMaterial)
                                                       .Include(p => p.ProductImages)
                                                       .ToListAsync();
                var materials = await _context.ProductMaterials.ToListAsync();
                var types = await _context.ProductTypes.ToListAsync();


                var orders = await _context.Orders.Include(o => o.OrderItems).ToListAsync();


                var totalSales = orders.Sum(o => o.TotalPrice);
                var salesOverTime = new List<decimal>(new decimal[12]);
                var salesDistribution = orders.SelectMany(o => o.OrderItems)
                                    .GroupBy(oi => new { oi.Product.Id_product, oi.Product.Product_name }) // Группируем по ProductId и ProductName
                                    .Select(g => new SalesDistribution
                                    {
                                        ProductId = g.Key.Id_product,
                                        ProductName = g.Key.Product_name, // Получаем название товара
                                        TotalQuantity = g.Sum(oi => oi.Quantity)
                                    }).ToList();

                var now = DateTime.UtcNow;
                foreach (var order in orders)
                {
                    var orderDate = order.OrderDate.Date; // Предполагается, что у вас есть OrderDate в модели Order
                    if (orderDate >= now.AddMonths(-12))
                    {
                        int monthIndex = (orderDate.Month - now.Month + 12) % 12;
                        salesOverTime[monthIndex] += order.TotalPrice;
                    }
                }

                // Создание модели
                var model = new ManagerViewModel
                {
                    Products = products,
                    Boutiques = boutiques,
                    Materials = materials,
                    Types = types,
                    TotalSales = totalSales,
                    SalesDistributions = salesDistribution,
                    SalesOverTime = salesOverTime // Заполнение данных о продажах за последние 12 месяцев
                };

                return View("ManagerDashboard", model);
            
            }

            if (user != null && user.Id_role == 2)
            {
                ViewBag.UserName = user.First_name + " " + user.Last_name;
                ViewBag.UserRole = user.Role?.Role_name;
                var boutiques = await _context.Boutiques.ToListAsync();
                var products = await _context.Products.Include(p => p.ProductType)
                                                      .Include(p => p.ProductMaterial)
                                                      .Include(p => p.ProductImages)
                                                      .ToListAsync();
                var materials = await _context.ProductMaterials.ToListAsync();
                var types = await _context.ProductTypes.ToListAsync();
                var users = await _context.Users.Include(u => u.Role).ToListAsync();
                var role = await _context.Roles.ToListAsync();

                var model = (products.AsEnumerable(), boutiques.AsEnumerable(), materials.AsEnumerable(), types.AsEnumerable(), users.AsEnumerable(), role.AsEnumerable());
                return View("AdminDashboard", model);
            }

            if (user != null && user.Id_role == 1)
            {
                ViewBag.UserName = user.First_name + " " + user.Last_name;
                ViewBag.UserRole = user.Role?.Role_name;
                var favoriteProductIds = await _context.Favorites
               .Where(f => f.Id_user == user.Id_user)
               .Select(f => f.Id_product)
               .ToListAsync();
                ViewBag.FavoriteProductIds = favoriteProductIds;
                HttpContext.Session.Remove("ShoppingCart");
                return View("Index");
            }
            return View(await _context.Products.ToListAsync());
        }



        [HttpPost]
        public async Task<IActionResult> ExportSalesData(string format)
        {
            var salesAnalytics = await GetSalesAnalytics();

            // Получение изображений графиков из формы
            var totalSalesChartImage = Request.Form["totalSalesChartImage"];
            var salesDistributionChartImage = Request.Form["salesDistributionChartImage"];

            if (string.IsNullOrEmpty(totalSalesChartImage) || string.IsNullOrEmpty(salesDistributionChartImage))
            {
                return BadRequest("Не удалось получить изображения графиков.");
            }

            if (format == "pdf")
            {
                return GeneratePdf(salesAnalytics, totalSalesChartImage, salesDistributionChartImage);
            }
            else if (format == "excel")
            {
                return GenerateExcel(salesAnalytics, totalSalesChartImage, salesDistributionChartImage);
            }
            else if (format == "csv")
            {
                return GenerateCsv(salesAnalytics);
            }
            else if (format == "json")
            {
                return GenerateJson(salesAnalytics);
            }

            return BadRequest("Неверный формат");
        }


        private async Task<IEnumerable<SalesAnalytics>> GetSalesAnalytics()
        {
            var orders = await _context.Orders.Include(o => o.OrderItems)
                                               .ThenInclude(oi => oi.Product)
                                               .ToListAsync();

            var salesData = from order in orders
                            from item in order.OrderItems
                            select new SalesAnalytics
                            {
                                OrderId = order.Id,
                                OrderDate = order.OrderDate,
                                TotalPrice = order.TotalPrice,
                                ProductName = item.Product.Product_name,
                                Quantity = item.Quantity,
                                Price = item.Product.Price
                            };

            return salesData.ToList();
        }

        private ActionResult GeneratePdf(IEnumerable<SalesAnalytics> salesData, string totalSalesChartImage, string salesDistributionChartImage)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document();
                PdfWriter.GetInstance(document, stream);
                document.Open();

                document.Add(new Paragraph("Аналитика по продажам"));

                // Добавление изображений графиков
                if (!string.IsNullOrEmpty(totalSalesChartImage))
                {
                    var imgTotalSales = iTextSharp.text.Image.GetInstance(totalSalesChartImage);
                    document.Add(imgTotalSales);
                }

                if (!string.IsNullOrEmpty(salesDistributionChartImage))
                {
                    var imgSalesDistribution = iTextSharp.text.Image.GetInstance(salesDistributionChartImage);
                    document.Add(imgSalesDistribution);
                }

                foreach (var sale in salesData)
                {
                    document.Add(new Paragraph($"Заказ ID: {sale.OrderId}, Дата: {sale.OrderDate}, Товар: {sale.ProductName}, Количество: {sale.Quantity}, Цена: {sale.Price:C}, Общая сумма: {sale.TotalPrice:C}"));
                }

                document.Close();
                return File(stream.ToArray(), "application/pdf", "sales_report.pdf");
            }
        }

        private ActionResult GenerateExcel(IEnumerable<SalesAnalytics> salesData, string totalSalesChartImage, string salesDistributionChartImage)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sales Data");

                // Заголовки столбцов
                worksheet.Cells[1, 1].Value = "ID заказа";
                worksheet.Cells[1, 2].Value = "Дата заказа";
                worksheet.Cells[1, 3].Value = "Общая сумма";
                worksheet.Cells[1, 4].Value = "Название товара";
                worksheet.Cells[1, 5].Value = "Количество";
                worksheet.Cells[1, 6].Value = "Цена";

                // Заполнение данными
                int row = 2;
                foreach (var sale in salesData)
                {
                    worksheet.Cells[row, 1].Value = sale.OrderId;
                    worksheet.Cells[row, 2].Value = sale.OrderDate;
                    worksheet.Cells[row, 3].Value = sale.TotalPrice;
                    worksheet.Cells[row, 4].Value = sale.ProductName;
                    worksheet.Cells[row, 5].Value = sale.Quantity;
                    worksheet.Cells[row, 6].Value = sale.Price;
                    row++;
                }

                return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sales_report.xlsx");
            }
        }

        private ActionResult GenerateCsv(IEnumerable<SalesAnalytics> salesData)
        {
            var csvBuilder = new StringBuilder();

            // Заголовки столбцов
            csvBuilder.AppendLine("ID заказа,Дата заказа,Общая сумма,Название товара,Количество,Цена");

            // Заполнение данными
            foreach (var sale in salesData)
            {
                csvBuilder.AppendLine($"{sale.OrderId},{sale.OrderDate},{sale.TotalPrice},{sale.ProductName},{sale.Quantity},{sale.Price}");
            }

            return File(Encoding.UTF8.GetBytes(csvBuilder.ToString()), "text/csv", "sales_report.csv");
        }

        private ActionResult GenerateJson(IEnumerable<SalesAnalytics> salesData)
        {
            var jsonResult = JsonConvert.SerializeObject(salesData);
            return File(Encoding.UTF8.GetBytes(jsonResult), "application/json", "sales_report.json");
        }
    


        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ToListAsync();

            return View(orders);
        }

        public IActionResult ShoppingCart()
        {
            ShoppingCart cart = new ShoppingCart();

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));

            return View(cart);
        }
        
        public IActionResult Checkout()
        {
            ShoppingCart cart;

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));

                decimal totalPrice = 0;
                var boutiqueDetails = new List<Boutique>();

                // Проверяем, аутентифицирован ли пользователь
                if (!User.Identity.IsAuthenticated)
                {
                    return RedirectToAction("Login", "Account"); // Перенаправление на страницу входа
                }

                // Получаем UserId из утверждений
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier); // Обычно используется ClaimTypes.NameIdentifier для ID пользователя
                int userId = userIdClaim != null ? Convert.ToInt32(userIdClaim.Value) : 0; // Преобразуем значение в int

                // Создаем новый заказ
                var order = new Order
                {
                    UserId = userId, // Устанавливаем UserId
                    OrderDate = DateTime.Now,
                    TotalPrice = 0,
                    OrderItems = new List<OrderItem>()
                };

                foreach (var cartLine in cart.CartLines)
                {
                    var product = _context.Products.Include(p => p.Boutique).FirstOrDefault(p => p.Id_product == cartLine.ProductId);
                    if (product != null)
                    {
                        totalPrice += product.Price * cartLine.Quantity;

                        product.Quantity -= cartLine.Quantity;

                        // Добавляем элемент заказа
                        order.OrderItems.Add(new OrderItem
                        {
                            ProductId = product.Id_product,
                            Quantity = cartLine.Quantity
                        });

                        boutiqueDetails.Add(new Boutique
                        {
                            Boutique_address = product.Boutique?.Boutique_address ?? "Адрес не указан",
                            Opening_time = product.Boutique?.Opening_time ?? TimeSpan.Zero,
                            Closing_time = product.Boutique?.Closing_time ?? TimeSpan.Zero
                        });
                    }
                }

                order.TotalPrice = totalPrice;

                // Сохраняем изменения в базе данных
                _context.Orders.Add(order);
                _context.SaveChanges();

                ViewBag.TotalPrice = totalPrice;
                ViewBag.BoutiqueDetails = boutiqueDetails;

                // Очищаем корзину после оформления заказа
                HttpContext.Session.Remove("ShoppingCart");

                return View(cart); // Передаем корзину в представление
            }

            return BadRequest("Корзина пуста.");
        }

        public IActionResult RemoveFromCart()
        {
            int number = Convert.ToInt32(Request.Query["number"]);
            ShoppingCart cart;

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));

                // Получаем товар из корзины
                var cartLine = cart.CartLines[number];

                // Удаляем товар из корзины
                cart.CartLines.RemoveAt(number);

                // Обновляем корзину в сессии
                HttpContext.Session.SetString("ShoppingCart", System.Text.Json.JsonSerializer.Serialize(cart));

                // Если необходимо, можно освободить резервированное количество
                // InventoryManager.ReleaseProduct(cartLine.Product.Id_product, cartLine.Quantity);

                return Redirect("ShoppingCart"); // Перенаправляем на страницу корзины
            }

            return BadRequest("Корзина пуста.");
        }

        public IActionResult RemoveAllFromCart()
        {
            ShoppingCart cart = new ShoppingCart();

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));

                // Освобождаем все товары из корзины (если используется InventoryManager или аналогичный класс)
                foreach (var cartLine in cart.CartLines)
                {
                    // Освобождаем резервированное количество
                    InventoryManager.ReleaseProduct(cartLine.ProductId, cartLine.Quantity);
                }

                // Очищаем все товары из корзины
                cart.CartLines.Clear();

                // Обновляем корзину в сессии
                HttpContext.Session.SetString("ShoppingCart", System.Text.Json.JsonSerializer.Serialize(cart));
            }

            // Очищаем корзину из сессии
            HttpContext.Session.Remove("ShoppingCart");

            return Redirect("ShoppingCart"); // Перенаправляем на страницу корзины
        }

        // Метод для отображения бутиков
        public async Task<IActionResult> Boutiques()
        {
            var boutiques = await _context.Boutiques.ToListAsync();
            return View(boutiques);
        }

        // Метод для создания нового бутика
        [HttpPost]
        public async Task<IActionResult> CreateBoutique(string Boutique_address, TimeSpan Opening_time, TimeSpan Closing_time)
        {
            var newBoutique = new Boutique
            {
                Boutique_address = Boutique_address,
                Opening_time = Opening_time,
                Closing_time = Closing_time
            };

            _context.Boutiques.Add(newBoutique);

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Метод для редактирования бутика
        [HttpPost]
        public async Task<IActionResult> EditBoutique(int id, string boutiqueAddress, TimeSpan openingTime, TimeSpan closingTime)
        {
            var existingBoutique = await _context.Boutiques.FindAsync(id);

            if (existingBoutique != null)
            {
                existingBoutique.Boutique_address = boutiqueAddress;
                existingBoutique.Opening_time = openingTime;
                existingBoutique.Closing_time = closingTime;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для удаления бутика
        [HttpPost]
        public async Task<IActionResult> DeleteBoutique(int id)
        {
            var existingBoutique = await _context.Boutiques.FindAsync(id);

            if (existingBoutique != null)
            {
                _context.Boutiques.Remove(existingBoutique);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для создания нового типа продукта
        [HttpPost]
        public async Task<IActionResult> CreateProductType(string Product_type_name)
        {
            var newType = new ProductType { Product_type_name = Product_type_name };

            _context.ProductTypes.Add(newType);

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Метод для редактирования типа продукта
        [HttpPost]
        public async Task<IActionResult> EditProductType(int id, string productTypeName)
        {
            var existingType = await _context.ProductTypes.FindAsync(id);

            if (existingType != null)
            {
                existingType.Product_type_name = productTypeName;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для удаления типа продукта
        [HttpPost]
        public async Task<IActionResult> DeleteProductType(int id)
        {
            var existingType = await _context.ProductTypes.FindAsync(id);

            if (existingType != null)
            {
                _context.ProductTypes.Remove(existingType);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для создания нового материала
        [HttpPost]
        public async Task<IActionResult> CreateProductMaterial(string Material_name)
        {
            var newMaterial = new ProductMaterial { Material_name = Material_name };

            _context.ProductMaterials.Add(newMaterial);

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Метод для редактирования материала
        [HttpPost]
        public async Task<IActionResult> EditProductMaterial(int id, string materialName)
        {
            var existingMaterial = await _context.ProductMaterials.FindAsync(id);

            if (existingMaterial != null)
            {
                existingMaterial.Material_name = materialName;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для удаления материала
        [HttpPost]
        public async Task<IActionResult> DeleteProductMaterial(int id)
        {
            var existingMaterial = await _context.ProductMaterials.FindAsync(id);

            if (existingMaterial != null)
            {
                _context.ProductMaterials.Remove(existingMaterial);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для отображения ролей
        public async Task<IActionResult> Roles()
        {
            var roles = await _context.Roles.ToListAsync(); // Предполагается, что у вас есть DbSet<Role> в контексте
            return View(roles);
        }
        [HttpPost]
        public async Task<IActionResult> CreateRole(string Role_name)
        {
            var newRole = new Role { Role_name = Role_name }; // Не устанавливайте Id_role
            _context.Roles.Add(newRole);

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (DbUpdateException ex)
            {
                // Логирование ошибки
                Console.WriteLine(ex.InnerException?.Message);
                return Json(new { success = false, message = "Ошибка при сохранении роли." });
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Console.WriteLine(ex.Message);
                return Json(new { success = false, message = "Произошла ошибка." });
            }
        }
        // Метод для редактирования роли
        [HttpPost]
        public async Task<IActionResult> EditRole(int id, string roleName)
        {
            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole != null)
            {
                existingRole.Role_name = roleName; // Обновляем название роли
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для удаления роли
        [HttpPost]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role != null)
            {
                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        // Метод для отображения пользователей
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users.Include(u => u.Role).ToListAsync();
            return View(users);
        }

        // Метод для добавления нового пользователя
        [HttpPost]
        public async Task<IActionResult> CreateUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // Метод для редактирования пользователя
        [HttpPost]
        public async Task<IActionResult> EditUser(int id, User user)
        {

            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser != null)
            {
                existingUser.First_name = user.First_name;
                existingUser.Last_name = user.Last_name;
                existingUser.Middle_name = user.Middle_name;
                existingUser.Id_role = user.Id_role;
                existingUser.User_login = user.User_login;
                existingUser.User_password = user.User_password; // Не забудьте хэшировать пароль

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            
            return Json(new { success = false });
        }

        // Метод для удаления пользователя
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public IActionResult CreateProduct(Product model, string imageUrl)
        {
            // Сохранение товара
            _context.Products.Add(model);
            _context.SaveChanges();

            // Сохранение изображения
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var productImage = new ProductImage
                {
                    Url_image = imageUrl,
                    Id_product = model.Id_product // Предполагается, что Id_product заполняется после сохранения
                };
                _context.ProductImages.Add(productImage);
                _context.SaveChanges();
            }

            return Json(new { success = true });

        }
        [HttpPost]
        public async Task<IActionResult> EditProduct(Product product, List<string> ImageUrls)
        {
            _context.Products.Update(product);

            // Удаляем старые изображения (если необходимо)
            var existingImages = await _context.ProductImages.Where(pi => pi.Id_product == product.Id_product).ToListAsync();

            foreach (var image in existingImages)
            {
                _context.ProductImages.Remove(image);
            }

            // Сохранение новых изображений
            foreach (var url in ImageUrls)
            {
                var productImage = new ProductImage
                {
                    Url_image = url,
                    Id_product = product.Id_product // Связываем изображение с товаром
                };
                _context.ProductImages.Add(productImage);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });           
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Удаляем связанные изображения
                var images = await _context.ProductImages.Where(pi => pi.Id_product == id).ToListAsync();
                _context.ProductImages.RemoveRange(images);

                // Удаляем продукт
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

  
        private bool IsUserAuthorized()
        {
            var userRole = User.FindFirst("Role")?.Value; // Получаем роль пользователя из claims
            return userRole == "Администратор" || userRole == "Менеджер"; // Проверка на администраторов и менеджеров
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id_product == id);
        }

        // Проверка существования пользователя
        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id_user == id);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult SignIn()
        {
            if (HttpContext.Session.Keys.Contains("AuthUser"))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignIn(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                User user = await _context.Users
                    .Include(u => u.Role) // Подключаем роль
                    .FirstOrDefaultAsync(u => u.User_login == model.Login && u.User_password == model.Password);

                if (user != null)
                {
                    await Authenticate(user); // Передаем объект user в Authenticate
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Некорректные логин или пароль");
            }

            return RedirectToAction("SignIn", "Home");
        }


        private async Task Authenticate(User user)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.User_login),
        new Claim(ClaimTypes.NameIdentifier, user.Id_user.ToString()), // Добавляем ID пользователя
        new Claim(ClaimTypes.Role, user.Role.Role_name) // Добавляем роль
    };

            ClaimsIdentity id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); // Выход из аутентификации
                                                                                               //HttpContext.Session.Remove("AuthUser"); // Больше не нужно

            return RedirectToAction("SignIn"); // Перенаправляем на страницу входа
        }


        public IActionResult SignUp()
        {
            return View();
        }
        
        private bool IsUserValid(User person)
        {
            // Создаем новый список для ошибок
            var validationResults = new List<ValidationResult>();

            // Создаем валидатор для нашей модели
            var context = new ValidationContext(person);

            // Проверяем валидацию модели (все свойства кроме Id_role)
            var isValid = Validator.TryValidateObject(person, context, validationResults, true);

            // Удаляем ошибки, связанные с Id_role
            validationResults.RemoveAll(vr => vr.MemberNames.Contains("Id_role"));

            // Если все ошибки пусты, возвращаем true
            return validationResults.Count == 0;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(User person)
        {
            if (IsUserValid(person))
            {
                // Проверка на существование пользователя
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.User_login == person.User_login);
                if (existingUser != null)
                {
                    ModelState.AddModelError("User_login", "Пользователь с таким логином уже существует.");
                    return View(person);
                }

                // Добавление пользователя в базу данных
                _context.Users.Add(person);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Вы успешно зарегистрированы!";
                return RedirectToAction("SignUp");
            }
            else
            {
                var errors = new List<string>();
                var validationResults = new List<ValidationResult>();
                var context = new ValidationContext(person);
                Validator.TryValidateObject(person, context, validationResults, true);

                foreach (var error in validationResults)
                {
                    errors.AddRange(error.MemberNames.Select(m => $"{m}: {error.ErrorMessage}"));
                    Console.WriteLine(string.Join(", ", errors));
                }
                return View(person);
            }
        }

        [HttpPost]
        public IActionResult ToggleTheme()
        {
            // Получаем текущую тему из куки
            var currentTheme = Request.Cookies["Theme"];

            // Меняем тему
            var newTheme = currentTheme == "dark" ? "light" : "dark";

            // Устанавливаем новую тему в куки
            Response.Cookies.Append("Theme", newTheme, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

            return Json(new { success = true, theme = newTheme });
        }

        public async Task<IActionResult> Catalog()
        {
            var products = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.ProductMaterial)
                .Include(p => p.ProductImages)
                .ToListAsync();

            var materials = await _context.ProductMaterials.ToListAsync();
            var types = await _context.ProductTypes.ToListAsync();

            int userId = 0;
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out userId))
                {
                    // Пользователь аутентифицирован, ID пользователя получен
                }
                else
                {
                    // Обработка ошибки: не удалось получить ID пользователя
                    // ...
                }
            }

            // Получаем список ID избранных товаров для пользователя
            List<int> favoriteProductIds = new List<int>();
            if (userId > 0)
            {
                favoriteProductIds = await _context.Favorites
                    .Where(f => f.Id_user == userId)
                    .Select(f => f.Id_product)
                    .ToListAsync();
            }

            // Получаем корзину из сессии
            ShoppingCart cart = new ShoppingCart();
            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));
            }

            // Создаем модель для передачи в представление
            var model = new CatalogViewModel
            {
                Products = products,
                Materials = materials,
                Types = types,
                ShoppingCart = cart,
                ReviewCounts = new Dictionary<int, int>(), // Initialize ReviewCounts
                AverageRatings = new Dictionary<int, decimal>() // Initialize AverageRatings
            };
            // Заполняем данные о количестве отзывов и среднем рейтинге
            foreach (var product in model.Products)
            {
                // Количество отзывов
                model.ReviewCounts[product.Id_product] = await _context.Reviews
                    .CountAsync(r => r.Id_product == product.Id_product);

                // Средний рейтинг
                if (model.ReviewCounts[product.Id_product] > 0)
                {
                    model.AverageRatings[product.Id_product] = await _context.Reviews
                        .Where(r => r.Id_product == product.Id_product)
                        .AverageAsync(r => (decimal)r.Rating);
                }
            }
            // Передаем список ID избранных товаров в представление через ViewBag
            ViewBag.FavoriteProductIds = favoriteProductIds;

            return View("Catalog", model);
        }
    }
}