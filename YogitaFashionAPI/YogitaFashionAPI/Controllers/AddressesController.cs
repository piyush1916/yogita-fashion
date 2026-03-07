using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Controllers5
{
    [Route("addresses")]
    [ApiController]
    public class AddressesController : ControllerBase
    {
        private static List<Address> addresses = new List<Address>();

        [HttpGet]
        public IActionResult GetAddresses()
        {
            return Ok(addresses);
        }

        [HttpPost]
        public IActionResult AddAddress(Address address)
        {
            address.Id = addresses.Count + 1;
            addresses.Add(address);
            return Ok(address);
        }

        [HttpPatch("{id}")]
        public IActionResult UpdateAddress(int id, Address updatedAddress)
        {
            var address = addresses.FirstOrDefault(a => a.Id == id);

            if (address == null)
            {
                return NotFound("Address not found");
            }

            address.FullName = updatedAddress.FullName;
            address.Phone = updatedAddress.Phone;
            address.City = updatedAddress.City;
            address.State = updatedAddress.State;
            address.Pincode = updatedAddress.Pincode;
            address.Street = updatedAddress.Street;
            address.UserId = updatedAddress.UserId;

            return Ok(address);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteAddress(int id)
        {
            var address = addresses.FirstOrDefault(a => a.Id == id);

            if (address == null)
            {
                return NotFound("Address not found");
            }

            addresses.Remove(address);
            return Ok(new { message = "Address deleted successfully" });
        }
    }
}