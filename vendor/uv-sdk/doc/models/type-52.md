
# Type 52

Type of identification.

* `BIC` - Party identified by BIC.
* `PROPRIETARY` - Party identified by a proprietary code under a Data Source Scheme.
* `NAME_AND_ADDRESS` - Party identified by name and address.

## Enumeration

`Type52`

## Fields

| Name |
|  --- |
| `Bic` |
| `Proprietary` |
| `NameAndAddress` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Type52 type52 = Type52.Bic;
```

