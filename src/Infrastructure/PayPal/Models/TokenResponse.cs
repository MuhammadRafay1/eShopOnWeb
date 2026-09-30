using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
