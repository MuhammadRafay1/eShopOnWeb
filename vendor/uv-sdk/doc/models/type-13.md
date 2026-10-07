
# Type 13

Account group type.

* PERSONAL - Account group of a person holding assets on their own behalf.
* LEGAL_ENTITY - Account group of a legal entity holding assets on behalf of their users.
* FRENCH_PEA - Account group of a french resident holding assets in Plan d'Epargne en Actions.
* ISA - Account group of a UK resident holding assets in an individual savings account.
* CHILD - Account group of a child user holding assets in a child account.

## Enumeration

`Type13`

## Fields

| Name |
|  --- |
| `Personal` |
| `LegalEntity` |
| `FrenchPea` |
| `Isa` |
| `Child` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Type13 type13 = Type13.LegalEntity;
```

