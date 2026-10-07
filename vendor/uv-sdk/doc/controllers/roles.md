# Roles

All Roles related paths.

```csharp
RolesApi rolesApi = client.RolesApi;
```

## Class Name

`RolesApi`

## Methods

* [List Roles](../../doc/controllers/roles.md#list-roles)
* [Create Role](../../doc/controllers/roles.md#create-role)
* [Retrieve Role](../../doc/controllers/roles.md#retrieve-role)
* [Role Deactivation](../../doc/controllers/roles.md#role-deactivation)


# List Roles

Returns a paginated list of roles.

Use the `offset` and `limit` query parameters to page through results; `meta.total_count` gives the total number of matching roles. Use `user_id` to filter by a specific user, `entity_id` to filter by account group or business, and `sort`/`order` to control the ordering by `created_at` or `updated_at`.

See the User roles guide ([TOL](https://docs.upvest.co/products/tol/guides/users/users_onboarding_roles) / [BYOL](https://docs.upvest.co/products/byol/guides/users/users_onboarding_roles)) for role types and assignment rules.

```csharp
ListRolesAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort1? sort = Models.Sort1.CreatedAt,
    Guid? userId = null,
    Guid? entityId = null,
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
| `sort` | [`Sort1?`](../../doc/models/sort-1.md) | Query, Optional | The field to sort the results by. One of `created_at` or `updated_at`; defaults to `created_at`.<br><br>**Default**: `Sort1.created_at` |
| `userId` | `Guid?` | Query, Optional | Filters results by user ID. Universally Unique Identifier (UUID). |
| `entityId` | `Guid?` | Query, Optional | Filters results by entity ID. Universally Unique Identifier (UUID). |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`roles:admin`, `roles:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.RolesListResponse](../../doc/models/roles-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort1? sort = Sort1.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<RolesListResponse> result = await rolesApi.ListRolesAsync(
        upvestClientId,
        upvestApiVersion,
        sort,
        null,
        null,
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
    "count": 2,
    "total_count": 2,
    "sort": "created_at",
    "order": "ASC"
  },
  "data": [
    {
      "id": "baf05386-0459-4e8c-9ac9-cd6442f194dd",
      "created_at": "2025-04-01T10:11:40Z",
      "updated_at": "2025-04-01T10:11:40Z",
      "user_id": "9c36af78-91a0-4174-a515-fc81214e3dab",
      "entity_type": "ACCOUNT_GROUP",
      "entity_id": "413715f2-5401-4b97-8055-034a6b879f8c",
      "custody_type": "SOLE_CUSTODY",
      "role_type": "GUARDIAN",
      "status": "ACTIVE"
    },
    {
      "id": "e8b5a51d-8baf-4d0b-8a3b-8f8f8f8f8f8f",
      "created_at": "2025-04-01T10:12:40Z",
      "updated_at": "2025-04-01T10:12:40Z",
      "user_id": "0d10c51f-33f2-4399-b8ab-92ec84e6b2f0",
      "entity_type": "BUSINESS",
      "entity_id": "6deb17c8-950e-4377-b500-5522af5ef712",
      "role_type": "LEGAL_REPRESENTATIVE",
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


# Create Role

Creates a role, assigning a user to an account group or business entity.

The `entity_type` parameter determines whether `entity_id` refers to an account group or a business, and constrains which `role_type` values are valid.

See the User roles guide ([TOL](https://docs.upvest.co/products/tol/guides/users/users_onboarding_roles) / [BYOL](https://docs.upvest.co/products/byol/guides/users/users_onboarding_roles)) for role types and assignment rules.

```csharp
CreateRoleAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateRoleBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateRoleBody`](../../doc/models/containers/create-role-body.md) | Body, Optional | This is a container for any-of cases. |

## Requires scope

### oauth-client-credentials

`roles:admin`

## Response Type

**200**: Role created.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type CreateRoleResponse.

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
CreateRoleBody body = CreateRoleBody.FromAccountGroupRoleCreateRequest(
    new AccountGroupRoleCreateRequest
    {
        UserId = new Guid("9c36af78-91a0-4174-a515-fc81214e3dab"),
        EntityType = "ACCOUNT_GROUP",
        EntityId = new Guid("413715f2-5401-4b97-8055-034a6b879f8c"),
        RoleType = "GUARDIAN",
        CustodyType = CustodyType.SoleCustody,
    }
);

try
{
    ApiResponse<CreateRoleResponse> result = await rolesApi.CreateRoleAsync(
        upvestClientId,
        idempotencyKey,
        null,
        body
    );
    result.Data.Match<VoidType>(
        accountGroupRoleCreateResponse: accountGroupRoleCreateResponse =>
        {
            // TODO: handle accountGroupRoleCreateResponse here
            Console.WriteLine(accountGroupRoleCreateResponse);
            return null;
        },
        businessRole: businessRole =>
        {
            // TODO: handle businessRole here
            Console.WriteLine(businessRole);
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
  "id": "baf05386-0459-4e8c-9ac9-cd6442f194dd",
  "created_at": "2025-04-01T10:11:40Z",
  "updated_at": "2025-04-01T10:11:40Z",
  "user_id": "9c36af78-91a0-4174-a515-fc81214e3dab",
  "entity_type": "ACCOUNT_GROUP",
  "entity_id": "413715f2-5401-4b97-8055-034a6b879f8c",
  "role_type": "GUARDIAN",
  "status": "PENDING"
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


# Retrieve Role

Returns the role identified by `role_id`, including its status and entity assignment.

```csharp
RetrieveRoleAsync(
    Guid roleId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `roleId` | `Guid` | Template, Required | The unique identifier of the role. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`roles:admin`, `roles:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type RetrieveRoleResponse.

## Example Usage

```csharp
Guid roleId = new Guid("000010c8-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<RetrieveRoleResponse> result = await rolesApi.RetrieveRoleAsync(
        roleId,
        upvestClientId,
        upvestApiVersion
    );
    result.Data.Match<VoidType>(
        accountGroupRole: accountGroupRole =>
        {
            // TODO: handle accountGroupRole here
            Console.WriteLine(accountGroupRole);
            return null;
        },
        businessRole: businessRole =>
        {
            // TODO: handle businessRole here
            Console.WriteLine(businessRole);
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
  "id": "baf05386-0459-4e8c-9ac9-cd6442f194dd",
  "created_at": "2025-04-01T10:11:40Z",
  "updated_at": "2025-04-01T10:11:40Z",
  "user_id": "9c36af78-91a0-4174-a515-fc81214e3dab",
  "entity_type": "ACCOUNT_GROUP",
  "entity_id": "413715f2-5401-4b97-8055-034a6b879f8c",
  "role_type": "GUARDIAN",
  "custody_type": "JOINT_CUSTODY",
  "status": "ACTIVE"
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


# Role Deactivation

Deactivates the role specified by its ID. The role remains queryable with status `DEACTIVATED`.
Only roles whose `entity_type` is `BUSINESS` may be deactivated via this endpoint; account-group roles are rejected with `403 Forbidden`.
Deactivation is a terminal state; once deactivated, a role cannot be reactivated.
Deactivation is idempotent: calling this endpoint on an already-deactivated role returns `202 Accepted` without side effects.
A `ROLE.DEACTIVATED` webhook event is emitted on the first successful deactivation.

```csharp
RoleDeactivationAsync(
    Guid roleId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `roleId` | `Guid` | Template, Required | The unique identifier of the role. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`roles:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid roleId = new Guid("000010c8-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await rolesApi.RoleDeactivationAsync(
        roleId,
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

