using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.Api.Services;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string ToBase32(byte[] data)
    {
        if (data == null || data.Length == 0) return string.Empty;

        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            result.Append(Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return result.ToString();
    }

    public static byte[] FromBase32(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();

        input = input.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in input)
        {
            int val = Alphabet.IndexOf(c);
            if (val < 0) continue;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}

public static class TotpService
{
    public static string GenerateSecretKey(int byteCount = 10)
    {
        byte[] randomBytes = new byte[byteCount];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Base32.ToBase32(randomBytes);
    }

    public static string GenerateCode(string base32Secret, DateTime? utcTime = null)
    {
        byte[] secretBytes = Base32.FromBase32(base32Secret);
        if (secretBytes.Length == 0) return string.Empty;

        var time = utcTime ?? DateTime.UtcNow;
        long timeStep = ((DateTimeOffset)time).ToUnixTimeSeconds() / 30;

        byte[] counter = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(secretBytes);
        byte[] hash = hmac.ComputeHash(counter);

        int offset = hash[hash.Length - 1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1000000;
        return otp.ToString("D6");
    }

    public static bool ValidateCode(string base32Secret, string inputCode, int driftWindow = 1)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(inputCode))
            return false;

        inputCode = inputCode.Trim();
        if (inputCode.Length != 6) return false;

        var now = DateTime.UtcNow;

        for (int i = -driftWindow; i <= driftWindow; i++)
        {
            var testTime = now.AddSeconds(i * 30);
            var expected = GenerateCode(base32Secret, testTime);
            if (string.Equals(expected, inputCode, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string GenerateOtpAuthUri(string email, string base32Secret, string issuer = "FundooNotes")
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";
    }

    public static string GenerateQrCodeUrl(string otpAuthUri)
    {
        return $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(otpAuthUri)}";
    }
}
