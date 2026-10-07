
# Auth Access Token

Schema for an access token response.

## Structure

`AuthAccessToken`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccessToken` | `string` | Required | The generated access token.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `ExpiresIn` | `int` | Required | How many seconds the access token is valid for. |
| `TokenType` | `string` | Required | This is always 'bearer'.<br><br>**Default**: `"bearer"` |
| `Scope` | `string` | Required | List of space delimited scopes requested for this access token.<br><br>**Constraints**: *Maximum Length*: `1000` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

AuthAccessToken authAccessToken = new AuthAccessToken
{
    AccessToken = "access_token4",
    ExpiresIn = 216,
    TokenType = "bearer",
    Scope = "scope4",
};
```

