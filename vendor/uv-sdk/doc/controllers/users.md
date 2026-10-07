# Users

All user related paths.

```csharp
UsersApi usersApi = client.UsersApi;
```

## Class Name

`UsersApi`

## Methods

* [List Users](../../doc/controllers/users.md#list-users)
* [Create User](../../doc/controllers/users.md#create-user)
* [Retrieve User](../../doc/controllers/users.md#retrieve-user)
* [Offboard User](../../doc/controllers/users.md#offboard-user)
* [User Data Change](../../doc/controllers/users.md#user-data-change)


# List Users

Returns a paginated list of users.

Use the `offset` and `limit` query parameters to page through results; `meta.total_count` gives the total number of matching users. Use `sort`/`order` to control ordering by `created_at` or `updated_at`.

See the User onboarding guide ([TOL](https://docs.upvest.co/products/tol/guides/users/users_onboarding_overview) / [BYOL](https://docs.upvest.co/products/byol/guides/users/users_onboarding_overview)) for the user lifecycle.

```csharp
ListUsersAsync(
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
| `sort` | [`Sort1?`](../../doc/models/sort-1.md) | Query, Optional | Sort the result by `created_at`, `updated_at`.<br><br>**Default**: `Sort1.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`users:admin`, `users:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.UsersListResponse](../../doc/models/users-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort1? sort = Sort1.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<UsersListResponse> result = await usersApi.ListUsersAsync(
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
      "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be202",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "first_name": "Karl",
      "last_name": "Schmidt",
      "email": "karl.schmidt@example.com",
      "salutation": "SALUTATION_MALE",
      "title": "DR",
      "birth_date": "1972-09-30",
      "birth_city": "Berlin",
      "birth_country": "DE",
      "birth_name": "Schmidt",
      "decease_date": null,
      "nationalities": [
        "DE",
        "AU"
      ],
      "postal_address": {
        "address_line1": "Rosenweg 221",
        "address_line2": "apt. 33",
        "postcode": "45678",
        "city": "Berlin",
        "state": "BE",
        "country": "DE"
      },
      "address": {
        "address_line1": "Rosenweg 221",
        "address_line2": "apt. 33",
        "postcode": "45678",
        "city": "Berlin",
        "state": "BE",
        "country": "DE"
      },
      "status": "ACTIVE"
    },
    {
      "id": "287673ef-1bae-4d8a-9e78-8ddaae249d72",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "first_name": "John",
      "last_name": "Doe",
      "email": "john.doe@example.com",
      "salutation": "SALUTATION_MALE",
      "title": "PROF_DR",
      "birth_date": "1970-10-01",
      "birth_city": "Berlin",
      "birth_country": "DE",
      "birth_name": "Schmidt",
      "nationalities": [
        "DE",
        "AU"
      ],
      "decease_date": "2026-02-27",
      "postal_address": null,
      "address": {
        "address_line1": "Rosenweg 221",
        "address_line2": "apt. 33",
        "postcode": "45678",
        "city": "Berlin",
        "state": "BE",
        "country": "DE"
      },
      "status": "ACTIVE"
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create User

Creates a user.

The user must complete identity checks and provide any required identifiers before activation.

See the Creating a user guide ([TOL](https://docs.upvest.co/products/tol/guides/users/users_onboarding_create) / [BYOL](https://docs.upvest.co/products/byol/guides/users/users_onboarding_create)) for the full onboarding flow.

```csharp
CreateUserAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateUserBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateUserBody`](../../doc/models/containers/create-user-body.md) | Body, Optional | This is a container for any-of cases. |

## Requires scope

### oauth-client-credentials

`users:admin`

## Response Type

**200**: User created.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type CreateUserResponse.

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
CreateUserBody body = CreateUserBody.FromUserBYOLCreateRequest(
    new UserByolCreateRequest
    {
        FirstName = "Karl",
        LastName = "Schmidt",
        BirthDate = DateTime.Parse("1972-09-30"),
        Nationalities = new List<Nationality>
        {
            Nationality.De,
            Nationality.Au,
        },
        Address = new Address
        {
            AddressLine1 = "Rosenweg 221",
            Postcode = "45678",
            Country = Country.De,
            City = "Berlin",
            AddressLine2 = "apt. 33",
            State = "BE",
        },
        Email = "karl.schmidt@example.com",
        Salutation = Salutation.SalutationMale,
        Title = Title.Dr,
        BirthCity = "Berlin",
        BirthCountry = BirthCountry.De,
        BirthName = "Schmidt",
        PostalAddress = new Address
        {
            AddressLine1 = "Rosenweg 221",
            Postcode = "45678",
            Country = Country.De,
            City = "Berlin",
            AddressLine2 = "apt. 33",
            State = "BE",
        },
        Tags = new List<Tag>
        {
            Tag.ClientEmployee,
        },
    }
);

try
{
    ApiResponse<CreateUserResponse> result = await usersApi.CreateUserAsync(
        upvestClientId,
        idempotencyKey,
        null,
        body
    );
    result.Data.Match<VoidType>(
        userByol: userByol =>
        {
            // TODO: handle userByol here
            Console.WriteLine(userByol);
            return null;
        },
        userTol: userTol =>
        {
            // TODO: handle userTol here
            Console.WriteLine(userTol);
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
  "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be202",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "first_name": "Karl",
  "last_name": "Schmidt",
  "email": "karl.schmidt@example.com",
  "salutation": "SALUTATION_MALE",
  "title": "DR",
  "birth_date": "1972-09-30",
  "birth_city": "Berlin",
  "birth_country": "DE",
  "birth_name": "Schmidt",
  "decease_date": null,
  "nationalities": [
    "DE",
    "AU"
  ],
  "postal_address": {
    "address_line1": "Rosenweg 221",
    "address_line2": "apt. 33",
    "postcode": "45678",
    "city": "Berlin",
    "state": "BE",
    "country": "DE"
  },
  "address": {
    "address_line1": "Rosenweg 221",
    "address_line2": "apt. 33",
    "postcode": "45678",
    "city": "Berlin",
    "state": "BE",
    "country": "DE"
  },
  "status": "ACTIVE"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve User

Returns the user identified by `user_id`, including profile and status details

```csharp
RetrieveUserAsync(
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

`users:admin`, `users:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type RetrieveUserResponse.

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<RetrieveUserResponse> result = await usersApi.RetrieveUserAsync(
        userId,
        upvestClientId,
        upvestApiVersion
    );
    result.Data.Match<VoidType>(
        userByol: userByol =>
        {
            // TODO: handle userByol here
            Console.WriteLine(userByol);
            return null;
        },
        userTol: userTol =>
        {
            // TODO: handle userTol here
            Console.WriteLine(userTol);
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
  "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be202",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "first_name": "Karl",
  "last_name": "Schmidt",
  "email": "karl.schmidt@example.com",
  "salutation": "SALUTATION_MALE",
  "title": "DR",
  "birth_date": "1972-09-30",
  "birth_city": "Berlin",
  "birth_country": "DE",
  "birth_name": "Schmidt",
  "decease_date": null,
  "nationalities": [
    "DE",
    "AU"
  ],
  "postal_address": {
    "address_line1": "Rosenweg 221",
    "address_line2": "apt. 33",
    "postcode": "45678",
    "city": "Berlin",
    "state": "BE",
    "country": "DE"
  },
  "address": {
    "address_line1": "Rosenweg 221",
    "address_line2": "apt. 33",
    "postcode": "45678",
    "city": "Berlin",
    "state": "BE",
    "country": "DE"
  },
  "status": "ACTIVE",
  "tags": [
    "CLIENT_EMPLOYEE"
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


# Offboard User

Starts the user offboarding process in the background.

```csharp
OffboardUserAsync(
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

`users:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await usersApi.OffboardUserAsync(
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


# User Data Change

Requests a data change for a user specified by ID.

```csharp
UserDataChangeAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    UserDataChangeBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`UserDataChangeBody`](../../doc/models/containers/user-data-change-body.md) | Body, Optional | This is a container for any-of cases. |

## Requires scope

### oauth-client-credentials

`users:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UserDataChangeBody body = UserDataChangeBody.FromUserBYOLDataChangeRequest(
    new UserByolDataChangeRequest
    {
        FirstName = "Karl",
        LastName = "Schmidt",
        ["issuance_date"] = ApiHelper.JsonDeserialize<object>("\"2030-01-01\""),
        ["data_download_link"] = ApiHelper.JsonDeserialize<object>("\"https://bucket.customer.com/ident/user3.zip\""),
        ["document_type"] = ApiHelper.JsonDeserialize<object>("\"ID_CARD\""),
    }
);

try
{
    await usersApi.UserDataChangeAsync(
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

