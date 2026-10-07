
# User Documentation Type

Type of documentation supporting the US withholding tax status.

* KYC - Withholding status derived from the existing KYC data (non-resident aliens only).
* W8-BEN - IRS Form W-8BEN provided by the user (non-resident aliens only).
* W9 - IRS Form W-9 provided by the user (US persons only).
* UNDOCUMENTED - No documentation is available for the user.

For a non-resident alien, "KYC" is accepted for every operating model; whether "W8-BEN" and "UNDOCUMENTED" are also accepted depends on your operating model.

## Enumeration

`UserDocumentationType`

## Fields

| Name |
|  --- |
| `Kyc` |
| `W8Ben` |
| `W9` |
| `Undocumented` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

UserDocumentationType userDocumentationType = UserDocumentationType.W9;
```

