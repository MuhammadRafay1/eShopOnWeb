# Savings Plans

```csharp
SavingsPlansApi savingsPlansApi = client.SavingsPlansApi;
```

## Class Name

`SavingsPlansApi`

## Methods

* [Retrieve Savings Plan](../../doc/controllers/savings-plans.md#retrieve-savings-plan)
* [Delete Savings Plan](../../doc/controllers/savings-plans.md#delete-savings-plan)
* [List Savings Plans](../../doc/controllers/savings-plans.md#list-savings-plans)
* [Create Savings Plan](../../doc/controllers/savings-plans.md#create-savings-plan)
* [Retrieve Savings Plan Execution](../../doc/controllers/savings-plans.md#retrieve-savings-plan-execution)
* [Delete Savings Plan Execution](../../doc/controllers/savings-plans.md#delete-savings-plan-execution)
* [List Savings Plan Executions](../../doc/controllers/savings-plans.md#list-savings-plan-executions)


# Retrieve Savings Plan

Retrieve a savings plan specified by its ID.

```csharp
RetrieveSavingsPlanAsync(
    Guid savingsPlanId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `savingsPlanId` | `Guid` | Template, Required | The unique identifier of the savings plan. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`savings_plans:admin`, `savings_plans:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.SavingsPlanInstrument](../../doc/models/savings-plan-instrument.md).

## Example Usage

```csharp
Guid savingsPlanId = new Guid("00001c80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<SavingsPlanInstrument> result = await savingsPlansApi.RetrieveSavingsPlanAsync(
        savingsPlanId,
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
  "id": "fc34d28e-38f5-49d2-92f6-82acb79098f4",
  "created_at": "2023-07-21T14:10:00.00Z",
  "updated_at": "2023-07-21T15:10:00.00Z",
  "user_id": "4b9732bd-7496-4913-8a5f-6360479d7fed",
  "account_id": "00ef0be8-d564-43af-b3c7-11b7a2188030",
  "name": "Instrument savings plan",
  "type": "INSTRUMENT",
  "instrument_id": "DE0007664005",
  "instrument_id_type": "ISIN",
  "cash_amount": "100.00",
  "currency": "EUR",
  "start_date": "2023-07-24",
  "period": "WEEK",
  "interval": 1,
  "status": "ACTIVE",
  "fee_configuration": [
    {
      "type": "TRANSACTION_FEE_BUY",
      "transaction_fee_model_id": "3ae9403c-1611-46eb-84ee-e06a02bcfd08"
    }
  ],
  "cancellation_reason": "CANCELLED_BY_CLIENT",
  "cancellation_details": "User requested cancellation"
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


# Delete Savings Plan

Delete a savings plan specified by its ID.

```csharp
DeleteSavingsPlanAsync(
    Guid savingsPlanId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `savingsPlanId` | `Guid` | Template, Required | The unique identifier of the savings plan. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`savings_plans:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid savingsPlanId = new Guid("00001c80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await savingsPlansApi.DeleteSavingsPlanAsync(
        savingsPlanId,
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
| 409 | Conflict. An operation is not available for the current state of the resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Savings Plans

Returns a paginated list of savings plans for the authenticated client.

Savings plans can be of type `INSTRUMENT` (recurring buy of a single instrument) or `PORTFOLIO` (recurring investment distributed across the account's portfolio allocation).

```csharp
ListSavingsPlansAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    string accountId = null,
    string userId = null,
    ListSavingsPlansInstrumentId instrumentId = null,
    Models.Sort23? sort = Models.Sort23.Id,
    Models.Order58? order = Models.Order58.Asc,
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
| `accountId` | `string` | Query, Optional | Filters savings plans by account ID |
| `userId` | `string` | Query, Optional | Filters savings plans by user ID |
| `instrumentId` | [`ListSavingsPlansInstrumentId`](../../doc/models/containers/list-savings-plans-instrument-id.md) | Query, Optional | This is a container for one-of cases. |
| `sort` | [`Sort23?`](../../doc/models/sort-23.md) | Query, Optional | Sort the result by `id`.<br><br>**Default**: `Sort23.id` |
| `order` | [`Order58?`](../../doc/models/order-58.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. By default, only ASC for ascending sort.<br><br>**Default**: `Order58.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`savings_plans:admin`, `savings_plans:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.SavingsPlanListResponse](../../doc/models/savings-plan-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
string accountId = "3ada8e9e-82c4-4c78-a43d-a691b1636509";
string userId = "996e5e2c-bb76-4a38-8d5c-ff43d86904da";
ListSavingsPlansInstrumentId instrumentId = ListSavingsPlansInstrumentId.FromString("FR0010524777");

Sort23? sort = Sort23.Id;
Order58? order = Order58.Asc;
int? limit = 100;
try
{
    ApiResponse<SavingsPlanListResponse> result = await savingsPlansApi.ListSavingsPlansAsync(
        upvestClientId,
        upvestApiVersion,
        accountId,
        userId,
        instrumentId,
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
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "fc34d28e-38f5-49d2-92f6-82acb79098f4",
      "created_at": "2023-07-21T14:10:00.00Z",
      "updated_at": "2023-07-21T15:10:00.00Z",
      "user_id": "4b9732bd-7496-4913-8a5f-6360479d7fed",
      "account_id": "00ef0be8-d564-43af-b3c7-11b7a2188030",
      "name": "Instrument savings plan",
      "type": "INSTRUMENT",
      "instrument_id": "DE0007664005",
      "instrument_id_type": "ISIN",
      "cash_amount": "100.00",
      "currency": "EUR",
      "start_date": "2023-07-24",
      "period": "WEEK",
      "interval": 1,
      "status": "ACTIVE",
      "fee_configuration": [
        {
          "type": "TRANSACTION_FEE_BUY",
          "transaction_fee_model_id": "3ae9403c-1611-46eb-84ee-e06a02bcfd08"
        }
      ],
      "cancellation_reason": "CANCELLED_BY_CLIENT",
      "cancellation_details": "User requested cancellation"
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


# Create Savings Plan

Creates a recurring savings plan that automatically invests a fixed cash amount at a defined frequency.

Set `type` to `INSTRUMENT` to invest in a single instrument, or `PORTFOLIO` to distribute the investment across the account's portfolio allocation.

```csharp
CreateSavingsPlanAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateSavingsPlanBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateSavingsPlanBody`](../../doc/models/containers/create-savings-plan-body.md) | Body, Optional | This is a container for any-of cases. |

## Requires scope

### oauth-client-credentials

`savings_plans:admin`

## Response Type

**202**: Savings plan object

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.SavingsPlanInstrument](../../doc/models/savings-plan-instrument.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
CreateSavingsPlanBody body = CreateSavingsPlanBody.FromSavingsPlanPortfolio(
    new SavingsPlanPortfolio
    {
        UserId = new Guid("fc34d28e-38f5-49d2-92f6-82acb79098f4"),
        AccountId = new Guid("00ef0be8-d564-43af-b3c7-11b7a2188030"),
        Type = "INSTRUMENT",
        CashAmount = "100.00",
        Currency = Currency.Eur,
        StartDate = "2023-07-24",
        Period = Period1.Week,
        Interval = 2,
        Name = "Instrument savings plan",
        ["instrument_id"] = ApiHelper.JsonDeserialize<object>("\"DE0007664005\""),
        ["instrument_id_type"] = ApiHelper.JsonDeserialize<object>("\"ISIN\""),
        ["fee_configuration"] = ApiHelper.JsonDeserialize<object>("[{\"type\":\"TRANSACTION_FEE_BUY\",\"transaction_fee_model_id\":\"3ae9403c-1611-46eb-84ee-e06a02bcfd08\"}]"),
    }
);

try
{
    ApiResponse<SavingsPlanInstrument> result = await savingsPlansApi.CreateSavingsPlanAsync(
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
  "id": "fc34d28e-38f5-49d2-92f6-82acb79098f4",
  "created_at": "2023-07-21T14:10:00.00Z",
  "updated_at": "2023-07-21T15:10:00.00Z",
  "user_id": "4b9732bd-7496-4913-8a5f-6360479d7fed",
  "account_id": "00ef0be8-d564-43af-b3c7-11b7a2188030",
  "name": "Instrument savings plan",
  "type": "INSTRUMENT",
  "instrument_id": "DE0007664005",
  "instrument_id_type": "ISIN",
  "cash_amount": "100.00",
  "currency": "EUR",
  "start_date": "2023-07-24",
  "period": "WEEK",
  "interval": 1,
  "status": "ACTIVE",
  "fee_configuration": [
    {
      "type": "TRANSACTION_FEE_BUY",
      "transaction_fee_model_id": "3ae9403c-1611-46eb-84ee-e06a02bcfd08"
    }
  ],
  "cancellation_reason": "CANCELLED_BY_CLIENT",
  "cancellation_details": "User requested cancellation"
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


# Retrieve Savings Plan Execution

Retrieves a single execution of a savings plan by its ID, including the order placed and its current status.

```csharp
RetrieveSavingsPlanExecutionAsync(
    Guid savingsPlanId,
    Guid savingsPlanExecutionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `savingsPlanId` | `Guid` | Template, Required | The unique identifier of the savings plan. Universally Unique Identifier (UUID). |
| `savingsPlanExecutionId` | `Guid` | Template, Required | The unique identifier of the savings plan execution. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`savings_plans:admin`, `savings_plans:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type RetrieveSavingsPlanExecutionResponse.

## Example Usage

```csharp
Guid savingsPlanId = new Guid("00001c80-0000-0000-0000-000000000000");
Guid savingsPlanExecutionId = new Guid("00001286-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<RetrieveSavingsPlanExecutionResponse> result = await savingsPlansApi.RetrieveSavingsPlanExecutionAsync(
        savingsPlanId,
        savingsPlanExecutionId,
        upvestClientId,
        upvestApiVersion
    );
    result.Data.Match<VoidType>(
        savingsPlanExecutionInstrument: savingsPlanExecutionInstrument =>
        {
            // TODO: handle savingsPlanExecutionInstrument here
            Console.WriteLine(savingsPlanExecutionInstrument);
            return null;
        },
        savingsPlanExecutionPortfolio: savingsPlanExecutionPortfolio =>
        {
            // TODO: handle savingsPlanExecutionPortfolio here
            Console.WriteLine(savingsPlanExecutionPortfolio);
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
  "id": "fc34d28e-38f5-49d2-92f6-82acb79098f4",
  "created_at": "2023-07-21T14:10:00.00Z",
  "updated_at": "2023-07-21T15:10:00.00Z",
  "user_id": "4b9732bd-7496-4913-8a5f-6360479d7fed",
  "account_id": "00ef0be8-d564-43af-b3c7-11b7a2188030",
  "savings_plan_id": "c31083c1-1cd3-405f-95d1-f49a37f0e032",
  "order_id": "68068440-6b8e-4700-8cfc-95bf21b34b96",
  "cash_amount": "100",
  "currency": "EUR",
  "status": "FILLED",
  "type": "INSTRUMENT",
  "instrument_id": "IE00BYTRRD19",
  "instrument_id_type": "ISIN",
  "execution_date": "2023-07-24"
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


# Delete Savings Plan Execution

Deletes a scheduled savings plan execution before it has been processed. Only executions in the `NEW` status can be deleted.

```csharp
DeleteSavingsPlanExecutionAsync(
    Guid savingsPlanId,
    Guid savingsPlanExecutionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `savingsPlanId` | `Guid` | Template, Required | The unique identifier of the savings plan. Universally Unique Identifier (UUID). |
| `savingsPlanExecutionId` | `Guid` | Template, Required | The unique identifier of the savings plan execution. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`savings_plans:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid savingsPlanId = new Guid("00001c80-0000-0000-0000-000000000000");
Guid savingsPlanExecutionId = new Guid("00001286-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await savingsPlansApi.DeleteSavingsPlanExecutionAsync(
        savingsPlanId,
        savingsPlanExecutionId,
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
| 409 | Conflict. An operation is not available for the current state of the resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Savings Plan Executions

List savings plan executions for a saving plan specified by its ID.

```csharp
ListSavingsPlanExecutionsAsync(
    Guid savingsPlanId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    string startDate = null,
    string endDate = null,
    Models.Sort35? sort = Models.Sort35.ExecutionDate,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `savingsPlanId` | `Guid` | Template, Required | The unique identifier of the savings plan. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `startDate` | `string` | Query, Optional | - |
| `endDate` | `string` | Query, Optional | - |
| `sort` | [`Sort35?`](../../doc/models/sort-35.md) | Query, Optional | Sort the result by `execution_date`, `created_at`, or `updated_at`.<br><br>**Default**: `Sort35.execution_date` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`savings_plans:admin`, `savings_plans:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.SavingsPlanExecutionListResponse](../../doc/models/savings-plan-execution-list-response.md).

## Example Usage

```csharp
Guid savingsPlanId = new Guid("00001c80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
string startDate = "10/14/2022 10:10:10";
string endDate = "10/14/2022 10:10:10";
Sort35? sort = Sort35.ExecutionDate;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<SavingsPlanExecutionListResponse> result = await savingsPlansApi.ListSavingsPlanExecutionsAsync(
        savingsPlanId,
        upvestClientId,
        upvestApiVersion,
        startDate,
        endDate,
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
    "sort": "execution_date",
    "order": "ASC"
  },
  "data": [
    {
      "id": "fc34d28e-38f5-49d2-92f6-82acb79098f4",
      "created_at": "2023-07-21T14:10:00.00Z",
      "updated_at": "2023-07-21T15:10:00.00Z",
      "user_id": "4b9732bd-7496-4913-8a5f-6360479d7fed",
      "account_id": "00ef0be8-d564-43af-b3c7-11b7a2188030",
      "savings_plan_id": "c31083c1-1cd3-405f-95d1-f49a37f0e032",
      "order_id": "68068440-6b8e-4700-8cfc-95bf21b34b96",
      "cash_amount": "100",
      "currency": "EUR",
      "status": "FILLED",
      "type": "INSTRUMENT",
      "instrument_id": "DE0007664005",
      "instrument_id_type": "ISIN",
      "execution_date": "2023-10-05",
      "fee_configuration": [
        {
          "type": "TRANSACTION_FEE_BUY",
          "transaction_fee_model_id": "3ae9403c-1611-46eb-84ee-e06a02bcfd08"
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
| 405 | Method Not Allowed. The requested method is not allowed on the requested resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

