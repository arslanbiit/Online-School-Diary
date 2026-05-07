using System.Security.Cryptography;
using System.Text;

namespace OnlineSchoolDiary.Shared.Security;

public static class PasswordHasher
{
    public static string Hash(string password, string? salt = null)
    {
        salt ??= "OnlineSchoolDiary::v1";
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{salt}::{password}");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    public static bool Verify(string password, string expectedHash, string? salt = null) =>
        string.Equals(Hash(password, salt), expectedHash, StringComparison.OrdinalIgnoreCase);
}

