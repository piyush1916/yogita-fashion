namespace YogitaFashionAPI.Models
{
    public class Address
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string FullName { get; set; } = "";

        public string Phone { get; set; } = "";

        public string City { get; set; } = "";

        public string State { get; set; } = "";

        public string Pincode { get; set; } = "";

        public string Street { get; set; } = "";
    }
}