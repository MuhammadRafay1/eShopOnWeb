# Files

All files API related paths.

```csharp
FilesApi filesApi = client.FilesApi;
```

## Class Name

`FilesApi`


# Fetching File Metadata

Retrieve a file metadata

```csharp
FetchingFileMetadataAsync(
    string folder,
    string fileName,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    string redirect = "0")
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `folder` | `string` | Template, Required | Folder containing the file. Must match the pattern `^[a-z0-9-_\.]{1,32}$`.<br><br>**Constraints**: *Pattern*: `^[a-z0-9-_\.]{1,32}$` |
| `fileName` | `string` | Template, Required | Name of the file to retrieve. Must match the pattern `^[a-z0-9-_\.]{1,255}$`.<br><br>**Constraints**: *Pattern*: `^[a-z0-9-_\.]{1,255}$` |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `redirect` | `string` | Query, Optional | To enable HTTP redirect.<br><br>**Default**: `"0"` |

## Requires scope

### oauth-client-credentials

`files:read`

## Response Type

**200**: Returns a files metadata object if a valid name and folder are provided.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.FilesMetadata](../../doc/models/files-metadata.md).

## Example Usage

```csharp
string folder = "booking_references";
string fileName = "list.txt";
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
string redirect = "0";
try
{
    ApiResponse<FilesMetadata> result = await filesApi.FetchingFileMetadataAsync(
        folder,
        fileName,
        upvestClientId,
        upvestApiVersion,
        redirect
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
  "id": "d588e071-6e02-4ae6-a2b6-a5f08499633b",
  "created_at": "2024-03-04T08:24:18Z",
  "updated_at": "2024-03-14T11:18:39Z",
  "signed_url": "https://storage.googleapis.com/upvest-tooling-datasharing-service-ia-unstable-7263/477ed9c4943c47f235712a1e80c1ef8491768f0a31ed2d81f244dfebe418e909/tats-tests-encrypted-text.txt?Expires=1710415438&GoogleAccessId=tooling-datasharing-service%40ia-unstable-7263.iam.gserviceaccount.com&Signature=cxb7JQXJVfO6ltBK%2FCWJkzoQcONakNiVsitn0nwtLDJmBj4cznBbMnT0yKtIO4v7BjhR92JmUd9wm7MurZYcuxdBPTMk%2BLTl0KN3QMEJ7%2FVjM1zSerIOxQ8OLFWCswt16Fj9%2BpwD7ZMplIrSEBGijPD48gLlZF%2FTW4yPqkLbE6sYn5Kn3K9A%2B3iF7%2BUaljQ2jLTxG9vCapp8WKjGjDyDXRtDDYY7OXJ6aDrjxDEUxO6kkOGzxMEBCALoHjD2lMpkEy5Gp1BkUbPka7hYVrbQrDM1xpBRsq%2F%2FCT%2F%2Bt196e8JP9YGr%2FxeA%2Bqt6ZTTryzGZhHlvHJFjqIqj2Ip7HioFjw%3D%3D",
  "file_name": "tats-tests-encrypted-text.txt",
  "content_length": 919,
  "checksum": "709b190a108791217f62d839f85a7a997c2039cc29a737d59fd50454b85b2956"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

