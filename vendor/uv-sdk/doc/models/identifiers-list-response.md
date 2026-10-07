
# Identifiers List Response

Response containing the list of all identifiers held by a user.

## Structure

`IdentifiersListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Data` | [`List<Identifier>`](../../doc/models/identifier.md) | Required | List of identifiers held by the user |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

IdentifiersListResponse identifiersListResponse = new IdentifiersListResponse
{
    Data = new List<Identifier>
    {
        new Identifier
        {
            Id = new Guid("00001c2a-0000-0000-0000-000000000000"),
            CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            UserId = new Guid("0000233a-0000-0000-0000-000000000000"),
            IdentifierStandard = "identifier_standard8",
            IdentifierProp = "identifier8",
            Type = Type7.NationalId,
            IssuingCountry = IssuingCountry.Td,
        },
    },
};
```

