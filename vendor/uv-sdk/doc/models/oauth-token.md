
# Oauth Token

OAuth 2 Authorization endpoint response

## Structure

`OauthToken`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `AccessToken` | `string` | Required | Access token |
| `TokenType` | `string` | Required | Type of access token |
| `ExpiresIn` | `long?` | Optional | Time in seconds before the access token expires |
| `Scope` | `string` | Optional | List of scopes granted<br>This is a space-delimited list of strings. |
| `Expiry` | `long?` | Optional | Time of token expiry as unix timestamp (UTC) |
| `RefreshToken` | `string` | Optional | Refresh token<br>Used to get a new access token when it expires. |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

OauthToken oauthToken = new OauthToken
{
    AccessToken = "access_token8",
    TokenType = "token_type8",
    ExpiresIn = 10L,
    Scope = "scope2",
    Expiry = 152L,
    RefreshToken = "refresh_token0",
};
```

