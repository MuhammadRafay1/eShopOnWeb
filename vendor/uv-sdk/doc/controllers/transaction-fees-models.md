# Transaction Fees Models

```csharp
TransactionFeesModelsApi transactionFeesModelsApi = client.TransactionFeesModelsApi;
```

## Class Name

`TransactionFeesModelsApi`

## Methods

* [List Transaction Fee Models](../../doc/controllers/transaction-fees-models.md#list-transaction-fee-models)
* [Create Transaction Fee Model](../../doc/controllers/transaction-fees-models.md#create-transaction-fee-model)
* [Retrieve Transaction Fee Model](../../doc/controllers/transaction-fees-models.md#retrieve-transaction-fee-model)


# List Transaction Fee Models

Returns a paginated list of the transaction fee models created for your client account.

Use the `offset` and `limit` query parameters to page through results; `meta.total_count` gives the total number of matching fee models.

See the Transaction fees guide ([TOL](https://docs.upvest.co/products/tol/guides/fees/fees_transaction_fees_overview) / [BYOL](https://docs.upvest.co/products/byol/guides/fees/fees_transaction_fees_overview)) for fee model concepts.

```csharp
ListTransactionFeeModelsAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort1? sort = Models.Sort1.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort1?`](../../doc/models/sort-1.md) | Query, Optional | The field to sort the results by, either `created_at` or `updated_at`.<br><br>**Default**: `Sort1.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`transaction_fees:admin`, `transaction_fees:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TransactionFeeModelsListResponse](../../doc/models/transaction-fee-models-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort1? sort = Sort1.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<TransactionFeeModelsListResponse> result = await transactionFeesModelsApi.ListTransactionFeeModelsAsync(
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
    "count": 5,
    "total_count": 5,
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "label": "absolute fee amount - flat fee model (only 1 tier specified)",
      "currency": "EUR",
      "charge_method": "CHARGED_BY_CLIENT",
      "value_type": "ABSOLUTE",
      "application_type": "VOLUME",
      "base_amount_scope": "ORDER",
      "tiers": [
        {
          "tier_id": "0",
          "base_amount_from": "0",
          "fee_amount": "10"
        }
      ]
    },
    {
      "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "label": "absolute fee amount - tiered fee model",
      "currency": "EUR",
      "charge_method": "CHARGED_BY_CLIENT",
      "value_type": "ABSOLUTE",
      "application_type": "VOLUME",
      "base_amount_scope": "ORDER",
      "tiers": [
        {
          "tier_id": "0",
          "base_amount_from": "0",
          "fee_amount": "10"
        },
        {
          "tier_id": "1",
          "base_amount_from": "2000",
          "fee_amount": "12"
        },
        {
          "tier_id": "2",
          "base_amount_from": "4000",
          "fee_amount": "14"
        }
      ]
    },
    {
      "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "label": "relative fee (bps) - flat fee model (only 1 tier specified)",
      "currency": "EUR",
      "charge_method": "CHARGED_BY_CLIENT",
      "value_type": "RELATIVE",
      "application_type": "VOLUME",
      "base_amount_scope": "ORDER",
      "tiers": [
        {
          "tier_id": "0",
          "base_amount_from": "0",
          "fee_bps": "100"
        }
      ]
    },
    {
      "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "label": "relative fee (bps) - tiered fee model with lower/upper bound",
      "currency": "EUR",
      "charge_method": "CHARGED_BY_CLIENT",
      "value_type": "RELATIVE",
      "application_type": "VOLUME",
      "base_amount_scope": "ORDER",
      "tiers": [
        {
          "tier_id": "0",
          "base_amount_from": "0",
          "fee_bps": "10",
          "min_fee_amount": "10",
          "max_fee_amount": "100"
        },
        {
          "tier_id": "1",
          "base_amount_from": "2000",
          "fee_bps": "12",
          "min_fee_amount": "100",
          "max_fee_amount": "1000"
        },
        {
          "tier_id": "2",
          "base_amount_from": "4000",
          "fee_bps": "14",
          "min_fee_amount": "1000",
          "max_fee_amount": "1111"
        }
      ]
    },
    {
      "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "label": "relative fee (bps) with a minimum: for orders less than 10.000 1 EUR+1%, for orders more than 10.000 101 EUR+0,5%",
      "currency": "EUR",
      "charge_method": "CHARGED_BY_CLIENT",
      "value_type": "RELATIVE",
      "application_type": "VOLUME",
      "base_amount_scope": "ORDER",
      "tiers": [
        {
          "tier_id": "0",
          "base_amount_from": "0",
          "fee_bps": "0",
          "min_fee_amount": "1"
        },
        {
          "tier_id": "1",
          "base_amount_from": "0.01",
          "fee_bps": "100"
        },
        {
          "tier_id": "2",
          "base_amount_from": "10000",
          "fee_bps": "0",
          "min_fee_amount": "100"
        },
        {
          "tier_id": "3",
          "base_amount_from": "10000.01",
          "fee_bps": "50"
        }
      ]
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
| 409 | Conflict. An operation is not available for the current state of the resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create Transaction Fee Model

Creates a transaction fee model that defines how per-order transaction fees are calculated — either as fixed cash amounts (`ABSOLUTE`) or in basis points of the order value (`RELATIVE`), with one or more tiers based on the order's cash amount.

Fee models are immutable once created; to change a fee structure, create a new model. Apply a model to an order by referencing its ID in the `fee_configuration` object of the order request.

See the Building transaction fees guide ([TOL](https://docs.upvest.co/products/tol/guides/fees/fees_transaction_fees_building) / [BYOL](https://docs.upvest.co/products/byol/guides/fees/fees_transaction_fees_building)) for implementation details.

```csharp
CreateTransactionFeeModelAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.FeeConfigurationCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`FeeConfigurationCreateRequest`](../../doc/models/fee-configuration-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`transaction_fees:admin`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.FeeConfiguration](../../doc/models/fee-configuration.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
FeeConfigurationCreateRequest body = new FeeConfigurationCreateRequest
{
    Label = "transaction fee buy - new year promotion",
    Currency = Currency.Eur,
    ChargeMethod = "CHARGED_BY_CLIENT",
    ValueType = ValueType.Absolute,
    ApplicationType = "VOLUME",
    BaseAmountScope = "ORDER",
    Tiers = new List<FeeConfigurationCreateRequestTiers>
    {
        FeeConfigurationCreateRequestTiers.FromAbsoluteTransactionFeeTier(
            new AbsoluteTransactionFeeTier
            {
                TierId = "0",
                BaseAmountFrom = "0",
                FeeAmount = "10",
            }
        ),
        FeeConfigurationCreateRequestTiers.FromAbsoluteTransactionFeeTier(
            new AbsoluteTransactionFeeTier
            {
                TierId = "1",
                BaseAmountFrom = "2000",
                FeeAmount = "12",
            }
        ),
        FeeConfigurationCreateRequestTiers.FromAbsoluteTransactionFeeTier(
            new AbsoluteTransactionFeeTier
            {
                TierId = "2",
                BaseAmountFrom = "4000",
                FeeAmount = "14",
            }
        ),
    },
};

try
{
    ApiResponse<FeeConfiguration> result = await transactionFeesModelsApi.CreateTransactionFeeModelAsync(
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
  "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "label": "transaction fee buy - new year promotion",
  "currency": "EUR",
  "charge_method": "CHARGED_BY_CLIENT",
  "value_type": "ABSOLUTE",
  "application_type": "VOLUME",
  "base_amount_scope": "ORDER",
  "tiers": [
    {
      "tier_id": "0",
      "base_amount_from": "0",
      "fee_amount": "10"
    },
    {
      "tier_id": "1",
      "base_amount_from": "2000",
      "fee_amount": "12"
    },
    {
      "tier_id": "2",
      "base_amount_from": "4000",
      "fee_amount": "14"
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
| 409 | Conflict. An operation is not available for the current state of the resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Transaction Fee Model

Returns the transaction fee model with the given ID, including its currency, charge method, value type, and tier structure.

See the Transaction fees guide ([TOL](https://docs.upvest.co/products/tol/guides/fees/fees_transaction_fees_overview) / [BYOL](https://docs.upvest.co/products/byol/guides/fees/fees_transaction_fees_overview)) for fee model concepts.

```csharp
RetrieveTransactionFeeModelAsync(
    Guid transactionFeeModelId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `transactionFeeModelId` | `Guid` | Template, Required | The unique identifier of the transaction fee model. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`transaction_fees:admin`, `transaction_fees:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.FeeConfiguration](../../doc/models/fee-configuration.md).

## Example Usage

```csharp
Guid transactionFeeModelId = new Guid("00001e8c-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<FeeConfiguration> result = await transactionFeesModelsApi.RetrieveTransactionFeeModelAsync(
        transactionFeeModelId,
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
  "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "label": "transaction fee buy - new year promotion",
  "currency": "EUR",
  "charge_method": "CHARGED_BY_CLIENT",
  "value_type": "ABSOLUTE",
  "application_type": "VOLUME",
  "base_amount_scope": "ORDER",
  "tiers": [
    {
      "tier_id": "0",
      "base_amount_from": "0",
      "fee_amount": "10"
    },
    {
      "tier_id": "1",
      "base_amount_from": "2000",
      "fee_amount": "12"
    },
    {
      "tier_id": "2",
      "base_amount_from": "4000",
      "fee_amount": "14"
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
| 409 | Conflict. An operation is not available for the current state of the resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

