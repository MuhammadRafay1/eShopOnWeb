
# Oauth Provider Exception

OAuth 2 Authorization endpoint exception.

*This model accepts additional fields of type object.*

## Structure

`OauthProviderException`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Error` | [`OauthProviderError`](../../doc/models/oauth-provider-error.md) | Required | Gets or sets error code. |
| `ErrorDescription` | `string` | Optional | Gets or sets human-readable text providing additional information on error.<br>Used to assist the client developer in understanding the error that occurred. |
| `ErrorUri` | `string` | Optional | Gets or sets a URI identifying a human-readable web page with information about the error, used to provide the client developer with additional information about the error. |
| `AdditionalProperties` | `object this[string key]` | Optional | - |

## Example

```csharp
try
{
    // make the API call
}
catch (ApiException e)
{
    if (e is OauthProviderException)
    {
        // TODO: Handle OauthProviderException
        Console.WriteLine(e.Message);
    }
}
```

