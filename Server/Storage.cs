using System.Diagnostics;
using System.Text.Json;

namespace ShopPi
{
    public record SpotifyToken(string Access, string Refresh, DateTimeOffset Expires);

    public static class Storage
    {
        private static JsonStorage? _storage;

        public static async Task AddStateAsync(string state)
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            _storage.States.Add(new StateInfo(DateTimeOffset.Now, state));
            await SaveStorageAsync();
        }

        public static async Task<bool> StateExistsAsync(string state)
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            var min = DateTimeOffset.Now - TimeSpan.FromHours(1);
            return _storage.States.Where(s => s.Timestamp >= min).FirstOrDefault(s => s.State == state) != default;
        }

        public static async Task SetTokenAsync(SpotifyToken? token)
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            _storage.Token = token;
            await SaveStorageAsync();
        }

        public static async Task<SpotifyToken?> GetTokenAsync()
        {
            await LoadStorageAsync();
            Debug.Assert(_storage is not null);

            return _storage.Token;
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

        private record StateInfo(DateTimeOffset Timestamp, string State);

        private class JsonStorage
        {
            public SpotifyToken? Token { get; set; }
            public List<StateInfo> States { get; set; } = new();
        }
    }
}
