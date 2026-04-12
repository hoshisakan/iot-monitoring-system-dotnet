using System.Security.Cryptography;
using System.Text;
using Pico2WH.Pi5.IIoT.Application.Common.Interfaces;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Identity.Security;

/// <summary>PBKDF2-HMAC-SHA256（格式：<c>v1|iter|saltB64|keyB64</c>）。</summary>
public sealed class PasswordHasherService : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1|{Iterations}|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('|');
        if (parts.Length != 4 || parts[0] != "v1")
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations < 1)
            return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
