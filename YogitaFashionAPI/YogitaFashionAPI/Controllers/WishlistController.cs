using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Controllers
{
    [Route("wishlist")]
    [ApiController]
    public class WishlistController : ControllerBase
    {
        private static List<WishlistItem> wishlist = new List<WishlistItem>();

        [HttpGet]
        public IActionResult GetWishlist()
        {
            return Ok(wishlist);
        }

        [HttpPost]
        public IActionResult AddToWishlist(WishlistItem item)
        {
            item.Id = wishlist.Count + 1;
            wishlist.Add(item);
            return Ok(item);
        }

        [HttpDelete("{productId}")]
        public IActionResult RemoveFromWishlist(int productId)
        {
            var item = wishlist.FirstOrDefault(w => w.ProductId == productId);

            if (item == null)
            {
                return NotFound("Wishlist item not found");
            }

            wishlist.Remove(item);
            return Ok(new { message = "Removed from wishlist" });
        }
    }
}