using System;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Webhooks;

/// <summary>Decodes base64url values (as used in JWKS key coordinates) to bytes.</summary>
internal static class Base64Url
{
    public static byte[] Decode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
