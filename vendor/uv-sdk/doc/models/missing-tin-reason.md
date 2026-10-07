
# Missing Tin Reason

Reason why TIN is missing

* TIN_NOT_YET_ASSIGNED - Indicates that the tax identification number has not yet been assigned by the tax authorities. A common example is, that a user has moved to a country and thus became taxable, but that the tax authorities have not yet assigned the TIN to this user.
* COUNTRY_HAS_NO_TIN - Indicates that the specific country does not provide a TIN.
* OTHER_REASONS - Applies in case of other reasons - i.e. when a user does not have the TIN at hand. Note this may cause additional inquiries by our customer service team.

## Enumeration

`MissingTinReason`

## Fields

| Name |
|  --- |
| `TinNotYetAssigned` |
| `CountryHasNoTin` |
| `OtherReasons` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

MissingTinReason missingTinReason = MissingTinReason.TinNotYetAssigned;
```

