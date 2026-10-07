
# Securities Transfer Counterparty Bic

Counterparty with BIC (Bank Identifier Code) based identification for securities transfer.

*This model accepts additional fields of type object.*

## Structure

`SecuritiesTransferCounterpartyBic`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | Type of the counterparty.<br><br>**Default**: `"BIC"` |
| `Id` | `string` | Required | Business Identifier Code (also known as SWIFT-BIC, BIC, SWIFT ID or SWIFT code) [ISO 9362](https://en.wikipedia.org/wiki/ISO_9362).<br><br>**Constraints**: *Pattern*: `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` |
| `AccountNumber` | `string` | Optional | The account number is composed of valid Swift charset with a max length of 28 if provided. The account number helps other brokers identify the owner of the assets.<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]{0,28}$` |
| `Name` | `string` | Optional | The name is going to be split into 4 lines of 35 characters, the split is space based. This means that even if the name is exactly 140 of length, we may drop out the last parts if they don't fit into 4x35.<br><br>**Constraints**: *Pattern*: `^[0-9A-Za-z+?/\-:()\.,' ]{0,140}$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

SecuritiesTransferCounterpartyBic securitiesTransferCounterpartyBic = new SecuritiesTransferCounterpartyBic
{
    Type = "BIC",
    Id = "id2",
    AccountNumber = "account_number2",
    Name = "name2",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

