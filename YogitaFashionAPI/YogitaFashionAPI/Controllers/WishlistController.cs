using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Controllers
{
    [Route("wishlist")]
    [ApiController]
    public class WishlistController : ControllerBase
    {
        private readonly AppDbContext _db;

        public WishlistController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetWishlist()
        {
            var items = await _db.WishlistItems
                .OrderByDescending(item => item.Id)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> AddToWishlist([FromBody] WishlistItem input)
        {
            var item = new WishlistItem
            {
                UserId = input.UserId,
                ProductId = input.ProductId
            };

            _db.WishlistItems.Add(item);
            await _db.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{productId:int}")]
        public async Task<IActionResult> RemoveFromWishlist(int productId)
        {
            var item = await _db.WishlistItems.FirstOrDefaultAsync(entry => entry.ProductId == productId);
            if (item == null)
            {
                return NotFound("Wishlist item not found");
            }

            _db.WishlistItems.Remove(item);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Removed from wishlist" });
        }
    }
}
