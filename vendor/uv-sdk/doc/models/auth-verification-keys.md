
# Auth Verification Keys

Webhooks verification keys.

## Structure

`AuthVerificationKeys`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Keys` | [`List<Key>`](../../doc/models/key.md) | Required | List of verification keys |

## Example

```csharp
using System.Collections.Generic;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

AuthVerificationKeys authVerificationKeys = new AuthVerificationKeys
{
    Keys = new List<Key>
    {
        new Key
        {
            Kid = new Guid("0000081e-0000-0000-0000-000000000000"),
            Kty = Kty.Ec,
            Crv = Crv.P521,
            X = "x8",
            Y = "y0",
            ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
        },
    },
};
```

