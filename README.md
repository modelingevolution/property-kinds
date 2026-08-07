# ModelingEvolution.PropertyKinds

A closed, code-defined catalogue of **typed property value kinds** — and the search operators each one
supports.

A typed property value is the pair `(PropertyValueKind kind, string canonical)`. The **set of kinds is
code**; the **registry of properties that use them is data**, and lives wherever the consuming
application keeps it. Nothing in this package knows what a product, a price or an offer is.

```csharp
var catalog = PropertyKindCatalog.Default;

var mass = catalog.For(PropertyValueKind.Mass);
mass.Canonicalize("500 g", null);          // "500 g"      — the vendor's unit is preserved
mass.Format("≈40 kg", null, null);         // "≈40 kg"     — and so is the vendor's hedge

Mass.Parse("500 g").Kilograms;             // 0.5m         — but the magnitude is normalised
Mass.Parse("500 g") < Mass.Parse("1 kg");  // true
```

## Why it exists

Because a typed value that crosses a boundary as text has to be recovered from prose on the far side,
and `decimal.TryParse("380 A")` is `false`. A regular expression over `30 – 380 A` is not a query; it is
a bug waiting for a value with a space in it.

```csharp
var tested  = catalog.Facet(PropertyValueKind.Range, "30 – 380 A", null, null);
var welded  = catalog.Facet(PropertyValueKind.Range, "30 – 500 A", null, null);

tested.Supports(SearchOperator.Within);                 // true — an interval knows what it admits
tested.TryMatch(SearchOperator.Within, welded, out var inside);  // true, inside == true
```

## The kinds

| Kind | Backing type | Canonical | Facet |
|---|---|---|---|
| `Text` `LongText` `Url` | `string` | as written | `Opaque` |
| `Integer` `Decimal` | `string` | invariant, no grouping | `Scalar` |
| `Boolean` | `string` | `true` / `false` | `Choice` |
| `Enum` `EnumSet` | `string` | choice keys, comma-separated, in declared order | `Choice` |
| `Money` | `string` | `3299.00 PLN` | `Scalar` (per currency) |
| `Mass` | `Mass` | `10 kg`, `≈40 kg` | `Scalar` |
| `Length` | `Length` | `1300 mm`, `±0.03 mm` | `Scalar` |
| `Percent` | `Percent` | `60 %` | `Scalar` |
| `Current` `Voltage` `Frequency` | `Current` `Voltage` `Frequency` | `500 A`, `85 V`, `25 Hz` | `Scalar` |
| `Power` `Duration` | `Power` `Duration` | `24 kVA`, `450 ms` | `Scalar` |
| `Angle` `Speed` | `Angle` `Speed` | `±360°`, `1.5 m/s` | `Scalar` |
| `Size3D` | `Size3D` | `620 × 300 × 480 mm` | `Composite` |
| `Range` | `ValueRange` | `30 – 500 A`, `80 – 350 A+` | `Interval` |
| `ByteSize` `Size2D` | — | — | **reserved, not implemented** |

The **ordinals are the wire format and are frozen forever.** Adding a kind is additive on both sides;
reordering one silently re-types every value already stored. The package's major version bumps only if
an ordinal moves, which it never does.

## The operator model

Five shapes, because five is all the data has.

| Shape | Operators |
|---|---|
| `Scalar` | `= ≠ < ≤ > ≥`, `between` |
| `Interval` | `overlaps`, `contains`, `within` |
| `Composite` | `between` (per axis), `fits within` |
| `Choice` | `in`, `not in`, `any of`, `all of` |
| `Opaque` | `contains` (substring) |

`TryMatch` is **total**: it returns `false` when the question cannot be answered — an unsupported
operator, the wrong operand shape, two values in incompatible units, or a tolerance compared against a
magnitude — and never guesses. `±0.03 mm` of repeatability is not `0.03 mm` of reach, and 100 EUR does
not order against 100 PLN in a package that has no exchange rate.

This is not a search engine, an index or a query language. It is the statement of *which comparisons a
kind admits*, so that a later feature can build one.

## Reading a value a newer producer wrote

A producer and a consumer deploy independently, so there is a window where the producer emits a kind the
consumer does not know. Deserialising straight into the CLR enum turns that window into a parse failure,
and a parse failure on a read path takes the page down.

```csharp
var kind = PropertyKindRef.Read(yaml.Kind);   // never throws
kind.IsKnown;                                 // false for "Torque"
kind.Token;                                   // "Torque" — nameable in a log
kind.Display;                                 // "Torque"; "Unknown" when the token was blank
kind.ToString();                              // the serialised form, verbatim — round-trips exactly

catalog.Facet(kind, canonical, unit, choices) // PropertyFacet.None — refuses, never guesses
```

The row still renders: the producer emitted its own `display` string for the value, and that is what a
reader sees. What an unknown kind cannot do is participate in a facet or a comparison.

## Localised kind labels

```csharp
PropertyKindDisplay.For(PropertyValueKind.Range, "pl");   // "Zakres"
PropertyKindDisplay.For(PropertyValueKind.Size3D, "pl");  // "Wymiary"
```

These label **the kind**, for a search UI's operator picker. **They are not specification-table
headings.** A heading names a *property* — "Enclosure length" — and a property is a data row somebody can
rename without recompiling anything. An attribute cannot label a type that did not exist when the
assembly was built, which is why this package ships no language-keyed attribute for property names or
enum choice labels at all: absent, so it cannot be reached for.

## License

MIT.
