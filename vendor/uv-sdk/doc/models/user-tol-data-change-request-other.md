
# User Tol Data Change Request Other

*This model accepts additional fields of type object.*

## Structure

`UserTolDataChangeRequestOther`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Email` | `string` | Optional | Email of the user. Must be a valid email address.<br><br>**Constraints**: *Maximum Length*: `100` |
| `Salutation` | [`Salutation?`](../../doc/models/salutation.md) | Optional | Salutation of the user used in reports and statements.<br><br>* (empty string) -<br>* SALUTATION_MALE -<br>* SALUTATION_FEMALE -<br>* SALUTATION_FEMALE_MARRIED -<br>* SALUTATION_DIVERSE - |
| `Title` | [`Title?`](../../doc/models/title.md) | Optional | Title of the user used in reports and statements.<br><br>* (empty string) -<br>* DR - Doctor<br>* PROF - Professor<br>* PROF_DR -<br>* DIPL_ING - Graduate engineer (Diplom-Ingenieur)<br>* MAGISTER - |
| `PhoneNumber` | `string` | Optional | Phone number of the user. [Phone number E.164 format](https://en.wikipedia.org/wiki/E.164).<br><br>**Constraints**: *Pattern*: `^([0-9]{8,15})?$` |
| `BirthName` | `string` | Optional | If applicable, birth name of the user.<br><br>**Constraints**: *Maximum Length*: `100` |
| `PostalAddress` | [`Address`](../../doc/models/address.md) | Optional | User postal address. Needs to be specified if different to the residential address, otherwise it is automatically populated. |
| `BranchId` | `Guid?` | Optional | Unique identifier of the market the user is onboarded on. Only relevant if the client is operating in different markets and the client is configured accordingly |
| `Gender` | [`Gender?`](../../doc/models/gender.md) | Optional | Gender of the user. Required for users applying for the German pension government bonus.<br><br>* (empty string) -<br>* MALE -<br>* FEMALE -<br>* DIVERSE - |
| `SocialSecurityNumber` | `string` | Optional | The user's assigned social security number (German social insurance number, e.g. `25300972S014`). Required for users applying for the German pension government bonus.<br><br>**Constraints**: *Pattern*: `^(\d{2}(0[1-9]\|[12]\d\|3[01])(0[1-9]\|1[0-2])\d{2}[A-Z]\d{3})?$` |
| `Tags` | [`List<Tag>`](../../doc/models/tag.md) | Optional | Labels applied to the user by the tenant. Omitted for users that have no tags.<br><br>* CLIENT_EMPLOYEE - The user is an employee of the tenant.<br><br>Providing this field in a data change request replaces the full set of tags; send an empty array to remove all tags.<br><br>**Constraints**: *Unique Items Required* |
| `DeceaseDate` | `string` | Optional | The user's date of death in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339). Send an empty string to clear a decease date that was recorded in error.<br><br>**Constraints**: *Pattern*: `^$\|^\d{4}-(0[1-9]\|1[012])-(0[1-9]\|[12][0-9]\|3[01])$` |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserTolDataChangeRequestOther userTolDataChangeRequestOther = new UserTolDataChangeRequestOther
{
    Email = "email2",
    Salutation = Salutation.SalutationMale,
    Title = Title.Dr,
    PhoneNumber = "phone_number8",
    BirthName = "birth_name4",
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

