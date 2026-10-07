
# User Byol Data Change Request

Request payload for changing the data of a BYOL user.

*This model accepts additional fields of type object.*

## Structure

`UserByolDataChangeRequest`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `FirstName` | `string` | Optional | First name of the user.<br><br>**Constraints**: *Minimum Length*: `2`, *Maximum Length*: `100` |
| `LastName` | `string` | Optional | Last name of the user.<br><br>**Constraints**: *Minimum Length*: `2`, *Maximum Length*: `100` |
| `Email` | `string` | Optional | Email of the user. Must be a valid email address.<br><br>**Constraints**: *Maximum Length*: `100` |
| `Salutation` | [`Salutation?`](../../doc/models/salutation.md) | Optional | Salutation of the user used in reports and statements.<br><br>* (empty string) -<br>* SALUTATION_MALE -<br>* SALUTATION_FEMALE -<br>* SALUTATION_FEMALE_MARRIED -<br>* SALUTATION_DIVERSE - |
| `Title` | [`Title?`](../../doc/models/title.md) | Optional | Title of the user used in reports and statements.<br><br>* (empty string) -<br>* DR - Doctor<br>* PROF - Professor<br>* PROF_DR -<br>* DIPL_ING - Graduate engineer (Diplom-Ingenieur)<br>* MAGISTER - |
| `BirthDate` | `DateTime?` | Optional | Birth date of the user in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339) |
| `BirthCity` | `string` | Optional | **Constraints**: *Minimum Length*: `1`, *Maximum Length*: `85` |
| `BirthCountry` | [`BirthCountry?`](../../doc/models/birth-country.md) | Optional | Accepted country code. [ISO 3166-1 alpha-2 codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2). |
| `BirthName` | `string` | Optional | If applicable, birth name of the user.<br><br>**Constraints**: *Maximum Length*: `100` |
| `DeceaseDate` | `string` | Optional | The user's date of death in YYYY-MM-DD format. [RFC 3339, section 5.6](https://json-schema.org/draft/2020-12/json-schema-validation.html#RFC3339). Send an empty string to clear a decease date that was recorded in error.<br><br>**Constraints**: *Pattern*: `^$\|^\d{4}-(0[1-9]\|1[012])-(0[1-9]\|[12][0-9]\|3[01])$` |
| `Nationalities` | [`List<Nationality>`](../../doc/models/nationality.md) | Optional | Nationalities of the user. [ISO 3166 alpha-2 Codes](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2).<br><br>**Constraints**: *Minimum Items*: `1` |
| `Address` | [`Address`](../../doc/models/address.md) | Optional | Address. Must not be a P.O. box or c/o address. |
| `PostalAddress` | [`Address`](../../doc/models/address.md) | Optional | User postal address. Needs to be specified if different to the residential address, otherwise it is automatically populated. |
| `BranchId` | `Guid?` | Optional | Unique identifier of the market the user is onboarded on. Only relevant if the client is operating in different markets and the client is configured accordingly |
| `Gender` | [`Gender?`](../../doc/models/gender.md) | Optional | Gender of the user. Required for users applying for the German pension government bonus.<br><br>* (empty string) -<br>* MALE -<br>* FEMALE -<br>* DIVERSE - |
| `SocialSecurityNumber` | `string` | Optional | The user's assigned social security number (German social insurance number, e.g. `25300972S014`). Required for users applying for the German pension government bonus.<br><br>**Constraints**: *Pattern*: `^(\d{2}(0[1-9]\|[12]\d\|3[01])(0[1-9]\|1[0-2])\d{2}[A-Z]\d{3})?$` |
| `Tags` | [`List<Tag>`](../../doc/models/tag.md) | Optional | Labels applied to the user by the tenant. Omitted for users that have no tags.<br><br>* CLIENT_EMPLOYEE - The user is an employee of the tenant.<br><br>Providing this field in a data change request replaces the full set of tags; send an empty array to remove all tags.<br><br>**Constraints**: *Unique Items Required* |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Utilities;

UserByolDataChangeRequest userByolDataChangeRequest = new UserByolDataChangeRequest
{
    FirstName = "first_name2",
    LastName = "last_name0",
    Email = "email4",
    Salutation = Salutation.SalutationFemaleMarried,
    Title = Title.DiplIng,
    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
};
```

