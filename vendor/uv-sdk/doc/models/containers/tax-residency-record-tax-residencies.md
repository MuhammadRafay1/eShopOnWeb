
# Tax Residency Record Tax Residencies

## Class Name

`TaxResidencyRecordTaxResidencies`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`WithTaxIdentifierNumber`](../../../doc/models/with-tax-identifier-number.md) | TaxResidencyRecordTaxResidencies.FromWithTaxIdentifierNumber(WithTaxIdentifierNumber withTaxIdentifierNumber) |
| [`WithoutTaxIdentifierNumber`](../../../doc/models/without-tax-identifier-number.md) | TaxResidencyRecordTaxResidencies.FromWithoutTaxIdentifierNumber(WithoutTaxIdentifierNumber withoutTaxIdentifierNumber) |

## WithTaxIdentifierNumber

### Initialization Code

#### Example

```csharp
TaxResidencyRecordTaxResidencies value = TaxResidencyRecordTaxResidencies.FromWithTaxIdentifierNumber(
    new WithTaxIdentifierNumber
    {
        Country = Country.Mr,
        TaxIdentifierNumber = "tax_identifier_number6",
    }
);
```

## WithoutTaxIdentifierNumber

### Initialization Code

#### Example

```csharp
TaxResidencyRecordTaxResidencies value = TaxResidencyRecordTaxResidencies.FromWithoutTaxIdentifierNumber(
    new WithoutTaxIdentifierNumber
    {
        Country = Country.Ge,
        MissingTinReason = MissingTinReason.CountryHasNoTin,
    }
);
```

