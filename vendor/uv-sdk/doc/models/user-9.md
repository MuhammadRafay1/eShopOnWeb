
# User 9

User details.

*This model accepts additional fields of type object.*

## Structure

`User9`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `FirstName` | `string` | Optional | First name of the user.<br><br>**Constraints**: *Minimum Length*: `2`, *Maximum Length*: `100` |
| `LastName` | `string` | Optional | Last name of the user.<br><br>**Constraints**: *Minimum Length*: `2`, *Maximum Length*: `100` |
| `Salutation` | [`Salutation?`](../../doc/models/salutation.md) | Optional | Salutation of the user used in reports and statements.<br><br>* (empty string) -<br>* SALUTATION_MALE -<br>* SALUTATION_FEMALE -<br>* SALUTATION_FEMALE_MARRIED -<br>* SALUTATION_DIVERSE - |
| `Title` | [`Title10?`](../../doc/models/title-10.md) | Optional | Addressed user's title is used in reports and statements.<br><br>* (empty string) -<br>* DR - Doctor<br>* PROF - Professor<br>* PROF_DR -<br>* DIPL_ING - Graduate engineer (Diplom-Ingenieur)<br>* MAGISTER - |
| `Address` | [`Address30`](../../doc/models/address-30.md) | Optional | User residential address. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

User9 user9 = new User9
{
    FirstName = "first_name8",
    LastName = "last_name6",
    Salutation = Salutation.SalutationMale,
    Title = Title10.Prof,
    Address = new Address30
    {
        AddressLine1 = "address_line10",
        AddressLine2 = "address_line28",
        Postcode = "postcode0",
        Country = "country0",
        State = "state2",
        ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
    },
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

