using System.Diagnostics;
using System.Text;

namespace ShopPi
{
    public static class Util
    {
        public enum Users
        {
            Chris,
            Courtney
        }

        public static string RandomString(int length)
        {
            // ReSharper disable StringLiteralTypo
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            // ReSharper enable StringLiteralTypo

            return new string(
                Enumerable
                    .Repeat(chars, length)
                    .Select(s => s[Random.Shared.Next(s.Length)])
                    .ToArray()
            );
        }

        public static async Task<Token?> GetTokenAsync(Users user, IConfiguration config, IReadOnlyDictionary<string, string> formData)
        {
            var authorization = $"{config["Spotify:ClientId"]}:{config["Spotify:ClientSecret"]}";
            authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization));

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + authorization);

            var response = await client.PostAsync(
                "https://accounts.spotify.com/api/token",
                new FormUrlEncodedContent(formData)
            );

            if (!response.IsSuccessStatusCode)
            {
                await Storage.SetTokenAsync(user, null);
                Debug.Write(await response.Content.ReadAsStringAsync());
                return null;
            }

            var rawToken = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>();
            if (rawToken is null)
            {
                // This should never happen.
                throw new InvalidOperationException();
            }

            var expires = DateTimeOffset.Now + TimeSpan.FromSeconds(rawToken.expires_in);
            return new Token(rawToken.access_token, rawToken.refresh_token, expires);
        }
    }
}
