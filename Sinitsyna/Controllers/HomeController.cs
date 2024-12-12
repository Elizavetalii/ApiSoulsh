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
using Newtonsoft.Json; // Убедитесь, что у вас установлена библиотека iTextSharp

namespace Sinitsyna.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
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
                HttpContext.Session.Remove("ShoppingCart");
                return View("Index");
            }
            return View(await _context.Products.ToListAsync());
        }

        public async Task<IActionResult> ExportSalesData(string format)
        {
            var salesAnalytics = await GetSalesAnalytics();

            if (format == "pdf")
            {
                return GeneratePdf(salesAnalytics);
            }
            else if (format == "excel")
            {
                return GenerateExcel(salesAnalytics);
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

        private ActionResult GeneratePdf(IEnumerable<SalesAnalytics> salesData)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document();
                PdfWriter.GetInstance(document, stream);
                document.Open();

                document.Add(new Paragraph("Аналитика по продажам"));
                foreach (var sale in salesData)
                {
                    document.Add(new Paragraph($"Заказ ID: {sale.OrderId}, Дата: {sale.OrderDate}, Товар: {sale.ProductName}, Количество: {sale.Quantity}, Цена: {sale.Price:C}, Общая сумма: {sale.TotalPrice:C}"));
                }

                document.Close();
                return File(stream.ToArray(), "application/pdf", "sales_report.pdf");
            }
        }

        private ActionResult GenerateExcel(IEnumerable<SalesAnalytics> salesData)
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
        [HttpGet]
        public IActionResult AddToCart(int Id, int Quantity)
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

            var product = _context.Products.Find(Id);

            if (product != null)
            {
                if (Quantity <= product.Quantity) // Проверка доступного количества
                {
                    var existingCartLine = cart.CartLines.FirstOrDefault(cl => cl.Product.Id_product == Id);
                    if (existingCartLine != null)
                    {
                        existingCartLine.Quantity += Quantity; // Увеличиваем количество
                    }
                    else
                    {
                        cart.CartLines.Add(new CartLine { Product = product, Quantity = Quantity });
                    }

                    HttpContext.Session.SetString("ShoppingCart", System.Text.Json.JsonSerializer.Serialize(cart));
                    return Ok(cart); // Возвращаем обновленную корзину
                }
                else
                {
                    return BadRequest("Недостаточно товара на складе.");
                }
            }

            return NotFound("Товар не найден.");
        }

        [HttpPost]
        public IActionResult UpdateCart(int Id, int Quantity)
        {
            ShoppingCart cart;

            if (HttpContext.Session.Keys.Contains("ShoppingCart"))
            {
                cart = System.Text.Json.JsonSerializer.Deserialize<ShoppingCart>(HttpContext.Session.GetString("ShoppingCart"));

                var existingCartLine = cart.CartLines.FirstOrDefault(cl => cl.Product.Id_product == Id);
                if (existingCartLine != null)
                {
                    existingCartLine.Quantity = Quantity; // Обновляем количество
                }

                HttpContext.Session.SetString("ShoppingCart", System.Text.Json.JsonSerializer.Serialize(cart));
                return Ok(cart); // Возвращаем обновленную корзину
            }

            return BadRequest("Корзина пуста.");
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
                    var product = _context.Products.Include(p => p.Boutique).FirstOrDefault(p => p.Id_product == cartLine.Product.Id_product);
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
                    InventoryManager.ReleaseProduct(cartLine.Product.Id_product, cartLine.Quantity);
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
                    HttpContext.Session.SetString("AuthUser", model.Login);
                    await Authenticate(model.Login);

                    TempData["FirstName"] = user.First_name;
                    TempData["LastName"] = user.Last_name;
                    TempData["Role"] = user.Role.Role_name;
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Некорректные логин или пароль");
            }

            return RedirectToAction("SignIn", "Home");
        }

        private async Task Authenticate(string userName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimsIdentity.DefaultNameClaimType, userName)
            };

            // claims.Add(new Claim(ClaimTypes.Role, TempData["Role"].ToString())); // Добавляем роль пользователя

            ClaimsIdentity id = new ClaimsIdentity(claims, "ApplicationCookie", ClaimsIdentity.DefaultNameClaimType,
                ClaimsIdentity.DefaultRoleClaimType);

            //// Сохраняем имя и роль в сессии
            HttpContext.Session.SetString("FirstName", userName);
            //// Убедитесь, что роль должным образом добавляется из подходящего места (например, из базы данных)
            HttpContext.Session.SetString("Role", TempData["Role"]?.ToString() ?? "User");
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
        }
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); // Выход из аутентификации
            HttpContext.Session.Remove("AuthUser");

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
            var products = await _context.Products.Include(p => p.ProductType)
                                                  .Include(p => p.ProductMaterial)
                                                  .Include(p => p.ProductImages)
                                                  .ToListAsync();

            var materials = await _context.ProductMaterials.ToListAsync();
            var types = await _context.ProductTypes.ToListAsync();

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
                ShoppingCart = cart // Передаем корзину
            };

            return View(model);
        }
    }
}