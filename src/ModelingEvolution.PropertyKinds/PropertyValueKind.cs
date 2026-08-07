namespace ModelingEvolution.PropertyKinds;

// =============================================================================
//  The strong-typing backbone: a CLOSED, CODE-DEFINED catalogue of value kinds.
//
//  The crux: a typed property value persists as the self-describing pair
//  (PropertyValueKind kind, string canonical). The SET of kinds is CODE (compile-time type-safety
//  per kind); the REGISTRY of properties that USE a kind is the consumer's business and is
//  typically data. Nothing in this package knows what a product, a price or an offer is.
//
//  CI-1 — the wire shape is FROZEN FOREVER. Adding a kind is purely additive: author/reuse a value
//  type, append ONE enum member (NEVER reorder — consumers serialise these as INTEGERS), wire ONE
//  catalogue row. Zero schema churn. A kind's TryValidate must never reject a canonical it once
//  accepted; it may only widen.
//
//  This file was extracted from the RocketWelder ERP (epic-067 F2) so that the ERP that WRITES a
//  typed value and the website that READS one agree on the type by compiling against it, rather
//  than by re-parsing each other's prose. See docs/epics/epic-069-website-configurator/
//  design-typed-property-contract.md §6.
// =============================================================================

/// <summary>The closed catalogue of value kinds a typed property can carry. <b>APPEND-ONLY — never reorder or reuse
/// an ordinal</b>: consumers serialize these as integers on the wire, so moving one silently re-types every value
/// already stored.</summary>
public enum PropertyValueKind
{
    // Primitives (reused — no new value type needed).

    /// <summary>A single line of free text.</summary>
    [KindDisplay("Text", "en"), KindDisplay("Tekst", "pl"), KindDisplay("Text", "de")]
    Text = 0,

    /// <summary>Multi-line free text.</summary>
    [KindDisplay("Long text", "en"), KindDisplay("Tekst długi", "pl"), KindDisplay("Langtext", "de")]
    LongText = 1,

    /// <summary>A whole number, with NO separator tolerance — see <see cref="PropertyKindCatalog"/> for why.</summary>
    [KindDisplay("Whole number", "en"), KindDisplay("Liczba całkowita", "pl"), KindDisplay("Ganzzahl", "de")]
    Integer = 2,

    /// <summary>A dimensionless decimal; the property supplies the unit LABEL.</summary>
    [KindDisplay("Number", "en"), KindDisplay("Liczba", "pl"), KindDisplay("Dezimalzahl", "de")]
    Decimal = 3,

    /// <summary>A yes/no fact.</summary>
    [KindDisplay("Yes / no", "en"), KindDisplay("Tak / nie", "pl"), KindDisplay("Ja / nein", "de")]
    Boolean = 4,

    /// <summary>An absolute http/https URL.</summary>
    [KindDisplay("Link", "en"), KindDisplay("Odnośnik", "pl"), KindDisplay("Link", "de")]
    Url = 5,

    // Enumerations — the CHOICES ARE DATA on the property definition, NOT a CLR enum (so a new welding process is an
    // admin edit, not a code change). This is the one deliberate asymmetry in the catalogue.

    /// <summary>Exactly one choice from a list the property definition carries as DATA.</summary>
    [KindDisplay("Choice", "en"), KindDisplay("Wybór", "pl"), KindDisplay("Auswahl", "de")]
    Enum = 6,

    /// <summary>Several choices from a list the property definition carries as DATA.</summary>
    [KindDisplay("Multiple choice", "en"), KindDisplay("Wybór wielokrotny", "pl"), KindDisplay("Mehrfachauswahl", "de")]
    EnumSet = 7,

    /// <summary>A money-typed SPEC attribute. NOT a node's price — a price is priced by whatever prices things, and
    /// is never mastered here.</summary>
    [KindDisplay("Amount", "en"), KindDisplay("Kwota", "pl"), KindDisplay("Betrag", "de")]
    Money = 8,

    // ── RESERVED, NOT IMPLEMENTED ────────────────────────────────────────────────────────────────────────────────
    // No value in the catalogue that motivated this package needs either: it holds zero byte-size values, and every
    // dimension it holds is a TRIPLE ("620 × 300 × 480 mm"), which a 2-D size cannot represent — that need is served
    // by Size3D below. Taking a package reference for them would be a speculative dependency. But the ordinals ARE
    // the wire format and freeze the moment the first value is stored, so the members STAY DECLARED to reserve them
    // permanently. They are deliberately NOT registered: using one fails loudly with "reserved but not yet
    // supported". Whoever needs one implements it then, against a real requirement — the ordinal is waiting.

    /// <summary><b>RESERVED, not implemented.</b> A size in bytes. The ordinal is held so the wire format never
    /// shifts; <see cref="PropertyKindCatalog.For"/> rejects it explicitly rather than silently.</summary>
    [KindDisplay("Data size", "en"), KindDisplay("Rozmiar danych", "pl"), KindDisplay("Datengröße", "de")]
    ByteSize = 9,

    // Angle and Speed ARE implemented (Dimensions.cs), but NOT over Drawing's Degree<double>/Speed<double>: those
    // format through the ambient culture and cannot parse their own output under a pl-PL culture, and Speed<T> is
    // hard-wired to mm/min so it cannot express m/s at all. Implementing them directly keeps the canonical invariant
    // and costs no package reference.

    /// <summary>An angle in degrees — a joint's ±360° vs ±175° limit is one whole SKU's difference.</summary>
    [KindDisplay("Angle", "en"), KindDisplay("Kąt", "pl"), KindDisplay("Winkel", "de")]
    Angle = 10,

    /// <summary>A linear speed in metres per second.</summary>
    [KindDisplay("Speed", "en"), KindDisplay("Prędkość", "pl"), KindDisplay("Geschwindigkeit", "de")]
    Speed = 11,

    /// <summary><b>RESERVED, not implemented.</b> A two-component size. Every dimension the motivating catalogue
    /// holds is a triple, so <see cref="Size3D"/> serves it; the ordinal is held for whoever needs 2-D.</summary>
    [KindDisplay("Size (2D)", "en"), KindDisplay("Wymiary (2D)", "pl"), KindDisplay("Größe (2D)", "de")]
    Size2D = 12,

    // ── House value-classes (Dimensions.cs) — nothing existing fits these engineering dimensions ──────────────────

    /// <summary>A mass in kilograms — a payload, a shipping weight.</summary>
    [KindDisplay("Mass", "en"), KindDisplay("Masa", "pl"), KindDisplay("Masse", "de")]
    Mass = 13,

    /// <summary>A length in millimetres — a reach, a tolerance-aware repeatability, a cable run.</summary>
    [KindDisplay("Length", "en"), KindDisplay("Długość", "pl"), KindDisplay("Länge", "de")]
    Length = 14,

    /// <summary>A percentage, 0–100 — a duty cycle, an efficiency, a power factor.</summary>
    [KindDisplay("Percentage", "en"), KindDisplay("Procent", "pl"), KindDisplay("Prozent", "de")]
    Percent = 15,

    /// <summary>ANY numeric interval with a unit: 30 – 500 A, 0 – 45 °C, 45 – 65 Hz, 150 – 250 ms. The unit comes
    /// from the PROPERTY DEFINITION (data), not from the kind — which is why two ranges in different units are
    /// refused a comparison rather than coerced into one.</summary>
    [KindDisplay("Range", "en"), KindDisplay("Zakres", "pl"), KindDisplay("Bereich", "de")]
    Range = 16,

    /// <summary>A current in amperes — the decisive welding scalar.</summary>
    [KindDisplay("Current", "en"), KindDisplay("Prąd", "pl"), KindDisplay("Stromstärke", "de")]
    Current = 17,

    /// <summary>A voltage in volts.</summary>
    [KindDisplay("Voltage", "en"), KindDisplay("Napięcie", "pl"), KindDisplay("Spannung", "de")]
    Voltage = 18,

    /// <summary>A frequency in hertz.</summary>
    [KindDisplay("Frequency", "en"), KindDisplay("Częstotliwość", "pl"), KindDisplay("Frequenz", "de")]
    Frequency = 19,

    /// <summary>A three-component size in the vendor's stated order — enclosure dimensions are always a triple.</summary>
    [KindDisplay("Dimensions", "en"), KindDisplay("Wymiary", "pl"), KindDisplay("Abmessungen", "de")]
    Size3D = 20,

    /// <summary>An apparent power in kVA.</summary>
    [KindDisplay("Power", "en"), KindDisplay("Moc", "pl"), KindDisplay("Leistung", "de")]
    Power = 21,

    /// <summary>A duration in milliseconds.</summary>
    [KindDisplay("Duration", "en"), KindDisplay("Czas", "pl"), KindDisplay("Dauer", "de")]
    Duration = 22,
}
