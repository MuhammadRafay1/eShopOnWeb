# Tax Exemptions

```csharp
TaxExemptionsApi taxExemptionsApi = client.TaxExemptionsApi;
```

## Class Name

`TaxExemptionsApi`

## Methods

* [Create Tax Exemption](../../doc/controllers/tax-exemptions.md#create-tax-exemption)
* [Retrieve Tax Exemption by Id](../../doc/controllers/tax-exemptions.md#retrieve-tax-exemption-by-id)
* [Update Tax Exemption](../../doc/controllers/tax-exemptions.md#update-tax-exemption)
* [Delete Tax Exemption](../../doc/controllers/tax-exemptions.md#delete-tax-exemption)
* [Retrieve Tax Exemptions for User](../../doc/controllers/tax-exemptions.md#retrieve-tax-exemptions-for-user)


# Create Tax Exemption

Creates a tax exemption.

```csharp
CreateTaxExemptionAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.TaxesTaxExemptionsCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`TaxesTaxExemptionsCreateRequest`](../../doc/models/taxes-tax-exemptions-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**202**: Creation request accepted.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TaxExemptionsCreateResponse](../../doc/models/tax-exemptions-create-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
TaxesTaxExemptionsCreateRequest body = new TaxesTaxExemptionsCreateRequest
{
    UserIds = new List<Guid>
    {
        new Guid("70fd317b-81e1-4f21-9f7e-3b5cb4dfe686"),
    },
    TaxExemptionDetails = new TaxExemptionRequestGermanTaxExemptionDetails
    {
        TaxExemptionAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
        {
            Amount = "1000",
            Currency = Currency.Eur,
        },
        TaxExemptionType = TaxExemptionType.Single,
    },
    Country = "DE",
    ValidToDate = DateTime.Parse("2024-12-31"),
};

try
{
    ApiResponse<TaxExemptionsCreateResponse> result = await taxExemptionsApi.CreateTaxExemptionAsync(
        upvestClientId,
        idempotencyKey,
        upvestApiVersion,
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
  "id": "f1a57a04-1a89-4dab-ae3a-ff9b2a9377c1"
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


# Retrieve Tax Exemption by Id

Returns the tax exemption specified by its ID

```csharp
RetrieveTaxExemptionByIdAsync(
    Guid taxExemptionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `taxExemptionId` | `Guid` | Template, Required | The unique identifier of the tax exemption. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.WebhookTaxExemptionCreatedTaxExemption](../../doc/models/webhook-tax-exemption-created-tax-exemption.md).

## Example Usage

```csharp
Guid taxExemptionId = new Guid("00001326-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<WebhookTaxExemptionCreatedTaxExemption> result = await taxExemptionsApi.RetrieveTaxExemptionByIdAsync(
        taxExemptionId,
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
  "id": "f1a57a04-1a89-4dab-ae3a-ff9b2a9377c1",
  "created_at": "2024-01-01T10:33:43Z",
  "updated_at": "2024-01-01T10:33:43Z",
  "status": "ACTIVE",
  "user_ids": [
    "70821d79-366f-4873-804b-14857d690496"
  ],
  "country": "DE",
  "valid_from_date": "2024-01-01",
  "valid_to_date": "2024-12-31",
  "tax_exemption_details": {
    "tax_exemption_type": "SINGLE",
    "tax_exemption_amount": {
      "amount": "1000.00",
      "currency": "EUR"
    },
    "utilized_amount": {
      "amount": "0.00",
      "currency": "EUR"
    },
    "remaining_amount": {
      "amount": "1000.00",
      "currency": "EUR"
    }
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


# Update Tax Exemption

Updates a tax exemption specified by its ID.

```csharp
UpdateTaxExemptionAsync(
    Guid taxExemptionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.TaxesTaxExemptionsUpdateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `taxExemptionId` | `Guid` | Template, Required | The unique identifier of the tax exemption. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`TaxesTaxExemptionsUpdateRequest`](../../doc/models/taxes-tax-exemptions-update-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**202**: The tax exemption update is submitted.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TaxExemptionsUpdateResponse](../../doc/models/tax-exemptions-update-response.md).

## Example Usage

```csharp
Guid taxExemptionId = new Guid("00001326-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
TaxesTaxExemptionsUpdateRequest body = new TaxesTaxExemptionsUpdateRequest
{
    UserIds = new List<Guid>
    {
        new Guid("70fd317b-81e1-4f21-9f7e-3b5cb4dfe686"),
    },
    TaxExemptionDetails = new TaxExemptionRequestGermanTaxExemptionDetails
    {
        TaxExemptionAmount = new TaxExemptionCreateRequestTaxExemptionDetailsAmount
        {
            Amount = "955",
            Currency = Currency.Eur,
        },
        TaxExemptionType = TaxExemptionType.Single,
    },
    Country = "DE",
    ValidToDate = DateTime.Parse("2024-12-31"),
};

try
{
    ApiResponse<TaxExemptionsUpdateResponse> result = await taxExemptionsApi.UpdateTaxExemptionAsync(
        taxExemptionId,
        upvestClientId,
        upvestApiVersion,
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
  "id": "f1a57a04-1a89-4dab-ae3a-ff9b2a9377c1"
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


# Delete Tax Exemption

Deletes a tax exemption specified by its ID.

```csharp
DeleteTaxExemptionAsync(
    Guid taxExemptionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `taxExemptionId` | `Guid` | Template, Required | The unique identifier of the tax exemption. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid taxExemptionId = new Guid("00001326-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await taxExemptionsApi.DeleteTaxExemptionAsync(
        taxExemptionId,
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


# Retrieve Tax Exemptions for User

Returns the tax exemptions of the user specified by ID.

```csharp
RetrieveTaxExemptionsForUserAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort9? sort = Models.Sort9.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort9?`](../../doc/models/sort-9.md) | Query, Optional | Sort the result by `created_at`.<br><br>**Default**: `Sort9.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`taxes:admin`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TaxExemptionRetrieveForUserResponse](../../doc/models/tax-exemption-retrieve-for-user-response.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort9? sort = Sort9.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<TaxExemptionRetrieveForUserResponse> result = await taxExemptionsApi.RetrieveTaxExemptionsForUserAsync(
        userId,
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit
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
  "meta": {
    "offset": 0,
    "limit": 100,
    "count": 1,
    "total_count": 1,
    "sort": "created_at",
    "order": "ASC"
  },
  "data": [
    {
      "id": "f1a57a04-1a89-4dab-ae3a-ff9b2a9377c1",
      "created_at": "2024-01-01T10:33:43Z",
      "updated_at": "2024-01-01T10:33:43Z",
      "status": "ACTIVE",
      "user_ids": [
        "70821d79-366f-4873-804b-14857d690496"
      ],
      "country": "DE",
      "valid_from_date": "2024-01-01",
      "valid_to_date": "2024-12-31",
      "tax_exemption_details": {
        "tax_exemption_type": "SINGLE",
        "tax_exemption_amount": {
          "amount": "1000.00",
          "currency": "EUR"
        },
        "utilized_amount": {
          "amount": "0.00",
          "currency": "EUR"
        },
        "remaining_amount": {
          "amount": "1000.00",
          "currency": "EUR"
        }
      }
    }
  ]
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 405 | Method Not Allowed. The requested method is not allowed on the requested resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

