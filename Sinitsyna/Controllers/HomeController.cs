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
        private readonly HttpClient _httpClient;

        public HomeController()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7147/api/") // адрес вашего API
            };
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddFavorite(int productId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return BadRequest("Не удалось получить ID пользователя.");

            var favorite = new { Id_user = userId, Id_product = productId };
            var response = await _httpClient.PostAsJsonAsync("favorites/add", favorite);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                return Ok(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return BadRequest(error);
            }
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RemoveFavorite(int productId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return BadRequest("Не удалось получить ID пользователя.");

            var requestData = new
            {
                UserId = userId,
                ProductId = productId
            };

            var response = await _httpClient.PostAsJsonAsync("favorites/remove", requestData);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                return Ok(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return BadRequest(error);
            }
        }

        [Authorize]
        public async Task<IActionResult> Favorites()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return BadRequest("Не удалось получить ID пользователя.");

            var response = await _httpClient.GetAsync($"favorites/{userId}");

            if (response.IsSuccessStatusCode)
            {
                var favoriteProducts = await response.Content.ReadFromJsonAsync<List<FavoriteViewModel>>();
                return View(favoriteProducts);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return BadRequest(error);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            // Получаем продукт с изображениями
            var productResponse = await _httpClient.GetAsync($"products/{id}");
            if (!productResponse.IsSuccessStatusCode)
                return NotFound();

            var product = await productResponse.Content.ReadFromJsonAsync<Product>();

            int userId = 0;
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
                    userId = parsedUserId;
            }

            bool isFavorite = false;
            if (userId > 0)
            {
                // Проверяем, есть ли товар в избранном пользователя
                var favResponse = await _httpClient.GetAsync($"favorites/user/{userId}");
                if (favResponse.IsSuccessStatusCode)
                {
                    var favorites = await favResponse.Content.ReadFromJsonAsync<List<Favorite>>();
                    isFavorite = favorites.Any(f => f.Id_product == id);
                }
            }

            // Работа с корзиной из сессии (оставляем без изменений)
            ShoppingCart cart = new ShoppingCart();
            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));
            }
            ViewBag.IsInCart = cart.CartLines.Any(cl => cl.ProductId == id);
            ViewBag.QuantityInCart = cart.CartLines.FirstOrDefault(cl => cl.ProductId == id)?.Quantity ?? 0;

            // Получаем отзывы по продукту из API
            var reviewsResponse = await _httpClient.GetAsync($"reviews/product/{id}");
            List<Review> reviews = new List<Review>();
            if (reviewsResponse.IsSuccessStatusCode)
            {
                reviews = await reviewsResponse.Content.ReadFromJsonAsync<List<Review>>();
            }

            // Преобразуем отзывы в ViewModel
            var reviewViewModels = reviews.Select(r => new ReviewViewModel
            {
                UserName = r.User.First_name, // предполагается, что User загружен в API
                Rating = r.Rating,
                Text = r.Text_reviews,
                CreatedDate = r.Created_date
            }).ToList();

            decimal averageRating = reviewViewModels.Any() ? (decimal)reviewViewModels.Average(x => x.Rating) : 0;

            var detailsViewModel = new DetailsViewModel
            {
                Product = product,
                Reviews = reviewViewModels,
                AddReview = new AddReviewViewModel { ProductId = id },
                AverageRating = averageRating,
                ReviewsCount = reviewViewModels.Count,
                IsFavorite = isFavorite
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
        public async Task<IActionResult> AddReview(AddReviewViewModel model)
        {
            // Проверяем, что пользователь аутентифицирован
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("SignIn", "Home");
            }

            // Получаем ID пользователя из Claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Не удалось получить ID пользователя.");
            }

            // Проверяем валидность модели
            if (!ModelState.IsValid)
            {
                // Можно передать ошибки в TempData/ViewData, если нужно
                TempData["Error"] = "Некорректные данные для отзыва.";
                return RedirectToAction("Details", new { id = model.ProductId });
            }

            // Создаём объект Review с именами свойств, соответствующими JSON (camelCase)
            var review = new
            {
                idUser = userId,
                idProduct = model.ProductId,
                rating = model.Rating,
                textReviews = model.Text,
                createdDate = DateTime.Now
            };

            // Отправляем POST-запрос на API
            var response = await _httpClient.PostAsJsonAsync("reviews", review);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction("Details", new { id = model.ProductId });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                TempData["Error"] = $"Ошибка при добавлении отзыва: {errorContent}";
                return RedirectToAction("Details", new { id = model.ProductId });
            }
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

            var productResponse = await _httpClient.GetAsync($"products/{Id}");
            if (!productResponse.IsSuccessStatusCode)
            {
                return NotFound("Товар не найден.");
            }

            var product = await productResponse.Content.ReadFromJsonAsync<Product>();

            if (Quantity <= product.Quantity) // Проверка доступного количества
            {
                var existingCartLine = cart.CartLines.FirstOrDefault(cl => cl.ProductId == Id);
                if (existingCartLine != null)
                {
                    existingCartLine.Quantity += Quantity; // Увеличиваем количество
                }
                else
                {
                    // Получаем URL первого изображения из продукта
                    string imageUrl = product.ProductImages?.FirstOrDefault()?.Url_image ?? "/media/default_image.png";

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

        private async Task<List<T>> GetListAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<T>>();
            }
            return new List<T>();
        }

        private async Task<List<T>> GetListWithIncludesAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<T>>();
            }
            return new List<T>();
        }



        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("SignIn", "Home");
            }

            // Получаем пользователя по логину через API
            var userResponse = await _httpClient.GetAsync($"users/bylogin/{User.Identity.Name}");
            if (!userResponse.IsSuccessStatusCode)
            {
                return RedirectToAction("SignIn", "Home");
            }

            var user = await userResponse.Content.ReadFromJsonAsync<User>();

            if (user != null && user.Id_role == 3)
            {
                ViewBag.UserName = $"{user.First_name} {user.Last_name}";
                ViewBag.UserRole = user.Role?.Role_name;

                var boutiques = await GetListAsync<Boutique>("boutiques");
                var products = await GetListWithIncludesAsync<Product>("products");
                var materials = await GetListAsync<ProductMaterial>("productmaterials");
                var types = await GetListAsync<ProductType>("producttypes");
                var orders = await GetListWithIncludesAsync<Order>("orders");

                var totalSales = orders.Sum(o => o.TotalPrice);

                var salesOverTime = new List<decimal>(new decimal[12]);
                var salesDistribution = orders.SelectMany(o => o.OrderItems)
                    .GroupBy(oi => new { oi.Product.Id_product, oi.Product.Product_name })
                    .Select(g => new SalesDistribution
                    {
                        ProductId = g.Key.Id_product,
                        ProductName = g.Key.Product_name,
                        TotalQuantity = g.Sum(oi => oi.Quantity)
                    }).ToList();

                var now = DateTime.UtcNow;
                foreach (var order in orders)
                {
                    var orderDate = order.OrderDate.Date;
                    if (orderDate >= now.AddMonths(-12))
                    {
                        int monthIndex = (orderDate.Month - now.Month + 12) % 12;
                        salesOverTime[monthIndex] += order.TotalPrice;
                    }
                }

                var model = new ManagerViewModel
                {
                    Products = products,
                    Boutiques = boutiques,
                    Materials = materials,
                    Types = types,
                    TotalSales = totalSales,
                    SalesDistributions = salesDistribution,
                    SalesOverTime = salesOverTime
                };

                return View("ManagerDashboard", model);
            }

            if (user != null && user.Id_role == 2)
            {
                ViewBag.UserName = $"{user.First_name} {user.Last_name}";
                ViewBag.UserRole = user.Role?.Role_name;

                var boutiques = await GetListAsync<Boutique>("boutiques");
                var products = await GetListWithIncludesAsync<Product>("products");
                var materials = await GetListAsync<ProductMaterial>("productmaterials");
                var types = await GetListAsync<ProductType>("producttypes");
                var users = await GetListWithIncludesAsync<User>("users");
                var roles = await GetListAsync<Role>("roles");

                var model = (products.AsEnumerable(), boutiques.AsEnumerable(), materials.AsEnumerable(), types.AsEnumerable(), users.AsEnumerable(), roles.AsEnumerable());
                return View("AdminDashboard", model);
            }

            if (user != null && user.Id_role == 1)
            {
                ViewBag.UserName = $"{user.First_name} {user.Last_name}";
                ViewBag.UserRole = user.Role?.Role_name;

                var favoriteProductIdsResponse = await _httpClient.GetAsync($"favorites/user/{user.Id_user}");
                List<int> favoriteProductIds = new List<int>();
                if (favoriteProductIdsResponse.IsSuccessStatusCode)
                {
                    favoriteProductIds = await favoriteProductIdsResponse.Content.ReadFromJsonAsync<List<int>>();
                }

                ViewBag.FavoriteProductIds = favoriteProductIds;
                HttpContext.Session.Remove("ShoppingCart");
                return View("Index");
            }

            var allProducts = await GetListAsync<Product>("products");
            return View(allProducts);
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
            var response = await _httpClient.GetAsync("orders");
            if (!response.IsSuccessStatusCode)
            {
                // Обработка ошибки, например, вернуть пустой список
                return Enumerable.Empty<SalesAnalytics>();
            }

            var orders = await response.Content.ReadFromJsonAsync<List<Order>>();

            var salesData = from order in orders
                            from item in order.OrderItems
                            select new SalesAnalytics
                            {
                                OrderId = order.Id,
                                OrderDate = order.OrderDate,
                                TotalPrice = order.TotalPrice,
                                ProductName = item.Product.Product_name, // используйте правильные имена свойств
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
            var response = await _httpClient.GetAsync("orders");
            if (!response.IsSuccessStatusCode)
            {
                // Обработка ошибки, например, показать пустой список или сообщение
                return View(new List<Order>());
            }

            var orders = await response.Content.ReadFromJsonAsync<List<Order>>();

            return View(orders);
        }

        public IActionResult ShoppingCart()
        {
            ShoppingCart cart = new ShoppingCart();

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));

            return View(cart);
        }

        public async Task<IActionResult> Checkout()
        {
            if (!HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                return BadRequest("Корзина пуста.");
            }

            var cartJson = HttpContext.Session.GetString("ShoppingCart");
            var cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(cartJson);

            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest("Не удалось получить ID пользователя.");
            }

            decimal totalPrice = 0;
            var boutiqueDetails = new List<Boutique>();

            var orderDto = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                TotalPrice = 0,
                OrderItems = new List<OrderItem>()
            };

            foreach (var cartLine in cart.CartLines)
            {
                var productResponse = await _httpClient.GetAsync($"products/{cartLine.ProductId}");
                if (!productResponse.IsSuccessStatusCode)
                {
                    return BadRequest($"Товар с ID {cartLine.ProductId} не найден.");
                }

                var product = await productResponse.Content.ReadFromJsonAsync<Product>();

                if (product == null)
                {
                    return BadRequest($"Товар с ID {cartLine.ProductId} не найден.");
                }

                totalPrice += product.Price * cartLine.Quantity;

                orderDto.OrderItems.Add(new OrderItem
                {
                    ProductId = product.Id_product,
                    Quantity = cartLine.Quantity
                });

                if (product.Boutique != null)
                {
                    boutiqueDetails.Add(new Boutique
                    {
                        Id_boutique = product.Boutique.Id_boutique,
                        Boutique_address = product.Boutique.Boutique_address,
                        Opening_time = product.Boutique.Opening_time,
                        Closing_time = product.Boutique.Closing_time
                    });
                }
                else
                {
                    boutiqueDetails.Add(new Boutique
                    {
                        Boutique_address = "Адрес не указан",
                        Opening_time = TimeSpan.Zero,
                        Closing_time = TimeSpan.Zero
                    });
                }
            }

            orderDto.TotalPrice = totalPrice;

            var createOrderResponse = await _httpClient.PostAsJsonAsync("orders", orderDto);
            if (!createOrderResponse.IsSuccessStatusCode)
            {
                var error = await createOrderResponse.Content.ReadAsStringAsync();
                return BadRequest($"Ошибка при оформлении заказа: {error}");
            }

            ViewBag.TotalPrice = totalPrice;
            ViewBag.BoutiqueDetails = boutiqueDetails;

            HttpContext.Session.Remove("ShoppingCart");

            return View(cart);
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

        // Метод для отображения бутиков через API
        public async Task<IActionResult> Boutiques()
        {
            var response = await _httpClient.GetAsync("boutiques");
            if (!response.IsSuccessStatusCode)
            {
                // Обработка ошибки, например, вернуть пустой список или сообщение
                return View(new List<Boutique>());
            }

            var boutiques = await response.Content.ReadFromJsonAsync<List<Boutique>>();
            return View(boutiques);
        }

        // Метод для создания нового бутика через API
        [HttpPost]
        public async Task<IActionResult> CreateBoutique(string Boutique_address, TimeSpan Opening_time, TimeSpan Closing_time)
        {
            var newBoutique = new Boutique
            {
                Boutique_address = Boutique_address,
                Opening_time = Opening_time,
                Closing_time = Closing_time
            };

            var response = await _httpClient.PostAsJsonAsync("boutiques", newBoutique);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }

        // Метод для редактирования бутика через API
        [HttpPost]
        public async Task<IActionResult> EditBoutique(int id, string boutiqueAddress, TimeSpan openingTime, TimeSpan closingTime)
        {
            // Получаем существующий бутик из API
            var getResponse = await _httpClient.GetAsync($"boutiques/{id}");
            if (!getResponse.IsSuccessStatusCode)
            {
                return Json(new { success = false, error = "Бутик не найден" });
            }

            var existingBoutique = await getResponse.Content.ReadFromJsonAsync<Boutique>();
            if (existingBoutique == null)
            {
                return Json(new { success = false, error = "Бутик не найден" });
            }

            // Обновляем поля
            existingBoutique.Boutique_address = boutiqueAddress;
            existingBoutique.Opening_time = openingTime;
            existingBoutique.Closing_time = closingTime;

            // Отправляем обновлённый бутик на API (PUT запрос)
            var putResponse = await _httpClient.PutAsJsonAsync($"boutiques/{id}", existingBoutique);
            if (putResponse.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await putResponse.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }
        // Метод для удаления бутика через API
        [HttpPost]
        public async Task<IActionResult> DeleteBoutique(int id)
        {
            var response = await _httpClient.DeleteAsync($"boutiques/{id}");
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }

        // Метод для создания нового типа продукта через API
        [HttpPost]
        public async Task<IActionResult> CreateProductType(string Product_type_name)
        {
            var newType = new ProductType { Product_type_name = Product_type_name };

            var response = await _httpClient.PostAsJsonAsync("producttypes", newType);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }

        // Метод для редактирования типа продукта через API
        [HttpPost]
        public async Task<IActionResult> EditProductType(int id, string productTypeName)
        {
            // Получаем существующий тип продукта
            var getResponse = await _httpClient.GetAsync($"producttypes/{id}");
            if (!getResponse.IsSuccessStatusCode)
            {
                return Json(new { success = false, error = "Тип продукта не найден" });
            }

            var existingType = await getResponse.Content.ReadFromJsonAsync<ProductType>();
            if (existingType == null)
            {
                return Json(new { success = false, error = "Тип продукта не найден" });
            }

            existingType.Product_type_name = productTypeName;

            var putResponse = await _httpClient.PutAsJsonAsync($"producttypes/{id}", existingType);
            if (putResponse.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await putResponse.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }
        // Метод для удаления типа продукта через API
        [HttpPost]
        public async Task<IActionResult> DeleteProductType(int id)
        {
            var response = await _httpClient.DeleteAsync($"producttypes/{id}");
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }

        // Метод для создания нового материала через API
        [HttpPost]
        public async Task<IActionResult> CreateProductMaterial(string Material_name)
        {
            var newMaterial = new ProductMaterial { Material_name = Material_name };

            var response = await _httpClient.PostAsJsonAsync("productmaterials", newMaterial);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }

        // Метод для редактирования материала через API
        [HttpPost]
        public async Task<IActionResult> EditProductMaterial(int id, string materialName)
        {
            // Получаем существующий материал
            var getResponse = await _httpClient.GetAsync($"productmaterials/{id}");
            if (!getResponse.IsSuccessStatusCode)
            {
                return Json(new { success = false, error = "Материал не найден" });
            }

            var existingMaterial = await getResponse.Content.ReadFromJsonAsync<ProductMaterial>();
            if (existingMaterial == null)
            {
                return Json(new { success = false, error = "Материал не найден" });
            }

            existingMaterial.Material_name = materialName;

            var putResponse = await _httpClient.PutAsJsonAsync($"productmaterials/{id}", existingMaterial);
            if (putResponse.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await putResponse.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }

        // Метод для удаления материала через API
        [HttpPost]
        public async Task<IActionResult> DeleteProductMaterial(int id)
        {
            var response = await _httpClient.DeleteAsync($"productmaterials/{id}");
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, error });
            }
        }
        public async Task<IActionResult> Roles()
        {
            var response = await _httpClient.GetAsync("roles");
            if (!response.IsSuccessStatusCode)
            {
                // Обработка ошибки, например, вернуть пустой список
                return View(new List<Role>());
            }

            var roles = await response.Content.ReadFromJsonAsync<List<Role>>();
            return View(roles);
        }

        // Метод для создания новой роли через API
        [HttpPost]
        public async Task<IActionResult> CreateRole(string Role_name)
        {
            var newRole = new Role { Role_name = Role_name };

            var response = await _httpClient.PostAsJsonAsync("roles", newRole);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = error });
            }
        }

        // Метод для редактирования роли через API
        [HttpPost]
        public async Task<IActionResult> EditRole(int id, string roleName)
        {
            var getResponse = await _httpClient.GetAsync($"roles/{id}");
            if (!getResponse.IsSuccessStatusCode)
            {
                return Json(new { success = false, message = "Роль не найдена" });
            }

            var existingRole = await getResponse.Content.ReadFromJsonAsync<Role>();
            if (existingRole == null)
            {
                return Json(new { success = false, message = "Роль не найдена" });
            }

            existingRole.Role_name = roleName;

            var putResponse = await _httpClient.PutAsJsonAsync($"roles/{id}", existingRole);
            if (putResponse.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await putResponse.Content.ReadAsStringAsync();
                return Json(new { success = false, message = error });
            }
        }

        // Метод для удаления роли через API
        [HttpPost]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var response = await _httpClient.DeleteAsync($"roles/{id}");
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = error });
            }
        }

        // Метод для отображения пользователей через API
        public async Task<IActionResult> Users()
        {
            var response = await _httpClient.GetAsync("users");
            if (!response.IsSuccessStatusCode)
            {
                return View(new List<User>());
            }
            var users = await response.Content.ReadFromJsonAsync<List<User>>();
            return View(users);
        }

        // Метод для добавления нового пользователя через API
        [HttpPost]
        public async Task<IActionResult> CreateUser(User user)
        {
            var response = await _httpClient.PostAsJsonAsync("users", user);
            if (response.IsSuccessStatusCode)
                return Json(new { success = true });
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // Метод для редактирования пользователя через API
        [HttpPost]
        public async Task<IActionResult> EditUser(int id, User user)
        {
            var getResponse = await _httpClient.GetAsync($"users/{id}");
            if (!getResponse.IsSuccessStatusCode)
                return Json(new { success = false, message = "Пользователь не найден" });

            var existingUser = await getResponse.Content.ReadFromJsonAsync<User>();
            if (existingUser == null)
                return Json(new { success = false, message = "Пользователь не найден" });

            // Обновляем поля
            existingUser.First_name = user.First_name;
            existingUser.Last_name = user.Last_name;
            existingUser.Middle_name = user.Middle_name;
            existingUser.Id_role = user.Id_role;
            existingUser.User_login = user.User_login;
            existingUser.User_password = user.User_password; // Учтите, что пароль нужно хэшировать

            var putResponse = await _httpClient.PutAsJsonAsync($"users/{id}", existingUser);
            if (putResponse.IsSuccessStatusCode)
                return Json(new { success = true });

            var error = await putResponse.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // Метод для удаления пользователя через API
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var response = await _httpClient.DeleteAsync($"users/{id}");
            if (response.IsSuccessStatusCode)
                return Json(new { success = true });

            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // Метод для создания продукта через API
        [HttpPost]
        public async Task<IActionResult> CreateProduct(Product model, string imageUrl)
        {
            var response = await _httpClient.PostAsJsonAsync("products", model);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = error });
            }

            var createdProduct = await response.Content.ReadFromJsonAsync<Product>();

            if (!string.IsNullOrEmpty(imageUrl) && createdProduct != null)
            {
                var productImage = new ProductImage
                {
                    Url_image = imageUrl,
                    Id_product = createdProduct.Id_product
                };
                var imgResponse = await _httpClient.PostAsJsonAsync("productimages", productImage);
                if (!imgResponse.IsSuccessStatusCode)
                {
                    var error = await imgResponse.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = error });
                }
            }

            return Json(new { success = true });
        }

        // Метод для редактирования продукта через API
        [HttpPost]
        public async Task<IActionResult> EditProduct(Product product, List<string> ImageUrls)
        {
            var putResponse = await _httpClient.PutAsJsonAsync($"products/{product.Id_product}", product);
            if (!putResponse.IsSuccessStatusCode)
            {
                var error = await putResponse.Content.ReadAsStringAsync();
                return Json(new { success = false, message = error });
            }

            // Удаляем старые изображения
            var imagesResponse = await _httpClient.GetAsync($"productimages/product/{product.Id_product}");
            if (imagesResponse.IsSuccessStatusCode)
            {
                var existingImages = await imagesResponse.Content.ReadFromJsonAsync<List<ProductImage>>();
                if (existingImages != null)
                {
                    foreach (var img in existingImages)
                    {
                        await _httpClient.DeleteAsync($"productimages/{img.Id_image}");
                    }
                }
            }

            // Добавляем новые изображения
            foreach (var url in ImageUrls)
            {
                var productImage = new ProductImage
                {
                    Url_image = url,
                    Id_product = product.Id_product
                };
                await _httpClient.PostAsJsonAsync("productimages", productImage);
            }

            return Json(new { success = true });
        }

        // Метод для удаления продукта через API
        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            // Удаляем изображения
            var imagesResponse = await _httpClient.GetAsync($"productimages/product/{id}");
            if (imagesResponse.IsSuccessStatusCode)
            {
                var images = await imagesResponse.Content.ReadFromJsonAsync<List<ProductImage>>();
                if (images != null)
                {
                    foreach (var img in images)
                    {
                        await _httpClient.DeleteAsync($"productimages/{img.Id_image}");
                    }
                }
            }

            // Удаляем продукт
            var response = await _httpClient.DeleteAsync($"products/{id}");
            if (response.IsSuccessStatusCode)
                return Json(new { success = true });

            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        private bool IsUserAuthorized()
        {
            var userRole = User.FindFirst("Role")?.Value; // Получаем роль пользователя из claims
            return userRole == "Администратор" || userRole == "Менеджер"; // Проверка на администраторов и менеджеров
        }

        // Проверка существования продукта через API
        private async Task<bool> ProductExists(int id)
        {
            var response = await _httpClient.GetAsync($"products/{id}");
            return response.IsSuccessStatusCode;
        }

        // Проверка существования пользователя через API
        private async Task<bool> UserExists(int id)
        {
            var response = await _httpClient.GetAsync($"users/{id}");
            return response.IsSuccessStatusCode;
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var response = await _httpClient.GetAsync($"users/login?login={Uri.EscapeDataString(model.Login)}&password={Uri.EscapeDataString(model.Password)}");

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Некорректные логин или пароль");
                return View(model);
            }

            var user = await response.Content.ReadFromJsonAsync<User>();
            if (user == null)
            {
                ModelState.AddModelError("", "Некорректные логин или пароль");
                return View(model);
            }

            await Authenticate(user);

            return RedirectToAction("Index", "Home");
        }


        private async Task Authenticate(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            string userLogin = user.User_login ?? throw new ArgumentNullException(nameof(user.User_login));
            string userId = user.Id_user.ToString() ?? throw new ArgumentNullException(nameof(user.Id_user));

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, userLogin),
        new Claim(ClaimTypes.NameIdentifier, userId),
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
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
            //if (!ModelState.IsValid)
            //{
            //    return View(person);
            //}

            // Проверяем, существует ли пользователь с таким логином через API
            var existingUserResponse = await _httpClient.GetAsync($"users/bylogin/{Uri.EscapeDataString(person.User_login)}");
            if (existingUserResponse.IsSuccessStatusCode)
            {
                ModelState.AddModelError("UserLogin", "Пользователь с таким логином уже существует.");
                return View(person);
            }
            else if (existingUserResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                ModelState.AddModelError("", "Ошибка при проверке существования пользователя.");
                return View(person);
            }

            var createResponse = await _httpClient.PostAsJsonAsync("users", person);
            if (createResponse.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Вы успешно зарегистрированы!";
                return RedirectToAction("SignUp");
            }
            else if (createResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorJson = await createResponse.Content.ReadAsStringAsync();

                var validationProblem = System.Text.Json.JsonSerializer.Deserialize<Models.ValidationProblemDetails>(errorJson, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (validationProblem?.Errors != null)
                {
                    foreach (var key in validationProblem.Errors.Keys)
                    {
                        foreach (var errorMsg in validationProblem.Errors[key])
                        {
                            ModelState.AddModelError(key, errorMsg);
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Ошибка при регистрации");
                }
                return View(person);
            }
            else
            {
                var error = await createResponse.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"Ошибка при регистрации: {error}");
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
            // Получаем продукты с включениями через API
            var productsResponse = await _httpClient.GetAsync("products");
            if (!productsResponse.IsSuccessStatusCode)
            {
                // Обработка ошибки, например вернуть пустой список
                return View("Catalog", new CatalogViewModel());
            }
            var products = await productsResponse.Content.ReadFromJsonAsync<List<Product>>();

            // Получаем материалы
            var materialsResponse = await _httpClient.GetAsync("productmaterials");
            var materials = materialsResponse.IsSuccessStatusCode
                ? await materialsResponse.Content.ReadFromJsonAsync<List<ProductMaterial>>()
                : new List<ProductMaterial>();

            // Получаем типы продуктов
            var typesResponse = await _httpClient.GetAsync("producttypes");
            var types = typesResponse.IsSuccessStatusCode
                ? await typesResponse.Content.ReadFromJsonAsync<List<ProductType>>()
                : new List<ProductType>();

            // Получаем ID пользователя, если аутентифицирован
            int userId = 0;
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
                {
                    userId = parsedUserId;
                }
            }

            // Получаем список ID избранных товаров пользователя через API
            List<int> favoriteProductIds = new List<int>();
            if (userId > 0)
            {
                var favResponse = await _httpClient.GetAsync($"favorites/user/{userId}");
                if (favResponse.IsSuccessStatusCode)
                {
                    favoriteProductIds = await favResponse.Content.ReadFromJsonAsync<List<int>>();
                }
            }

            // Получаем корзину из сессии
            ShoppingCart cart = new ShoppingCart();
            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                var cartJson = HttpContext.Session.GetString("ShoppingCart");
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(cartJson);
            }

            // Создаём модель для передачи в представление
            var model = new CatalogViewModel
            {
                Products = products,
                Materials = materials,
                Types = types,
                ShoppingCart = cart,
                ReviewCounts = new Dictionary<int, int>(),
                AverageRatings = new Dictionary<int, decimal>()
            };

            // Для каждого продукта получаем количество отзывов и средний рейтинг через API
            foreach (var product in model.Products)
            {
                var countResponse = await _httpClient.GetAsync($"reviews/count/{product.Id_product}");
                if (countResponse.IsSuccessStatusCode)
                {
                    model.ReviewCounts[product.Id_product] = await countResponse.Content.ReadFromJsonAsync<int>();
                }
                else
                {
                    model.ReviewCounts[product.Id_product] = 0;
                }

                if (model.ReviewCounts[product.Id_product] > 0)
                {
                    var avgResponse = await _httpClient.GetAsync($"reviews/average/{product.Id_product}");
                    if (avgResponse.IsSuccessStatusCode)
                    {
                        model.AverageRatings[product.Id_product] = await avgResponse.Content.ReadFromJsonAsync<decimal>();
                    }
                    else
                    {
                        model.AverageRatings[product.Id_product] = 0m;
                    }
                }
                else
                {
                    model.AverageRatings[product.Id_product] = 0m;
                }
            }

            ViewBag.FavoriteProductIds = favoriteProductIds;

            return View("Catalog", model);
        }

    }
}