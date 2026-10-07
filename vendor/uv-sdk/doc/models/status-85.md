
# Status 85

Status of the transfer

* NEW - Transfer is created.
* DISCOVERY - Discovery with the ceding provider where information about the transfer is being exchanged and reviewed. Only relevant if type ISA_EXTERNAL.
* DISCOVERY_CONFIRMED - Discovery is completed and transfer request is accepted by the ceding provider. Only relevant if type ISA_EXTERNAL.
* INSTRUCTED - Transfer is instructed to the ceding provider.
* PROCESSING - Transfer instruction is confirmed by the ceding provider. Transfer is being processed.
* SETTLED - Transfer is completed.

## Enumeration

`Status85`

## Fields

| Name |
|  --- |
| `New` |
| `Discovery` |
| `DiscoveryConfirmed` |
| `Instructed` |
| `Processing` |
| `Settled` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Status85 status85 = Status85.New;
```

