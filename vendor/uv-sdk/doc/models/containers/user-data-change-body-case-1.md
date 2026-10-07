
# User Data Change Body Case 1

## Class Name

`UserDataChangeBodyCase1`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`UserTolDataChangeRequestNameChange`](../../../doc/models/user-tol-data-change-request-name-change.md) | UserDataChangeBodyCase1.FromUserTOLDataChangeRequestNameChange(UserTolDataChangeRequestNameChange userTolDataChangeRequestNameChange) |
| [`UserTolDataChangeRequestOther`](../../../doc/models/user-tol-data-change-request-other.md) | UserDataChangeBodyCase1.FromUserTOLDataChangeRequestOther(UserTolDataChangeRequestOther userTolDataChangeRequestOther) |
| [`UserTolDataChangeRequestNationality`](../../../doc/models/user-tol-data-change-request-nationality.md) | UserDataChangeBodyCase1.FromUserTOLDataChangeRequestNationality(UserTolDataChangeRequestNationality userTolDataChangeRequestNationality) |
| [`UserTolDataChangeRequestAddress`](../../../doc/models/user-tol-data-change-request-address.md) | UserDataChangeBodyCase1.FromUserTOLDataChangeRequestAddress(UserTolDataChangeRequestAddress userTolDataChangeRequestAddress) |

## UserTolDataChangeRequestNameChange

### Initialization Code

#### Example

```csharp
UserDataChangeBodyCase1 value = UserDataChangeBodyCase1.FromUserTOLDataChangeRequestNameChange(
    new UserTolDataChangeRequestNameChange
    {
        FirstName = "first_name4",
        LastName = "last_name2",
        IssuanceDate = DateTime.Parse("2016-03-13"),
        DataDownloadLink = "data_download_link6",
        DocumentType = DocumentType.Passport,
    }
);
```

## UserTolDataChangeRequestOther

### Initialization Code

#### Example

```csharp
UserDataChangeBodyCase1 value = UserDataChangeBodyCase1.FromUserTOLDataChangeRequestOther(
    new UserTolDataChangeRequestOther
    {
    }
);
```

## UserTolDataChangeRequestNationality

### Initialization Code

#### Example

```csharp
UserDataChangeBodyCase1 value = UserDataChangeBodyCase1.FromUserTOLDataChangeRequestNationality(
    new UserTolDataChangeRequestNationality
    {
        Nationalities = new List<Nationality>
        {
            Nationality.Vc,
        },
        IssuanceDate = DateTime.Parse("2016-03-13"),
        DataDownloadLink = "data_download_link2",
        DocumentType = DocumentType.Passport,
    }
);
```

## UserTolDataChangeRequestAddress

### Initialization Code

#### Example

```csharp
UserDataChangeBodyCase1 value = UserDataChangeBodyCase1.FromUserTOLDataChangeRequestAddress(
    new UserTolDataChangeRequestAddress
    {
        Address = new Address
        {
            AddressLine1 = "address_line10",
            Postcode = "postcode0",
            Country = Country.Bf,
            City = "city6",
        },
        IssuanceDate = DateTime.Parse("2016-03-13"),
        DataDownloadLink = "data_download_link4",
        DocumentType = DocumentType2.RegistrationCert,
    }
);
```

