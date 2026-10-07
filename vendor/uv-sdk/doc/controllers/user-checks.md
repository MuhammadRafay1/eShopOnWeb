# User Checks

```csharp
UserChecksApi userChecksApi = client.UserChecksApi;
```

## Class Name

`UserChecksApi`

## Methods

* [List User Checks](../../doc/controllers/user-checks.md#list-user-checks)
* [Create User Check](../../doc/controllers/user-checks.md#create-user-check)
* [Retrieve User Check](../../doc/controllers/user-checks.md#retrieve-user-check)


# List User Checks

Lists all checks for a user specified by ID.

```csharp
ListUserChecksAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    bool? listHistory = false,
    Models.Type6? type = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `listHistory` | `bool?` | Query, Optional | If true, return the full history of checks (all records) instead of only the latest check per type.<br><br>**Default**: `false` |
| `type` | [`Type6?`](../../doc/models/type-6.md) | Query, Optional | Filter the returned checks to a single allowed check type (e.g. US_WITHHOLDING_TAX_STATUS). |

## Requires scope

### oauth-client-credentials

`checks:admin`, `checks:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.UserCheckListResponse](../../doc/models/user-check-list-response.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
bool? listHistory = false;
try
{
    ApiResponse<UserCheckListResponse> result = await userChecksApi.ListUserChecksAsync(
        userId,
        upvestClientId,
        upvestApiVersion,
        listHistory
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


# Create User Check

Creates a new check for a user specified by ID.

```csharp
CreateUserCheckAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateUserCheckBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateUserCheckBody`](../../doc/models/containers/create-user-check-body.md) | Body, Optional | This is a container for one-of cases. |

## Requires scope

### oauth-client-credentials

`checks:admin`

## Response Type

**202**: The request has been successfully accepted.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.UserCheckCreateResponse](../../doc/models/user-check-create-response.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
CreateUserCheckBody body = CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(
    new UserCheckKnowYourCustomerCreateRequest
    {
        Type = "KYC",
        CheckConfirmedAt = DateTime.ParseExact("2019-08-24T14:15:22Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
            provider: CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        DataDownloadLink = "https://bucket.customer.com/ident/user3.zip",
        DocumentType = DocumentType3.IdCard,
        Provider = "KYC provider",
        Method = Method.VideoId,
        DocumentExpirationDate = DateTime.Parse("2030-01-01"),
        Nationality = "DE",
        ConfirmedAddress = new Address
        {
            AddressLine1 = "Rosenweg 221",
            Postcode = "45678",
            Country = Country.De,
            City = "Berlin",
            AddressLine2 = "apt. 33",
            State = "BE",
        },
    }
);

try
{
    ApiResponse<UserCheckCreateResponse> result = await userChecksApi.CreateUserCheckAsync(
        userId,
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


# Retrieve User Check

Retrieves a check for a user specified by its ID.

```csharp
RetrieveUserCheckAsync(
    Guid userId,
    Guid checkId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `checkId` | `Guid` | Template, Required | The unique identifier of the user check. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`checks:admin`, `checks:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type RetrieveUserCheckResponse.

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid checkId = new Guid("000002b6-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<RetrieveUserCheckResponse> result = await userChecksApi.RetrieveUserCheckAsync(
        userId,
        checkId,
        upvestClientId,
        upvestApiVersion
    );
    result.Data.Match<VoidType>(
        userCheckKnowYourCustomer: userCheckKnowYourCustomer =>
        {
            // TODO: handle userCheckKnowYourCustomer here
            Console.WriteLine(userCheckKnowYourCustomer);
            return null;
        },
        userCheckProofOfResidency: userCheckProofOfResidency =>
        {
            // TODO: handle userCheckProofOfResidency here
            Console.WriteLine(userCheckProofOfResidency);
            return null;
        },
        userCheckInstrumentFit: userCheckInstrumentFit =>
        {
            // TODO: handle userCheckInstrumentFit here
            Console.WriteLine(userCheckInstrumentFit);
            return null;
        },
        userCheckCompliance: userCheckCompliance =>
        {
            // TODO: handle userCheckCompliance here
            Console.WriteLine(userCheckCompliance);
            return null;
        },
        userCheckGuardian: userCheckGuardian =>
        {
            // TODO: handle userCheckGuardian here
            Console.WriteLine(userCheckGuardian);
            return null;
        },
        userCheckUsWithholdingTaxStatus: userCheckUsWithholdingTaxStatus =>
        {
            // TODO: handle userCheckUsWithholdingTaxStatus here
            Console.WriteLine(userCheckUsWithholdingTaxStatus);
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

