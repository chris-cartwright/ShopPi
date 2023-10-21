using System.IO.Ports;
using System.Text;
using Serilog;
using ILogger = Serilog.ILogger;

namespace ShopPi
{
    public static class Util
    {
	    private static readonly ILogger Logger = Log.ForContext(typeof(Util));

        public enum Users
        {
            Chris,
            Courtney
        }

        public enum Integrations
        {
	        Spotify,
	        ToDo
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

        public static async Task<Token?> GetSpotifyTokenAsync(Users user, IConfiguration config, IReadOnlyDictionary<string, string> formData)
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
                await Storage.SetTokenAsync(user, Integrations.Spotify, null);
                Logger
	                .ForContext("Response", await response.Content.ReadAsStringAsync())
	                .Information("Could not get OAuth token from Spotify.");
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

        public static async Task<Token?> GetToDoTokenAsync(Users user, IConfiguration config,
	        IReadOnlyDictionary<string, string> formData)
        {
	        var data = formData.ToDictionary(p => p.Key, p => p.Value);
	        data["client_id"] = config["ToDo:ClientId"];
	        data["client_secret"] = config["ToDo:ClientSecret"];

	        var client = new HttpClient();

			var response = await client.PostAsync(
				"https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
				new FormUrlEncodedContent(data)
			);

			if (!response.IsSuccessStatusCode)
			{
				await Storage.SetTokenAsync(user, Integrations.ToDo, null);
				Logger
					.ForContext("Response", await response.Content.ReadAsStringAsync())
					.Information("Could not get OAuth token from ToDo.");
				return null;
			}

			var rawToken = await response.Content.ReadFromJsonAsync<MicrosoftTokenResponse>();
			if (rawToken is null)
			{
				// This should never happen.
				throw new InvalidOperationException();
			}

			var expires = DateTimeOffset.Now + TimeSpan.FromSeconds(rawToken.expires_in);
			return new Token(rawToken.access_token, rawToken.refresh_token, expires);
		}

        public static void WriteBytes(this SerialPort port, params byte[] bytes)
        {
            port.Write(bytes, 0, bytes.Length);
        }

        public static string ReadUntil(this SerialPort port, char character)
        {
            var ret = new StringBuilder();
            char ch;
            do
            {
                ch = (char)port.ReadChar();
                ret.Append(ch);
            }
            while(ch != character);

            return ret.ToString();
        }
    }
}
