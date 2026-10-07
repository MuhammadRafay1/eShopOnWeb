
# User Data Change Body

## Class Name

`UserDataChangeBody`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserByolDataChangeRequest`](../../../doc/models/user-byol-data-change-request.md) | UserDataChangeBody.FromUserBYOLDataChangeRequest(UserByolDataChangeRequest userByolDataChangeRequest) |
| [`UserDataChangeBodyCase1`](../../../doc/models/containers/user-data-change-body-case-1.md) | UserDataChangeBody.FromUserDataChangeBodyCase1(UserDataChangeBodyCase1 userDataChangeBodyCase1) |

## UserByolDataChangeRequest

### Initialization Code

#### Example

```csharp
UserDataChangeBody value = UserDataChangeBody.FromUserBYOLDataChangeRequest(
    new UserByolDataChangeRequest
    {
    }
);
```

## UserDataChangeBodyCase1

### Initialization Code

#### Example

```csharp
UserDataChangeBody value = UserDataChangeBody.FromUserDataChangeBodyCase1(
    UserDataChangeBodyCase1.FromUserTOLDataChangeRequestNameChange(
        new UserTolDataChangeRequestNameChange
        {
            FirstName = "first_name4",
            LastName = "last_name2",
            IssuanceDate = DateTime.Parse("2016-03-13"),
            DataDownloadLink = "data_download_link6",
            DocumentType = DocumentType.Passport,
        }
    )
);
```

