using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Arisoul.T212Ext.Web.Auth;

/// <summary>
/// Time-based one-time codes (RFC 6238): HMAC-SHA1, 30-second steps, 6 digits, the defaults every
/// authenticator app uses. Secrets are Base32 (RFC 4648), as authenticator apps expect them.
/// </summary>
public static class Totp
{
    public const int Digits = 6;
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>A new 160-bit secret, Base32 encoded.</summary>
    public static string NewSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    /// <summary>The time step a moment falls in (the counter of RFC 4226).</summary>
    public static long StepOf(DateTimeOffset time) => time.ToUnixTimeSeconds() / (long)Step.TotalSeconds;

    public static string Code(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(key, counter, hash);

        // Dynamic truncation (RFC 4226, section 5.3).
        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7FFFFFFF) % 1_000_000;
        return binary.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The step a code matches, allowing one step either side for clock drift, or null.
    /// The caller refuses a step already used, so a code works only once.
    /// </summary>
    public static long? Match(string secret, string? code, DateTimeOffset now)
    {
        code = code?.Replace(" ", "", StringComparison.Ordinal).Trim();
        if (code is not { Length: Digits } || !code.All(char.IsAsciiDigit))
            return null;

        var key = Base32Decode(secret);
        var current = StepOf(now);
        for (var step = current - 1; step <= current + 1; step++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(Code(key, step)),
                    System.Text.Encoding.ASCII.GetBytes(code)))
                return step;
        }
        return null;
    }

    /// <summary>The link an authenticator app reads from a QR code, or that can be opened on the phone.</summary>
    public static string ProvisioningUri(string secret, string username) =>
        $"otpauth://totp/T212Ext:{Uri.EscapeDataString(username)}?secret={secret}&issuer=T212Ext&digits={Digits}&period={(int)Step.TotalSeconds}";

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var output = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    /// <summary>Decodes Base32, ignoring case, spaces and padding (apps show secrets in groups).</summary>
    public static byte[] Base32Decode(string text)
    {
        var output = new List<byte>(text.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in text)
        {
            if (c is ' ' or '=' or '-')
                continue;
            var value = Alphabet.IndexOf(char.ToUpperInvariant(c));
            if (value < 0)
                throw new FormatException($"'{c}' is not a Base32 character.");
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
