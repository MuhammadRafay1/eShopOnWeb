using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// Parses the spec's error models: Error-List-Response ("errors": [...]) and
/// Customer-Error-Response ("errors": { ... } | [...]).
/// </summary>
public static class MaxioErrorResponse
{
    public static IReadOnlyList<string> Parse(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("errors", out var errors))
            {
                return Array.Empty<string>();
            }

            if (errors.ValueKind == JsonValueKind.Array)
            {
                return errors.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToArray();
            }

            if (errors.ValueKind == JsonValueKind.Object)
            {
                return errors.EnumerateObject()
                    .Select(p => $"{p.Name}: {p.Value}")
                    .ToArray();
            }

            return Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}