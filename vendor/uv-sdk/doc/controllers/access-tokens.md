# Access Tokens

```csharp
AccessTokensApi accessTokensApi = client.AccessTokensApi;
```

## Class Name

`AccessTokensApi`


# Issue Token

Get an access token to use with the API with specified scopes.

You should _always_ scope your access tokens. You get one for read-access and separate ones for updating, creating or deleting resources.

:information_source: **Note** This endpoint does not require authentication.

```csharp
IssueTokenAsync(
    Guid upvestClientId,
    Guid clientId,
    string clientSecret,
    string grantType,
    string scope,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `clientId` | `Guid` | Form, Required | Client ID given during onboarding. |
| `clientSecret` | `string` | Form, Required | Client Secret given during onboarding.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `grantType` | `string` | Form, Required | This must always be `client_credentials`. |
| `scope` | `string` | Form, Required | List of space delimited scopes to request for this access token.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Response Type

**200**: Access token successfully generated.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AuthAccessToken](../../doc/models/auth-access-token.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid clientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
string clientSecret = "WHIW1yic-ouct3sceh";
string grantType = "client_credentials";
string scope = "users:read";
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<AuthAccessToken> result = await accessTokensApi.IssueTokenAsync(
        upvestClientId,
        clientId,
        clientSecret,
        grantType,
        scope,
        upvestApiVersion
    );
}
catch (ApiException e)
{
    Console.WriteLine(e.Message);
    if (e is ErrorException)
    {
       // TODO: Handle ErrorException exception here
    }
}
```

## Example Response *(as JSON)*

```json
{
  "access_token": "token.signature",
  "expires_in": 1800,
  "token_type": "bearer",
  "scope": "users:read"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

