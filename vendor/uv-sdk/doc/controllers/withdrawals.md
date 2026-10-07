# Withdrawals

All withdrawals related paths

```csharp
WithdrawalsApi withdrawalsApi = client.WithdrawalsApi;
```

## Class Name

`WithdrawalsApi`

## Methods

* [Create Cash Withdrawal](../../doc/controllers/withdrawals.md#create-cash-withdrawal)
* [Retrieve Cash Withdrawal](../../doc/controllers/withdrawals.md#retrieve-cash-withdrawal)
* [Cancel Cash Withdrawal](../../doc/controllers/withdrawals.md#cancel-cash-withdrawal)
* [List Cash Withdrawals](../../doc/controllers/withdrawals.md#list-cash-withdrawals)


# Create Cash Withdrawal

Trigger a withdrawal.

**Deprecation notice:** The `user_id` field is deprecated, optional, and will be removed in a future version. For business account withdrawals, ownership is determined by the reference account linked to the business.

```csharp
CreateCashWithdrawalAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PaymentsWithdrawalCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PaymentsWithdrawalCreateRequest`](../../doc/models/payments-withdrawal-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`payments:admin`

## Response Type

**200**: Withdrawal

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsWithdrawal](../../doc/models/payments-withdrawal.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PaymentsWithdrawalCreateRequest body = new PaymentsWithdrawalCreateRequest
{
    ReferenceAccountId = new Guid("295378ec-036e-4f3f-ae5c-2be85c93e837"),
    AccountGroupId = new Guid("2596db3b-0d03-4651-9eda-970910479dfb"),
    Amount = "200.00",
    UserId = new Guid("e56c14b3-8370-4c04-bc67-7092f7ec113c"),
    Currency = Currency.Eur,
    RemittanceInformation = "Withdrawal",
    PurposeCode = "OTHR",
};

try
{
    ApiResponse<PaymentsWithdrawal> result = await withdrawalsApi.CreateCashWithdrawalAsync(
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
  "id": "6ffa6b16-2380-4e7a-88b2-ae625c8eef99",
  "created_at": "2020-08-24T14:15:22Z",
  "updated_at": "2020-08-24T14:15:22Z",
  "reference_account_id": "295378ec-036e-4f3f-ae5c-2be85c93e837",
  "account_group_id": "ac1c39e9-2101-46b8-a624-d10a9e351b6c",
  "user_id": "b668282b-cdf3-439a-bda2-3f3c9f655bb7",
  "amount": "200.00",
  "currency": "EUR",
  "remittance_information": "Withdrawal",
  "taxes": [
    {
      "type": "TOTAL",
      "amount": "38.00",
      "currency": "EUR"
    }
  ],
  "status": "NEW",
  "purpose_code": "OTHR"
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


# Retrieve Cash Withdrawal

Returns the cash withdrawal specified by its ID, including its current status, amount, and reference account.

```csharp
RetrieveCashWithdrawalAsync(
    Guid withdrawalId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `withdrawalId` | `Guid` | Template, Required | The unique identifier of the cash withdrawal. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`payments:admin`, `payments:read`

## Response Type

**200**: Withdrawal

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsWithdrawal](../../doc/models/payments-withdrawal.md).

## Example Usage

```csharp
Guid withdrawalId = new Guid("00001272-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PaymentsWithdrawal> result = await withdrawalsApi.RetrieveCashWithdrawalAsync(
        withdrawalId,
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
  "id": "6ffa6b16-2380-4e7a-88b2-ae625c8eef99",
  "created_at": "2020-08-24T14:15:22Z",
  "updated_at": "2020-08-24T14:15:22Z",
  "reference_account_id": "295378ec-036e-4f3f-ae5c-2be85c93e837",
  "account_group_id": "ac1c39e9-2101-46b8-a624-d10a9e351b6c",
  "user_id": "b668282b-cdf3-439a-bda2-3f3c9f655bb7",
  "amount": "200.00",
  "currency": "EUR",
  "remittance_information": "Withdrawal",
  "taxes": [
    {
      "type": "TOTAL",
      "amount": "38.00",
      "currency": "EUR"
    }
  ],
  "status": "CONFIRMED",
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


# Cancel Cash Withdrawal

Cancels a withdrawal specified by its ID. It is only possible to cancel a withdrawal if it has the status `NEW`.

```csharp
CancelCashWithdrawalAsync(
    Guid withdrawalId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `withdrawalId` | `Guid` | Template, Required | The unique identifier of the cash withdrawal. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`payments:admin`

## Response Type

**204**: Request has been processed successfully.

`Task`

## Example Usage

```csharp
Guid withdrawalId = new Guid("00001272-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await withdrawalsApi.CancelCashWithdrawalAsync(
        withdrawalId,
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
| 422 | Unprocessable Entity. The syntax of request is correct but server can't process it due a semantic error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Cash Withdrawals

Returns a paginated list of cash withdrawals for the account group specified by its ID.

```csharp
ListCashWithdrawalsAsync(
    Guid accountGroupId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort4? sort = Models.Sort4.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountGroupId` | `Guid` | Template, Required | The unique identifier of the account group. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort4?`](../../doc/models/sort-4.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort4.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`payments:admin`, `payments:read`

## Response Type

**200**: Withdrawals list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.WithdrawalsListResponse](../../doc/models/withdrawals-list-response.md).

## Example Usage

```csharp
Guid accountGroupId = new Guid("00002436-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort4? sort = Sort4.Id;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<WithdrawalsListResponse> result = await withdrawalsApi.ListCashWithdrawalsAsync(
        accountGroupId,
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
    "total_count": 1
  },
  "data": [
    {
      "id": "6ffa6b16-2380-4e7a-88b2-ae625c8eef99",
      "created_at": "2020-08-24T14:15:22Z",
      "updated_at": "2020-08-24T14:15:22Z",
      "reference_account_id": "295378ec-036e-4f3f-ae5c-2be85c93e837",
      "account_group_id": "ac1c39e9-2101-46b8-a624-d10a9e351b6c",
      "user_id": "b668282b-cdf3-439a-bda2-3f3c9f655bb7",
      "amount": "200.00",
      "currency": "EUR",
      "remittance_information": "Withdrawal",
      "taxes": [
        {
          "type": "TOTAL",
          "amount": "38.00",
          "currency": "EUR"
        }
      ],
      "status": "CONFIRMED",
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

