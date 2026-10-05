using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>
/// Builds request bodies for Upvest as UTF-8 JSON with a bare <c>application/json</c> content type
/// (no charset parameter), so the signed <c>content-type</c> component matches what is sent.
/// </summary>
internal static class UpvestContent
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null, // field names are provided verbatim (snake_case)
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static HttpContent Json<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }
}
