# Business Checks

```csharp
BusinessChecksApi businessChecksApi = client.BusinessChecksApi;
```

## Class Name

`BusinessChecksApi`

## Methods

* [List Business Checks](../../doc/controllers/business-checks.md#list-business-checks)
* [Create Business Check](../../doc/controllers/business-checks.md#create-business-check)
* [Retrieve Business Check](../../doc/controllers/business-checks.md#retrieve-business-check)


# List Business Checks

Lists all checks for a business specified by ID.

```csharp
ListBusinessChecksAsync(
    Guid businessId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `businessId` | `Guid` | Template, Required | The unique identifier of the business. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`checks:admin`, `checks:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.BusinessCheckListResponse](../../doc/models/business-check-list-response.md).

## Example Usage

```csharp
Guid businessId = new Guid("0000001e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<BusinessCheckListResponse> result = await businessChecksApi.ListBusinessChecksAsync(
        businessId,
        upvestClientId,
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
  "data": [
    {
      "id": "d3f8c9a1-4b2e-4c8e-9f3e-2a5b7e6d9c1f",
      "business_id": "e7a1c2d4-5f6b-7a8e-9b0c-1d2e3f4a5b6c",
      "type": "KYB",
      "originally_confirmed_at": "2020-08-24T14:15:22Z",
      "check_confirmed_at": "2020-08-24T14:15:22Z",
      "data_download_link": "https://bucket.customer.com/por/kyb.zip",
      "document_type": "KYB_DOCUMENTS",
      "status": "PASSED"
    }
  ]
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create Business Check

Creates a new check for a business specified by ID.

```csharp
CreateBusinessCheckAsync(
    Guid businessId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateBusinessCheckBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `businessId` | `Guid` | Template, Required | The unique identifier of the business. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateBusinessCheckBody`](../../doc/models/containers/create-business-check-body.md) | Body, Optional | This is a container for one-of cases. |

## Requires scope

### oauth-client-credentials

`checks:admin`

## Response Type

**202**: The request has been successfully accepted.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.BusinessCheckCreateResponse](../../doc/models/business-check-create-response.md).

## Example Usage

```csharp
Guid businessId = new Guid("0000001e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
CreateBusinessCheckBody body = CreateBusinessCheckBody.FromBusinessCheckKnowYourBusinessCreateRequest(
    new BusinessCheckKnowYourBusinessCreateRequest
    {
        Type = "KYB",
        OriginallyConfirmedAt = DateTime.ParseExact("2020-08-24T14:15:22Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        CheckConfirmedAt = DateTime.ParseExact("2024-08-24T14:15:22Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        DataDownloadLink = "https://bucket.customer.com/por/kyb.zip",
    }
);

try
{
    ApiResponse<BusinessCheckCreateResponse> result = await businessChecksApi.CreateBusinessCheckAsync(
        businessId,
        upvestClientId,
        null,
        body
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
  "id": "e13e9d81-7f43-492d-a02a-440edced389a"
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


# Retrieve Business Check

Retrieves a check for a business specified by its ID.

```csharp
RetrieveBusinessCheckAsync(
    Guid businessId,
    Guid checkId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `businessId` | `Guid` | Template, Required | The unique identifier of the business. Universally Unique Identifier (UUID). |
| `checkId` | `Guid` | Template, Required | The unique identifier of the user check. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`checks:admin`, `checks:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type RetrieveBusinessCheckResponse.

## Example Usage

```csharp
Guid businessId = new Guid("0000001e-0000-0000-0000-000000000000");
Guid checkId = new Guid("000002b6-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<RetrieveBusinessCheckResponse> result = await businessChecksApi.RetrieveBusinessCheckAsync(
        businessId,
        checkId,
        upvestClientId,
        upvestApiVersion
    );
    result.Data.Match<VoidType>(
        businessCheckKyb: businessCheckKyb =>
        {
            // TODO: handle businessCheckKyb here
            Console.WriteLine(businessCheckKyb);
            return null;
        },
        businessCheckBusinessSelfAssessment: businessCheckBusinessSelfAssessment =>
        {
            // TODO: handle businessCheckBusinessSelfAssessment here
            Console.WriteLine(businessCheckBusinessSelfAssessment);
            return null;
        });
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

## Example Response

```
{
  "id": "f3b2d9c6-8e4f-4a7b-9c2d-7a5e1f3c4d6e",
  "business_id": "d4b5f9a7-8e2c-4c1d-9a3f-7b6e2d3c4f5a",
  "type": "KYB",
  "originally_confirmed_at": "2020-08-24T14:15:22Z",
  "check_confirmed_at": "2024-08-24T14:15:22Z",
  "data_download_link": "https://bucket.customer.com/por/kyb.zip",
  "document_type": "KYB_DOCUMENTS",
  "status": "IN_PROGRESS"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

