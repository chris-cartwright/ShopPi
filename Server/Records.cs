// ReSharper disable InconsistentNaming
namespace ShopPi
{
    public record SpotifyTokenResponse(string access_token, string? refresh_token, int expires_in);
}
