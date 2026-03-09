using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Controllers
{
    [Route("addresses")]
    [ApiController]
    public class AddressesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AddressesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAddresses()
        {
            var items = await _db.Addresses
                .OrderByDescending(address => address.Id)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> AddAddress([FromBody] Address input)
        {
            var address = new Address
            {
                UserId = input.UserId,
                FullName = (input.FullName ?? "").Trim(),
                Phone = (input.Phone ?? "").Trim(),
                City = (input.City ?? "").Trim(),
                State = (input.State ?? "").Trim(),
                Pincode = (input.Pincode ?? "").Trim(),
                Street = (input.Street ?? "").Trim()
            };

            _db.Addresses.Add(address);
            await _db.SaveChangesAsync();
            return Ok(address);
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] Address input)
        {
            var address = await _db.Addresses.FirstOrDefaultAsync(item => item.Id == id);
            if (address == null)
            {
                return NotFound("Address not found");
            }

            address.FullName = (input.FullName ?? "").Trim();
            address.Phone = (input.Phone ?? "").Trim();
            address.City = (input.City ?? "").Trim();
            address.State = (input.State ?? "").Trim();
            address.Pincode = (input.Pincode ?? "").Trim();
            address.Street = (input.Street ?? "").Trim();
            address.UserId = input.UserId;

            await _db.SaveChangesAsync();
            return Ok(address);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var address = await _db.Addresses.FirstOrDefaultAsync(item => item.Id == id);
            if (address == null)
            {
                return NotFound("Address not found");
            }

            _db.Addresses.Remove(address);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Address deleted successfully" });
        }
    }
}
