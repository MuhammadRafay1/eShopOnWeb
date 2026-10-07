
# Error Exception

## Structure

`ErrorException`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Type` | `string` | Required | URL to a document describing the error condition. |
| `Status` | `int` | Required | Transmission of the HTTP status code so that all information can be found in one place, but also to correct changes in the status code due to the use of proxy servers. |
| `Title` | `string` | Optional | A short, human-readable title for the general error type; the title should not change for given types. |
| `Detail` | `string` | Optional | A human-readable description of the specific error. |
| `Instance` | `string` | Optional | This optional key may be present, with a unique URI for the specific error; this will often point to an error log for that specific response. |
| `RequestId` | `string` | Optional | Correlation ID for the original request. |

## Example

```csharp
try
{
    // make the API call
}
catch (ApiException e)
{
    if (e is ErrorException)
    {
        // TODO: Handle ErrorException
        Console.WriteLine(e.Message);
    }
}
```

