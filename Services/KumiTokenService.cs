using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace KimiSync.Services;

public sealed class KumiTokenService
{
    private readonly byte[] key;

    public KumiTokenService(IConfiguration configuration, IHostEnvironment environment)
    {
        var path = configuration["SECRET_PATH"] ?? Path.Combine(environment.ContentRootPath, "secret.key");
        var secret = File.ReadAllText(path);
        if (secret.Length < 2048)
            throw new InvalidOperationException("secret.key must contain at least 2048 characters. Run dotnet run -- --new-secret.");
        key = Encoding.UTF8.GetBytes(secret);
    }

    public string Issue()
    {
        var uuid = Guid.NewGuid().ToString();
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{uuid};{Convert.ToHexStringLower(Sign(uuid))}"));
    }

    public string? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 1024) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)).Split(';');
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "D", out _) || parts[1].Length != 64)
                return null;
            var signature = Convert.FromHexString(parts[1]);
            return CryptographicOperations.FixedTimeEquals(Sign(parts[0]), signature) ? parts[0] : null;
        }
        catch (FormatException) { return null; }
    }

    private byte[] Sign(string uuid) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(uuid));
}
