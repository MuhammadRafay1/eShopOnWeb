# Tests

All test related paths.

```csharp
TestsApi testsApi = client.TestsApi;
```

## Class Name

`TestsApi`


# Create Bank Transaction

Trigger a bank transaction for testing purposes. This can be used to simulate a money transfer for various flows which need cash settlements on one of our bank accounts.

```csharp
CreateBankTransactionAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PaymentsBankTransactionCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PaymentsBankTransactionCreateRequest`](../../doc/models/payments-bank-transaction-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`tests:admin`

## Response Type

**200**: Bank Transaction

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsBankTransactionCreateResponse](../../doc/models/payments-bank-transaction-create-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PaymentsBankTransactionCreateRequest body = new PaymentsBankTransactionCreateRequest
{
    Account = new Account15
    {
        Iban = "NL70ABNA0106295004",
    },
    Amount = "13.37",
    Currency = BankTransactionCurrency.Eur,
    ClientReference = "client-reference",
    CounterpartyName = "counterparty-name",
    CounterpartyAccount = new CounterpartyAccount
    {
        Iban = "DE95500105173934555844",
    },
    RemittanceInformation = "remittance-info",
    CounterpartyAgent = new CounterpartyAgent
    {
        Bic = "DEUTDEFF",
    },
    VirtualBankAccountId = new Guid("f7b3b3b3-7b3b-4b3b-8b3b-3b3b3b3b3b3b"),
    PurposeCode = "OTHR",
};

try
{
    ApiResponse<PaymentsBankTransactionCreateResponse> result = await testsApi.CreateBankTransactionAsync(
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
  "bank_reference": "f2f0ef23-c68d-42c1-a744-8c86f471954b"
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

