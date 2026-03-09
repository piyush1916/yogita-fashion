using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("products")]
    public class ProductsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProductsController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _db;

        public ProductsController(
            IConfiguration configuration,
            ILogger<ProductsController> logger,
            IWebHostEnvironment environment,
            IHttpClientFactory httpClientFactory,
            AppDbContext db)
        {
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
            _httpClientFactory = httpClientFactory;
            _db = db;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts()
        {
            var threshold = GetLowStockThreshold();
            var products = await _db.Products
                .OrderByDescending(product => product.Id)
                .ToListAsync();

            var result = products
                .Select(product => ToProductResponse(product, threshold))
                .ToList();

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductById(int id)
        {
            var product = await _db.Products.FirstOrDefaultAsync(item => item.Id == id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found." });
            }

            return Ok(ToProductResponse(product, GetLowStockThreshold()));
        }

        [HttpGet("low-stock")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetLowStockProducts()
        {
            var threshold = GetLowStockThreshold();
            var items = await _db.Products
                .Where(product => product.Stock <= threshold)
                .OrderBy(product => product.Stock)
                .ToListAsync();

            return Ok(items.Select(product => ToProductResponse(product, threshold)).ToList());
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateProduct([FromBody] Product input)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                return BadRequest("Product name is required.");
            }

            var product = Normalize(input);
            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            await EnsureLowStockAlert(product);
            AuditLogStore.Add(User, "Create", "Product", product.Id.ToString(), $"Created product '{product.Name}'.");

            return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, ToProductResponse(product, GetLowStockThreshold()));
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product input)
        {
            var existing = await _db.Products.FirstOrDefaultAsync(item => item.Id == id);
            if (existing == null)
            {
                return NotFound(new { message = "Product not found." });
            }

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                return BadRequest("Product name is required.");
            }

            var normalized = Normalize(input);
            var previousStock = existing.Stock;
            var previousPrice = existing.Price;
            var previousOriginalPrice = existing.OriginalPrice;

            existing.Name = normalized.Name;
            existing.Description = normalized.Description;
            existing.Category = normalized.Category;
            existing.Brand = normalized.Brand;
            existing.Price = normalized.Price;
            existing.OriginalPrice = normalized.OriginalPrice;
            existing.Mrp = normalized.Mrp;
            existing.Size = normalized.Size;
            existing.Color = normalized.Color;
            existing.Sizes = normalized.Sizes;
            existing.Colors = normalized.Colors;
            existing.Stock = normalized.Stock;
            existing.ImageUrl = normalized.ImageUrl;
            existing.FeaturedProduct = normalized.FeaturedProduct;
            existing.IsBestSeller = normalized.IsBestSeller;

            await _db.SaveChangesAsync();

            if (previousStock <= 0 && existing.Stock > 0)
            {
                await NotifyAvailabilityForProduct(existing);
            }

            var currentDiscountActive = existing.OriginalPrice > existing.Price;
            var discountChanged = existing.Price != previousPrice || existing.OriginalPrice != previousOriginalPrice;
            if (currentDiscountActive && discountChanged)
            {
                await NotifySaleForProduct(existing);
            }

            await EnsureLowStockAlert(existing);
            AuditLogStore.Add(User, "Update", "Product", existing.Id.ToString(), $"Updated product '{existing.Name}'.");

            return Ok(ToProductResponse(existing, GetLowStockThreshold()));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var existing = await _db.Products.FirstOrDefaultAsync(item => item.Id == id);
            if (existing == null)
            {
                return NotFound(new { message = "Product not found." });
            }

            _db.Products.Remove(existing);
            InventoryAlertStore.ClearLowStockAlert(existing.Id);
            await _db.SaveChangesAsync();

            AuditLogStore.Add(User, "Delete", "Product", existing.Id.ToString(), $"Deleted product '{existing.Name}'.");
            return NoContent();
        }

        [HttpPost("upload-image")]
        [Authorize(Policy = "AdminOnly")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Image file is required." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Only JPG, PNG and WEBP files are allowed." });
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { message = "Image size must be up to 5MB." });
            }

            var cloudUpload = await TryUploadToCloudinary(file);
            if (cloudUpload.Success)
            {
                AuditLogStore.Add(User, "Upload", "ProductImage", cloudUpload.PublicId, $"Uploaded image to Cloudinary ({file.Length} bytes).");
                return Ok(new
                {
                    url = cloudUpload.Url,
                    relativeUrl = cloudUpload.Url,
                    fileName = cloudUpload.PublicId,
                    size = file.Length,
                    provider = "cloudinary"
                });
            }

            if (!string.IsNullOrWhiteSpace(cloudUpload.Error))
            {
                _logger.LogWarning("Cloudinary upload skipped/fallback: {Error}", cloudUpload.Error);
            }

            var root = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var uploadFolder = Path.Combine(root, "uploads", "products");
            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadFolder, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/products/{fileName}";
            var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativePath}";

            AuditLogStore.Add(User, "Upload", "ProductImage", fileName, $"Uploaded product image ({file.Length} bytes).");
            return Ok(new
            {
                url = absoluteUrl,
                relativeUrl = relativePath,
                fileName,
                size = file.Length,
                provider = "local"
            });
        }

        [HttpPost("{id:int}/stock-alerts")]
        [AllowAnonymous]
        public async Task<IActionResult> SubscribeStockAlert(int id, [FromBody] CreateStockAlertRequest request)
        {
            var product = await _db.Products.FirstOrDefaultAsync(item => item.Id == id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found." });
            }

            var email = (request.Email ?? "").Trim().ToLowerInvariant();
            var whatsAppNumber = NormalizePhone(request.WhatsAppNumber);

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(whatsAppNumber))
            {
                return BadRequest("Please provide email or WhatsApp number.");
            }

            var duplicate = await _db.StockAlertSubscriptions.FirstOrDefaultAsync(subscription =>
                subscription.ProductId == id &&
                string.Equals(subscription.Email, email, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(subscription.WhatsAppNumber, whatsAppNumber, StringComparison.Ordinal) &&
                subscription.NotifiedAt == null);

            if (duplicate != null)
            {
                return Ok(new
                {
                    message = "You are already subscribed for this product.",
                    duplicate.Id
                });
            }

            var next = new StockAlertSubscription
            {
                ProductId = id,
                Email = email,
                WhatsAppNumber = whatsAppNumber,
                CreatedAt = DateTime.UtcNow
            };

            _db.StockAlertSubscriptions.Add(next);
            await _db.SaveChangesAsync();

            return Created($"/products/{id}/stock-alerts/{next.Id}", next);
        }

        [HttpGet("stock-alerts")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetStockAlerts([FromQuery] bool pendingOnly = false)
        {
            var subscriptionsQuery = _db.StockAlertSubscriptions.AsQueryable();
            if (pendingOnly)
            {
                subscriptionsQuery = subscriptionsQuery.Where(item => item.NotifiedAt == null);
            }

            var subscriptions = await subscriptionsQuery
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();

            var productNames = await _db.Products
                .Where(item => subscriptions.Select(sub => sub.ProductId).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name);

            var result = subscriptions.Select(item => new
            {
                item.Id,
                item.ProductId,
                ProductName = productNames.TryGetValue(item.ProductId, out var name) ? name : "Deleted Product",
                item.Email,
                item.WhatsAppNumber,
                item.CreatedAt,
                item.NotifiedAt
            });

            return Ok(result);
        }

        [HttpPost("{id:int}/stock-alerts/notify")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> NotifyStockAlerts(int id)
        {
            var product = await _db.Products.FirstOrDefaultAsync(item => item.Id == id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found." });
            }

            if (product.Stock <= 0)
            {
                return BadRequest(new { message = "Product is still out of stock. Increase stock before sending alerts." });
            }

            var sentCount = await NotifyAvailabilityForProduct(product);
            AuditLogStore.Add(User, "Notify", "StockAlert", id.ToString(), $"Sent availability alert to {sentCount} customer(s).");

            return Ok(new
            {
                message = sentCount == 0
                    ? "No pending customer subscriptions for this product."
                    : $"Availability alerts sent to {sentCount} customer(s).",
                sentCount
            });
        }

        private object ToProductResponse(Product product, int lowStockThreshold)
        {
            return new
            {
                product.Id,
                product.Name,
                product.Price,
                product.OriginalPrice,
                product.Mrp,
                product.Category,
                product.Brand,
                product.ImageUrl,
                product.Description,
                product.Size,
                product.Color,
                product.Sizes,
                product.Colors,
                product.Stock,
                product.FeaturedProduct,
                product.IsBestSeller,
                IsOutOfStock = product.Stock <= 0,
                IsLowStock = product.Stock > 0 && product.Stock <= lowStockThreshold,
                LowStockThreshold = lowStockThreshold
            };
        }

        private static Product Normalize(Product input)
        {
            var price = Math.Max(0, input.Price);
            var originalPrice = input.OriginalPrice > 0
                ? input.OriginalPrice
                : (input.Mrp > 0 ? input.Mrp : price);

            var sizes = NormalizeList(input.Sizes, input.Size, new[] { "Free Size" });
            var colors = NormalizeList(input.Colors, input.Color, new[] { "Black" });
            var featured = input.FeaturedProduct || input.IsBestSeller;

            return new Product
            {
                Name = (input.Name ?? "").Trim(),
                Description = (input.Description ?? "").Trim(),
                Category = (input.Category ?? "").Trim(),
                Brand = (input.Brand ?? "").Trim(),
                Price = price,
                OriginalPrice = Math.Max(price, originalPrice),
                Mrp = Math.Max(price, originalPrice),
                Size = string.Join(", ", sizes),
                Color = string.Join(", ", colors),
                Sizes = sizes,
                Colors = colors,
                Stock = Math.Max(0, input.Stock),
                ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl)
                    ? "https://via.placeholder.com/300?text=Product"
                    : input.ImageUrl.Trim(),
                FeaturedProduct = featured,
                IsBestSeller = featured
            };
        }

        private static List<string> NormalizeList(IEnumerable<string> values, string csvFallback, IEnumerable<string> defaultValues)
        {
            var fromArray = values?
                .Select(item => (item ?? "").Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (fromArray.Count > 0)
            {
                return fromArray;
            }

            var fromCsv = (csvFallback ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (fromCsv.Count > 0)
            {
                return fromCsv;
            }

            return defaultValues.ToList();
        }

        private int GetLowStockThreshold()
        {
            var configured = _configuration.GetValue<int?>("Inventory:LowStockThreshold") ?? 5;
            return Math.Max(1, configured);
        }

        private async Task EnsureLowStockAlert(Product product)
        {
            var threshold = GetLowStockThreshold();
            if (product.Stock <= threshold)
            {
                if (InventoryAlertStore.IsLowStockAlertSent(product.Id))
                {
                    return;
                }

                var sendEnabled = _configuration.GetValue<bool?>("Inventory:SendLowStockEmails") ?? true;
                var recipient = _configuration["Inventory:AdminAlertEmail"] ?? "patilpiyush1619@gmail.com";
                if (sendEnabled)
                {
                    var subject = $"Low stock alert: {product.Name}";
                    var body =
                        $"Product: {product.Name}\n" +
                        $"Stock left: {product.Stock}\n" +
                        $"Threshold: {threshold}\n" +
                        $"Admin panel: http://127.0.0.1:5174/products/{product.Id}/edit";
                    await TrySendEmail(recipient, subject, body);
                }

                InventoryAlertStore.MarkLowStockAlertSent(product.Id);
                _logger.LogWarning("Low stock alert triggered for product {ProductId} ({ProductName}).", product.Id, product.Name);
                return;
            }

            InventoryAlertStore.ClearLowStockAlert(product.Id);
        }

        private async Task<int> NotifyAvailabilityForProduct(Product product)
        {
            var pending = await _db.StockAlertSubscriptions
                .Where(item => item.ProductId == product.Id && item.NotifiedAt == null)
                .ToListAsync();

            var sentCount = 0;
            foreach (var subscriber in pending)
            {
                var sent = await TrySendEmailNotification(product, subscriber);
                sent = await TrySendWhatsAppNotification(product, subscriber) || sent;

                if (sent)
                {
                    subscriber.NotifiedAt = DateTime.UtcNow;
                    sentCount++;
                }
            }

            if (sentCount > 0)
            {
                await _db.SaveChangesAsync();
            }

            return sentCount;
        }

        private async Task<bool> TrySendEmailNotification(Product product, StockAlertSubscription subscriber)
        {
            if (string.IsNullOrWhiteSpace(subscriber.Email))
            {
                return false;
            }

            var subject = $"Back in stock: {product.Name}";
            var body =
                $"Hello,\n\nGreat news. {product.Name} is available again in Yogita Fashion.\n" +
                $"Product link: http://127.0.0.1:5173/product/{product.Id}\n\n" +
                "You can place your order now.\n\nThank you.";

            return await TrySendEmail(subscriber.Email, subject, body);
        }

        private async Task<bool> TrySendWhatsAppNotification(Product product, StockAlertSubscription subscriber)
        {
            if (string.IsNullOrWhiteSpace(subscriber.WhatsAppNumber))
            {
                return false;
            }

            var simulate = _configuration.GetValue<bool>("AvailabilityAlerts:Simulate");
            var senderNumber = _configuration["AvailabilityAlerts:WhatsApp:SenderNumber"] ?? "+917448187062";
            if (simulate)
            {
                _logger.LogInformation(
                    "SIMULATED WHATSAPP from {SenderNumber} to {Number}: {Product} is back in stock. Link: http://127.0.0.1:5173/product/{ProductId}",
                    senderNumber,
                    subscriber.WhatsAppNumber,
                    product.Name,
                    product.Id
                );
                await Task.CompletedTask;
                return true;
            }

            _logger.LogWarning("WhatsApp API provider is not configured. Could not send WhatsApp alert to {Number}.", subscriber.WhatsAppNumber);
            await Task.CompletedTask;
            return false;
        }

        private static string NormalizePhone(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }

        private async Task<(bool Success, string Url, string PublicId, string Error)> TryUploadToCloudinary(IFormFile file)
        {
            var cloudName = (_configuration["Cloudinary:CloudName"] ?? "").Trim();
            var uploadPreset = (_configuration["Cloudinary:UploadPreset"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(uploadPreset))
            {
                return (false, "", "", "Cloudinary config missing.");
            }

            try
            {
                using var form = new MultipartFormDataContent();
                await using var fileStream = file.OpenReadStream();
                form.Add(new StreamContent(fileStream), "file", file.FileName);
                form.Add(new StringContent(uploadPreset), "upload_preset");

                var client = _httpClientFactory.CreateClient();
                var endpoint = $"https://api.cloudinary.com/v1_1/{cloudName}/image/upload";
                using var response = await client.PostAsync(endpoint, form);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, "", "", $"Cloudinary error ({(int)response.StatusCode}).");
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var secureUrl = root.TryGetProperty("secure_url", out var secureUrlNode) ? secureUrlNode.GetString() ?? "" : "";
                var publicId = root.TryGetProperty("public_id", out var publicIdNode) ? publicIdNode.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(secureUrl))
                {
                    return (false, "", "", "Cloudinary response missing secure_url.");
                }

                return (true, secureUrl, publicId, "");
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Cloudinary upload failed. Falling back to local storage.");
                return (false, "", "", "Cloudinary upload failed.");
            }
        }

        private async Task<int> NotifySaleForProduct(Product product)
        {
            var recipients = await _db.Users
                .Where(user =>
                    !string.IsNullOrWhiteSpace(user.Email) &&
                    !string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                .Select(user => user.Email.Trim().ToLowerInvariant())
                .Distinct()
                .ToListAsync();

            var salePercent = product.OriginalPrice > 0
                ? Math.Round(((product.OriginalPrice - product.Price) / product.OriginalPrice) * 100, 2)
                : 0;

            var sentCount = 0;
            foreach (var recipient in recipients)
            {
                var subject = $"Sale Alert: {product.Name} is now on discount";
                var body =
                    $"Hello,\n\nGreat news. {product.Name} is now on sale.\n" +
                    $"Discount: {salePercent}% off\n" +
                    $"Now: Rs {product.Price} (MRP Rs {product.OriginalPrice})\n" +
                    $"Buy now: http://127.0.0.1:5173/product/{product.Id}\n\n" +
                    "Thank you.";

                if (await TrySendEmail(recipient, subject, body))
                {
                    sentCount++;
                }
            }

            return sentCount;
        }

        private async Task<bool> TrySendEmail(string recipientEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return false;
            }

            var simulate = _configuration.GetValue<bool>("AvailabilityAlerts:Simulate");
            var senderEmail = _configuration["AvailabilityAlerts:Email:SenderEmail"] ?? "patilpiyush1788@gmail.com";
            var senderName = _configuration["AvailabilityAlerts:Email:SenderName"] ?? "Yogita Fashion";

            if (simulate)
            {
                _logger.LogInformation("SIMULATED EMAIL from {Sender} to {Receiver}: {Subject}", senderEmail, recipientEmail, subject);
                await Task.CompletedTask;
                return true;
            }

            var smtpHost = _configuration["AvailabilityAlerts:Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = _configuration.GetValue<int?>("AvailabilityAlerts:Email:SmtpPort") ?? 587;
            var appPassword = _configuration["AvailabilityAlerts:Email:AppPassword"] ?? "";
            var useSsl = _configuration.GetValue<bool?>("AvailabilityAlerts:Email:UseSsl") ?? true;

            if (string.IsNullOrWhiteSpace(appPassword))
            {
                _logger.LogWarning("Email app password is missing. Cannot send email to {Receiver}.", recipientEmail);
                return false;
            }

            try
            {
                using var message = new MailMessage(new MailAddress(senderEmail, senderName), new MailAddress(recipientEmail))
                {
                    Subject = subject,
                    Body = body
                };

                using var smtp = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = useSsl,
                    Credentials = new NetworkCredential(senderEmail, appPassword)
                };

                await smtp.SendMailAsync(message);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to send email to {Receiver}.", recipientEmail);
                return false;
            }
        }
    }
}
