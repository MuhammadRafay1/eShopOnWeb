
# Role Type 1

Role type for a business entity.

* `LEGAL_REPRESENTATIVE` — The user is a legal representative of the business.
* `AUTHORISED_SIGNATORY` — The user is authorised to sign documents and make commitments on behalf of the business.
* `ULTIMATE_BENEFICIAL_OWNER` — The user ultimately owns or controls the business.
* `CONTRACTING_EXECUTIVE` — The user is able to enter into contracts on behalf of the business.
* `TRADER` — The user is authorised to place orders on behalf of the business.
* `SOLE_TRADER` — The user places orders on behalf of a sole trader entity.

## Enumeration

`RoleType1`

## Fields

| Name |
|  --- |
| `LegalRepresentative` |
| `AuthorisedSignatory` |
| `UltimateBeneficialOwner` |
| `ContractingExecutive` |
| `Trader` |
| `SoleTrader` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

RoleType1 roleType1 = RoleType1.UltimateBeneficialOwner;
```

