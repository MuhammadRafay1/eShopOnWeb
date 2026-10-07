
# Allocation Instrument Id

## Class Name

`AllocationInstrumentId`

## Cases

| Type | Factory Method |
|  --- | --- |
| `string` | AllocationInstrumentId.FromString(string mString) |
| `Guid` | AllocationInstrumentId.FromUUID(Guid uuid) |

## string

### Initialization Code

#### Example

```csharp
AllocationInstrumentId value = AllocationInstrumentId.FromString("String0");
```

## Guid

### Initialization Code

#### Example

```csharp
AllocationInstrumentId value = AllocationInstrumentId.FromUUID(new Guid("00000000-0000-0000-0000-000000000000"));
```

