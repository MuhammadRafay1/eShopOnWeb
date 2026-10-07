# Tax Residencies

```csharp
TaxResidenciesApi taxResidenciesApi = client.TaxResidenciesApi;
```

## Class Name

`TaxResidenciesApi`

## Methods

* [Retrieve Tax Residencies](../../doc/controllers/tax-residencies.md#retrieve-tax-residencies)
* [Set Tax Residencies](../../doc/controllers/tax-residencies.md#set-tax-residencies)


# Retrieve Tax Residencies

Returns the tax residencies currently on file for the user identified by `user_id`.

See the User tax onboarding guide ([TOL](https://docs.upvest.co/products/tol/guides/users/users_tax_onboarding) / [BYOL](https://docs.upvest.co/products/byol/guides/users/users_tax_onboarding)) for tax residency requirements.

```csharp
RetrieveTaxResidenciesAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`taxes:admin`, `taxes:read`

## Response Type

**200**: User tax residencies

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TaxResidencyRecord](../../doc/models/tax-residency-record.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<TaxResidencyRecord> result = await taxResidenciesApi.RetrieveTaxResidenciesAsync(
        userId,
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
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "status": "ACTIVE",
  "tax_residencies": [
    {
      "country": "IT",
      "tax_identifier_number": "LLLLLL99L99L999L"
    },
    {
      "country": "FR",
      "missing_tin_reason": "COUNTRY_HAS_NO_TIN"
    },
    {
      "country": "DE",
      "tax_identifier_number": "12345678901"
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
| 405 | Method Not Allowed. The requested method is not allowed on the requested resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Set Tax Residencies

Records the tax residencies for the user identified by `user_id`, replacing any previously submitted set.

See the User tax onboarding guide ([TOL](https://docs.upvest.co/products/tol/guides/users/users_tax_onboarding) / [BYOL](https://docs.upvest.co/products/byol/guides/users/users_tax_onboarding)) for tax residency requirements.

```csharp
SetTaxResidenciesAsync(
    Guid userId,
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.TaxResidenciesSetRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`TaxResidenciesSetRequest`](../../doc/models/tax-residencies-set-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**200**: User tax residencies

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TaxResidencyRecord](../../doc/models/tax-residency-record.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
TaxResidenciesSetRequest body = new TaxResidenciesSetRequest
{
    TaxResidencies = new List<TaxResidenciesSetRequestTaxResidencies>
    {
        TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
            new WithTaxIdentifierNumber
            {
                Country = Country.De,
                TaxIdentifierNumber = "12345678901",
            }
        ),
    },
};

try
{
    ApiResponse<TaxResidencyRecord> result = await taxResidenciesApi.SetTaxResidenciesAsync(
        userId,
        upvestClientId,
        idempotencyKey,
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
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "tax_residencies": [
    {
      "country": "AT",
      "missing_tin_reason": "TIN_NOT_YET_ASSIGNED"
    },
    {
      "country": "DE",
      "tax_identifier_number": "12345678901"
    }
  ],
  "status": "ACTIVE"
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

