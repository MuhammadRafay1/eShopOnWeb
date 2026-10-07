
# Delay Reason Code

Categorised reason why a securities transfer is delayed past the regulatory deadline.

* `COUNTERPARTY_NOT_INSTRUCTING` - The counterparty has not yet instructed the transfer.
* `COUNTERPARTY_NOT_RESPONDING` - The counterparty is not responding.
* `COUNTERPARTY_DOES_NOT_AGREE_TO_DELIVERY_DETAILS` - The counterparty does not agree to the delivery details.
* `COUNTERPARTY_NOT_ACCEPTING` - The counterparty is not accepting the transfer.

## Enumeration

`DelayReasonCode`

## Fields

| Name |
|  --- |
| `CounterpartyNotInstructing` |
| `CounterpartyNotResponding` |
| `CounterpartyDoesNotAgreeToDeliveryDetails` |
| `CounterpartyNotAccepting` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

DelayReasonCode delayReasonCode = DelayReasonCode.CounterpartyDoesNotAgreeToDeliveryDetails;
```

