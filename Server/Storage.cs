using System.Diagnostics;
using System.Text.Json;
using Serilog;

namespace ShopPi;

public record OAuthToken(string Access, string Refresh, DateTimeOffset Expires);

public static class Storage
{
	private static readonly Serilog.ILogger Logger = Log.ForContext(typeof(Storage));
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

	public static async Task CleanStatesAsync()
	{
		await LoadStorageAsync();
		Debug.Assert(_storage is not null);

		var now = DateTimeOffset.Now;
		_storage.States = _storage.States
			.Where(s => now - s.Timestamp > TimeSpan.FromDays(1))
			.ToList();

		await SaveStorageAsync();
	}

	public static async Task SetTokenAsync(Util.Users user, Util.Integrations integration, OAuthToken? token)
	{
		await LoadStorageAsync();
		Debug.Assert(_storage is not null);
		_storage.Tokens[user][integration] = token;
		await SaveStorageAsync();
	}

	public static async Task<OAuthToken?> GetTokenAsync(Util.Users user, Util.Integrations integration)
	{
		await LoadStorageAsync();
		Debug.Assert(_storage is not null);

		if (!_storage.Tokens.TryGetValue(user, out var integrations))
		{
			return null;
		}

		return integrations.TryGetValue(integration, out var token) ? token : null;
	}

	private static async Task LoadStorageAsync()
	{
		if (_storage is not null)
		{
			Logger.Debug("Storage already loaded.");
			return;
		}

		if (!File.Exists("storage.json"))
		{
			_storage = new();
			Logger.Debug("Storage created.");
			return;
		}

		await using var file = File.OpenRead("storage.json");
		_storage = await JsonSerializer.DeserializeAsync<JsonStorage>(file) ?? new();
		Logger.Information("Storage loaded.");
	}

	private static async Task SaveStorageAsync()
	{
		if (File.Exists("storage.json"))
		{
			File.Copy("storage.json", "storage.json.last", true);
		}

		// Truncate is important to prevent corruption in the event the current payload is smaller.
		// `FileMode.Create` includes the truncate.
		await using var file = new FileStream("storage.json", FileMode.Create, FileAccess.Write, FileShare.None);
		await JsonSerializer.SerializeAsync(
			file,
			_storage,
			new JsonSerializerOptions
			{
				WriteIndented = true
			}
		);
		Logger.Information("Storage saved.");
	}

	private record StateInfo(DateTimeOffset Timestamp, Util.Users User, string State);

	private class JsonStorage
	{
		public Dictionary<Util.Users, Dictionary<Util.Integrations, OAuthToken?>> Tokens { get; set; } = new();
		public List<StateInfo> States { get; set; } = new();

		public JsonStorage()
		{
			foreach (var user in Enum.GetValues<Util.Users>())
			{
				Tokens.TryAdd(user, new());
				foreach (var integration in Enum.GetValues<Util.Integrations>())
				{
					Tokens[user].TryAdd(integration, null);
				}
			}
		}
	}
}
