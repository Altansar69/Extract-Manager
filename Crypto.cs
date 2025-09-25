using System.Text;

namespace ExtractManager;

public static class Crypto
{
    // A shared key used for basic XOR encryption.
    private static readonly byte[] SKey = "98d6984f-a727-42c6-a9b5-9db0287b0064"u8.ToArray();

    /// <summary>
    ///     Encrypts a string using a simple XOR cipher and then encodes it as Base64.
    /// </summary>
    public static string Encrypt(string plainText)
    {
        var data = Encoding.UTF8.GetBytes(plainText);
        for (var i = 0; i < data.Length; i++) data[i] = (byte)(data[i] ^ SKey[i % SKey.Length]);
        return Convert.ToBase64String(data);
    }

    /// <summary>
    ///     Decrypts a Base64 encoded string that was encrypted with the corresponding Encrypt(ion) method.
    /// </summary>
    /// <returns>The decrypted string, or null if decryption fails.</returns>
    public static string? Decrypt(string base64EncodedData)
    {
        try
        {
            var data = Convert.FromBase64String(base64EncodedData);
            for (var i = 0; i < data.Length; i++) data[i] = (byte)(data[i] ^ SKey[i % SKey.Length]);
            return Encoding.UTF8.GetString(data);
        }
        catch (FormatException)
        {
            // Occurs if the input string is not a valid Base64 string.
            return null;
        }
    }
}