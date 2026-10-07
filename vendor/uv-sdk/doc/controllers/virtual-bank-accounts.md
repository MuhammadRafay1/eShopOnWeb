# Virtual Bank Accounts

```csharp
VirtualBankAccountsApi virtualBankAccountsApi = client.VirtualBankAccountsApi;
```

## Class Name

`VirtualBankAccountsApi`

## Methods

* [List Virtual Bank Accounts](../../doc/controllers/virtual-bank-accounts.md#list-virtual-bank-accounts)
* [Create Virtual Bank Account](../../doc/controllers/virtual-bank-accounts.md#create-virtual-bank-account)
* [Retrieve Virtual Bank Account](../../doc/controllers/virtual-bank-accounts.md#retrieve-virtual-bank-account)


# List Virtual Bank Accounts

Returns a list of virtual bank accounts.

```csharp
ListVirtualBankAccountsAsync(
    Guid accountGroupId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort4? sort = Models.Sort4.CreatedAt,
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
| `sort` | [`Sort4?`](../../doc/models/sort-4.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort4.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |

## Requires scope

### oauth-client-credentials

`payments:admin`, `payments:read`

## Response Type

**200**: List of virtual bank accounts.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsVirtualBankAccountsListResponse](../../doc/models/payments-virtual-bank-accounts-list-response.md).

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
    ApiResponse<PaymentsVirtualBankAccountsListResponse> result = await virtualBankAccountsApi.ListVirtualBankAccountsAsync(
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
      "id": "22913092-0484-42e9-9273-9ffd6dfd0b13",
      "created_at": "2021-07-21T14:10:00.00Z",
      "account_group_id": "d1354aad-6da7-4da7-aaa1-e00ebfd3240d",
      "name": "Max's Virtual Bank Account",
      "owner": {
        "name": "Max Musterman"
      },
      "identification": {
        "swift": {
          "iban": "DE89000000000000000000",
          "bic": "ABCDEXXX"
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


# Create Virtual Bank Account

Creates a virtual bank account for an account group. The virtual bank account provides an IBAN and BIC that end users can use to send SEPA Credit Transfers directly to their investment account.

```csharp
CreateVirtualBankAccountAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PaymentsVirtualBankAccountCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PaymentsVirtualBankAccountCreateRequest`](../../doc/models/payments-virtual-bank-account-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`payments:admin`

## Response Type

**200**: Virtual Bank Account created.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsVirtualBankAccountsResponse](../../doc/models/payments-virtual-bank-accounts-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PaymentsVirtualBankAccountCreateRequest body = new PaymentsVirtualBankAccountCreateRequest
{
    AccountGroupId = new Guid("f874a64a-ceaa-42b2-b59a-39d4f62166fb"),
    Name = "Max's Virtual Bank Account",
};

try
{
    ApiResponse<PaymentsVirtualBankAccountsResponse> result = await virtualBankAccountsApi.CreateVirtualBankAccountAsync(
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
  "id": "22913092-0484-42e9-9273-9ffd6dfd0b13",
  "created_at": "2021-07-21T14:10:00.00Z",
  "account_group_id": "d1354aad-6da7-4da7-aaa1-e00ebfd3240d",
  "name": "Max's Virtual Bank Account",
  "owner": {
    "name": "Max Musterman"
  },
  "identification": {
    "swift": {
      "iban": "DE89000000000000000000",
      "bic": "ABCDEXXX"
    }
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


# Retrieve Virtual Bank Account

Returns a virtual bank account specified by its ID.

```csharp
RetrieveVirtualBankAccountAsync(
    Guid virtualBankAccountId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `virtualBankAccountId` | `Guid` | Template, Required | The unique identifier of the virtual bank account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`payments:admin`, `payments:read`

## Response Type

**200**: OK.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsVirtualBankAccountsResponse](../../doc/models/payments-virtual-bank-accounts-response.md).

## Example Usage

```csharp
Guid virtualBankAccountId = new Guid("0000262a-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PaymentsVirtualBankAccountsResponse> result = await virtualBankAccountsApi.RetrieveVirtualBankAccountAsync(
        virtualBankAccountId,
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
  "id": "22913092-0484-42e9-9273-9ffd6dfd0b13",
  "created_at": "2021-07-21T14:10:00.00Z",
  "account_group_id": "d1354aad-6da7-4da7-aaa1-e00ebfd3240d",
  "name": "Max's Virtual Bank Account",
  "owner": {
    "name": "Max Musterman"
  },
  "identification": {
    "swift": {
      "iban": "DE89000000000000000000",
      "bic": "ABCDEXXX"
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

