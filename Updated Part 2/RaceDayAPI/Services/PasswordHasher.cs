using System.Security.Cryptography;

namespace RaceDayAPI.Services
{
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2";
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100000;

        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        public static bool IsHashed(string stored)
        {
            return stored.StartsWith(Prefix + "$", StringComparison.Ordinal);
        }

        public static bool Verify(string password, string stored)
        {
            if (!IsHashed(stored))
                return false;

            string[] parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations))
                return false;

            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}