using System.Security.Cryptography;

namespace YogitaFashionAPI.Services
{
    public static class PasswordService
    {
        private const string HashPrefix = "pbkdf2";
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int DefaultIterations = 100_000;

        public static string HashPassword(string password)
        {
            var normalized = password ?? string.Empty;
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(
                normalized,
                salt,
                DefaultIterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return $"{HashPrefix}${DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                return false;
            }

            if (!string.Equals(parts[0], HashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);
                var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPasswordHashed(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && value.StartsWith($"{HashPrefix}$", StringComparison.OrdinalIgnoreCase);
        }
    }
}
