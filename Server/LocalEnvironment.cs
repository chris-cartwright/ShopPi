namespace ShopPi;

public class LocalEnvironment
{
	public Uri BaseUrl { get; }
	public Uri SpotifyCallback { get; }
	public Uri ToDoCallback { get; }

	public IConfiguration Config { get; }

	public LocalEnvironment(IConfiguration config)
	{
		Config = config;
		BaseUrl = new Uri(config["Urls"]);
		SpotifyCallback = new Uri(BaseUrl, "/api/spotify/callback");
		ToDoCallback = new Uri(BaseUrl, "/api/todo/callback");
	}
}
