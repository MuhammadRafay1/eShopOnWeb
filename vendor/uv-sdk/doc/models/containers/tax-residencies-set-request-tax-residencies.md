
# Tax Residencies Set Request Tax Residencies

## Class Name

`TaxResidenciesSetRequestTaxResidencies`

## Cases

| Type | Factory Method |
|  --- | --- |
| [`WithTaxIdentifierNumber`](../../../doc/models/with-tax-identifier-number.md) | TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(WithTaxIdentifierNumber withTaxIdentifierNumber) |
| [`WithoutTaxIdentifierNumber`](../../../doc/models/without-tax-identifier-number.md) | TaxResidenciesSetRequestTaxResidencies.FromWithoutTaxIdentifierNumber(WithoutTaxIdentifierNumber withoutTaxIdentifierNumber) |

## WithTaxIdentifierNumber

### Initialization Code

#### Example

```csharp
TaxResidenciesSetRequestTaxResidencies value = TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
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
TaxResidenciesSetRequestTaxResidencies value = TaxResidenciesSetRequestTaxResidencies.FromWithoutTaxIdentifierNumber(
    new WithoutTaxIdentifierNumber
    {
        Country = Country.Ge,
        MissingTinReason = MissingTinReason.CountryHasNoTin,
    }
);
```

