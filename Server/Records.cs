// ReSharper disable InconsistentNaming
namespace ShopPi
{
    public record SpotifyTokenResponse(string access_token, string? refresh_token, int expires_in);

    public record Token(string Access, string? Refresh, DateTimeOffset Expires);
}
