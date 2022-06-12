using System.Diagnostics;
using System.Text.Json;

namespace ShopPi
{
    public record SpotifyToken(string Access, string Refresh, DateTimeOffset Expires);

    public static class Storage
    {
        private static JsonStorage? _storage;

        public static async Task AddStateAsync(Util.Users user, string state)
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            _storage.States.Add(new StateInfo(DateTimeOffset.Now, user, state));
            await SaveStorageAsync();
        }

        public static async Task<Util.Users?> GetUserForStateAsync(string state)
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            var min = DateTimeOffset.Now - TimeSpan.FromHours(1);
            var possible = _storage.States
                .Where(s => s.Timestamp >= min)
                .FirstOrDefault(s => s.State == state);
            return possible?.User;
        }

        public static async Task SetTokenAsync(Util.Users user, SpotifyToken? token)
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            if (token is null)
            {
                _storage.Tokens.Remove(user);
            }
            else
            {
                _storage.Tokens[user] = token;
            }

            await SaveStorageAsync();
        }

        public static async Task<SpotifyToken?> GetTokenAsync(Util.Users user)
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            return _storage.Tokens.TryGetValue(user, out var found) ? found : null;
        }

        private static async Task LoadStorageAsync()
        {
            if (_storage is not null)
            {
                return;
            }

            if (!File.Exists("storage.json"))
            {
                _storage = new();
                return;
            }

            await using var file = File.OpenRead("storage.json");
            _storage = await JsonSerializer.DeserializeAsync<JsonStorage>(file) ?? new();
        }

        private static async Task SaveStorageAsync()
        {
            await using var file = File.OpenWrite("storage.json");
            await JsonSerializer.SerializeAsync(
                file,
                _storage,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );
        }

        private record StateInfo(DateTimeOffset Timestamp, Util.Users User, string State);

        private class JsonStorage
        {
            public Dictionary<Util.Users, SpotifyToken> Tokens { get; set; } = new();
            public List<StateInfo> States { get; set; } = new();
        }
    }
}
