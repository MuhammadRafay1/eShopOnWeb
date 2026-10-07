# Credit Fundings

```csharp
CreditFundingsApi creditFundingsApi = client.CreditFundingsApi;
```

## Class Name

`CreditFundingsApi`

## Methods

* [Retrieve Funding Details](../../doc/controllers/credit-fundings.md#retrieve-funding-details)
* [List Credit Fundings](../../doc/controllers/credit-fundings.md#list-credit-fundings)
* [Retrieve Credit Funding](../../doc/controllers/credit-fundings.md#retrieve-credit-funding)


# Retrieve Funding Details

Returns the virtual bank account details needed to fund an account group via SEPA Credit Transfer. Clients should present these details to end users so they can initiate the bank transfer.

```csharp
RetrieveFundingDetailsAsync(
    Guid accountGroupId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    int? limit = 100,
    int? offset = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountGroupId` | `Guid` | Template, Required | The unique identifier of the account group. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |

## Requires scope

### oauth-client-credentials

`payments:admin`, `payments:read`

## Response Type

**200**: Funding Details

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountGroupsPaymentsCreditFundingDetailsResponse](../../doc/models/account-groups-payments-credit-funding-details-response.md).

## Example Usage

```csharp
Guid accountGroupId = new Guid("00002436-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
int? limit = 100;
try
{
    ApiResponse<AccountGroupsPaymentsCreditFundingDetailsResponse> result = await creditFundingsApi.RetrieveFundingDetailsAsync(
        accountGroupId,
        upvestClientId,
        upvestApiVersion,
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
      "virtual_bank_account_id": "4e127ac5-dae3-487e-b76b-fef7225a8b80",
      "name": "Max's Virtual Bank Account",
      "currency": "EUR",
      "owner": {
        "name": "Max Musterman"
      },
      "identification": {
        "swift": {
          "iban": "DE02100500000054540402",
          "bic": "DEUTDEFFXXX"
        }
      }
    },
    {
      "virtual_bank_account_id": "a38470db-f54c-4514-ad71-6904a5c2744f",
      "name": "Max's Virtual Bank Account",
      "currency": "EUR",
      "owner": {
        "name": "Max Musterman"
      },
      "identification": {
        "swift": {
          "iban": "DE02100500000054540403",
          "bic": "DEUTDEFFXXX"
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Credit Fundings

Returns a paginated list of credit fundings for the account group specified by its ID. A credit funding represents an incoming SEPA Credit Transfer that has been received and applied to the account group.

```csharp
ListCreditFundingsAsync(
    Guid accountGroupId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort9? sort = Models.Sort9.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? limit = 100,
    int? offset = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountGroupId` | `Guid` | Template, Required | The unique identifier of the account group. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort9?`](../../doc/models/sort-9.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort9.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |

## Requires scope

### oauth-client-credentials

`credit_fundings:read`, `payments:admin`, `payments:read`

## Response Type

**200**: Credit Funding list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsCreditFundingsListResponse](../../doc/models/payments-credit-fundings-list-response.md).

## Example Usage

```csharp
Guid accountGroupId = new Guid("00002436-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort9? sort = Sort9.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<PaymentsCreditFundingsListResponse> result = await creditFundingsApi.ListCreditFundingsAsync(
        accountGroupId,
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
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
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "1ab4fef9-a097-4c6f-9345-647025d5fde6",
      "created_at": "2020-08-24T14:15:22Z",
      "account_group_id": "1ea60f56-b67b-61fc-829a-0242ac130003",
      "cash_amount": "200.00",
      "currency": "EUR",
      "status": "CONFIRMED",
      "remittance_information": "CF123456789",
      "counterparty": {
        "identification": {
          "name": "Max Musterman"
        },
        "account": {
          "identification": {
            "iban": "DE02100500000054540402"
          }
        }
      },
      "virtual_bank_account_id": "ab8f9b19-6a31-48d3-b201-4dee92f3da0d",
      "purpose_code": "OTHR"
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Credit Funding

Returns the credit funding specified by its ID, including its status, cash amount, and counterparty details.

```csharp
RetrieveCreditFundingAsync(
    Guid creditFundingId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `creditFundingId` | `Guid` | Template, Required | The unique identifier of the credit funding. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`credit_fundings:read`, `payments:admin`, `payments:read`

## Response Type

**200**: Credit Funding payment

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsCreditFundingsResponse](../../doc/models/payments-credit-fundings-response.md).

## Example Usage

```csharp
Guid creditFundingId = new Guid("000003a4-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PaymentsCreditFundingsResponse> result = await creditFundingsApi.RetrieveCreditFundingAsync(
        creditFundingId,
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
  "id": "1ab4fef9-a097-4c6f-9345-647025d5fde6",
  "created_at": "2020-08-24T14:15:22Z",
  "account_group_id": "1ea60f56-b67b-61fc-829a-0242ac130003",
  "cash_amount": "200.00",
  "currency": "EUR",
  "status": "CONFIRMED",
  "remittance_information": "CF123456789",
  "counterparty": {
    "identification": {
      "name": "Max Musterman"
    },
    "account": {
      "identification": {
        "iban": "DE02100500000054540402"
      }
    }
  },
  "virtual_bank_account_id": "ab8f9b19-6a31-48d3-b201-4dee92f3da0d",
  "purpose_code": "OTHR"
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

