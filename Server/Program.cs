using System.Diagnostics;
using AspNetCore.Authentication.ApiKey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using ShopPi;

const string corsPolicyName = "CorsPolicy";

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
	.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
	.Enrich.FromLogContext()
	.WriteTo.File(new CompactJsonFormatter(), "server.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
	.WriteTo.Console()
	.MinimumLevel.Debug()
	.CreateBootstrapLogger();

Log.Information("Application starting...");


var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfig) =>
{
	loggerConfig
		.ReadFrom.Configuration(context.Configuration)
		.ReadFrom.Services(services)
		.Enrich.FromLogContext();
});
builder.Services.AddHostedService<Manager>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(ApiKeyDefaults.AuthenticationScheme)
	.AddApiKeyInHeader<ApiKeyProvider>(o =>
	{
		o.KeyName = "X-Api-Key";
		o.Realm = "ShopPi";
	});
builder.Services.AddCors(options =>
{
	options.AddPolicy(
		corsPolicyName,
		config =>
		{
			config.WithOrigins(
				"http://localhost:8080",
				"http://localhost:4000"
			);
			config.WithHeaders(
				"X-Api-Key"
			);
		}
	);
});

Log.Information("Building application...");
var app = builder.Build();
app.UseSerilogRequestLogging();

var staticFiles = Path.Combine(builder.Environment.ContentRootPath, "public");
app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(staticFiles)
});
Log.Information("Serving static files from {StaticFilesPath}.", staticFiles);

// This must come before `app.UseAuthorization()`.
app.UseCors(corsPolicyName);

// So this is weird.
// If these two are reversed, authentication stops working.
// -- Start
app.UseAuthentication();
app.UseAuthorization();
// -- End

app.MapGet("/api/echo", [Authorize] (string msg) => Results.Ok($"Echo: {msg}"));

app.MapGet("/api/spotify/authorize", [Authorize] async (Util.Users user, IConfiguration config) =>
{
	var state = Util.RandomString(14);
	await Storage.AddStateAsync(user, state);

	var scopes = new[]
	{
			"user-library-read",
			"user-library-modify",
			"user-read-playback-state",
			"user-modify-playback-state",
			"user-read-currently-playing"
	};

	var qs = new Dictionary<string, string?>
	{
		["response_type"] = "code",
		["client_id"] = config["Spotify:ClientId"],
		["scope"] = string.Join(" ", scopes),
		["redirect_uri"] = config["RedirectUri"],
		["state"] = state
	};
	return Results.Ok(QueryHelpers.AddQueryString("https://accounts.spotify.com/authorize", qs));
});

app.MapGet("/api/spotify/callback", async (string state, string code, IConfiguration config) =>
{
	var user = await Storage.GetUserForStateAsync(state);
	if (user is null)
	{
		return Results.Redirect("/?error=Invalid state returned.");
	}

	var form = new Dictionary<string, string>
	{
		["code"] = code,
		["redirect_uri"] = config["RedirectUri"],
		["grant_type"] = "authorization_code"
	};

	var rawToken = await Util.GetTokenAsync(user.Value, config, form);
	if (rawToken is null)
	{
		return Results.Redirect("/?error=Failed to obtain access token.");
	}

	var token = new SpotifyToken(
		rawToken.Access,
		rawToken.Refresh!,
		rawToken.Expires
	);
	await Storage.SetTokenAsync(user.Value, token);
	return Results.Redirect("/");
});

app.MapGet("/api/spotify/token", [Authorize] async (Util.Users user, IConfiguration config) =>
{
	var token = await Storage.GetTokenAsync(user);
	if (token is null)
	{
		return Results.NotFound();
	}

	if (token.Expires > DateTimeOffset.Now)
	{
		return Results.Ok(token.Access);
	}

	var form = new Dictionary<string, string>
	{
		["refresh_token"] = token.Refresh,
		["grant_type"] = "refresh_token"
	};

	var rawToken = await Util.GetTokenAsync(user, config, form);
	if (rawToken is null)
	{
		await Storage.SetTokenAsync(user, null);
		return Results.NotFound();
	}

	token = new SpotifyToken(
		rawToken.Access,
		rawToken.Refresh ?? token.Refresh,
		rawToken.Expires
	);
	await Storage.SetTokenAsync(user, token);
	return Results.Ok(token.Access);
});

app.MapDelete("/api/spotify/token", [Authorize] async (Util.Users user) =>
{
	await Storage.SetTokenAsync(user, null);
});

try
{
	app.Start();

	var server = app.Services.GetService<IServer>();
	var addressesFeature = server?.Features.Get<IServerAddressesFeature>();
	Debug.Assert(addressesFeature is not null);
	Log.Information("Listening on URLs: {Urls}", addressesFeature.Addresses);

	app.WaitForShutdown();
}
catch (Exception ex)
{
	Console.Error.WriteLine("Unknown application error: " + ex.GetType().Name);
	Console.Error.Write(ex);
	Log.Fatal(ex, "Unknown failure.");
}