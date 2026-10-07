
# Transfer Type

Type of the securities transfer

* ISA_INTERNAL - Transfer occurs within the same ISA manager.
* ISA_EXTERNAL - Transfer occurs across different ISA managers, via Equisoft or others.

## Enumeration

`TransferType`

## Fields

| Name |
|  --- |
| `IsaInternal` |
| `IsaExternal` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

TransferType transferType = TransferType.IsaInternal;
```

