using System.Security.Claims;
using AspNetCore.Authentication.ApiKey;

namespace ShopPi
{
    public class ApiKey : IApiKey
    {
        public ApiKey(string key, string owner)
        {
            Key = key;
            OwnerName = owner;
            Claims = Array.Empty<Claim>();
        }

        public string Key { get; }
        public string OwnerName { get; }
        public IReadOnlyCollection<Claim> Claims { get; }
    }

	public class ApiKeyProvider : IApiKeyProvider
    {
        private readonly IConfiguration _config;

        public ApiKeyProvider(IConfiguration config)
        {
            _config = config;
        }

        public Task<IApiKey> ProvideAsync(string _)
        {
            var key = _config["ApiKey"];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("API key not set.");
            }

            return Task.FromResult<IApiKey>(new ApiKey(key, "WebClient"));
        }
    }
}
