using System;
using System.Security.Cryptography;
using System.Text;

namespace SpotifyNowPlayingOverlay.Utils
{
    public static class OAuthHelper
    {
        public static string GenerateCodeVerifier()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
            var bytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var result = new StringBuilder(bytes.Length);
            foreach (byte b in bytes)
            {
                result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }

        public static string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                return Base64UrlEncode(challengeBytes);
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            string base64 = Convert.ToBase64String(bytes);
            return base64
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
