using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;

namespace ModelingEvolution.PropertyKinds;

/// <summary>One kind's descriptor — the single place a kind's real type, unit, parser, formatter and search
/// operators wire in. Pure: no I/O, no state, no ambient culture, fully unit-testable.</summary>
public interface IPropertyKind
{
    /// <summary>The catalogue member this descriptor implements.</summary>
    PropertyValueKind Kind { get; }

    /// <summary>The unit the kind's canonical form carries intrinsically ("kg", "mm", "A", "%"), or null when the
    /// kind is dimensionless and the property definition supplies a display unit label instead.</summary>
    string? DefaultUnit { get; }

    /// <summary>Whether the property definition must supply an ordered choice-list (Enum / EnumSet only).</summary>
    bool RequiresChoices { get; }

    /// <summary>Which family of comparisons this kind's values admit.</summary>
    FacetShape FacetShape { get; }

    /// <summary>The operators <see cref="FacetShape"/> admits — what a search UI may offer for this kind.</summary>
    SearchOperator Operators { get; }

    /// <summary>The pure guard behind every value write: does <paramref name="canonical"/> parse under this kind
    /// (or, for an enumeration, belong to <paramref name="choices"/>)? Never performs I/O.</summary>
    bool TryValidate(string canonical, IReadOnlyList<string>? choices, out string? error);

    /// <summary>Normalises a valid canonical to the form new values are stored in — the round-trip of the backing
    /// type's <c>Parse</c>/<c>ToString</c>. Returns the input unchanged when it does not parse (never throws).</summary>
    string Canonicalize(string canonical, IReadOnlyList<string>? choices);

    /// <summary>The render form: "10 kg", "30 – 400 A", "MIG/MAG, TIG". <paramref name="unit"/> is the property
    /// definition's display label, appended only for dimensionless kinds.</summary>
    string Format(string canonical, string? unit, IReadOnlyList<string>? choices);

    /// <summary>The value reduced to the comparisons it can answer.
    ///
    /// <para><paramref name="unit"/> is the property definition's declared unit and matters only where the kind
    /// does not carry one itself; <paramref name="choices"/> is its declared choice list and gives a
    /// <see cref="FacetShape.Choice"/> facet its <see cref="PropertyFacet.Domain"/>. Both may be null. A canonical
    /// that does not parse yields <see cref="PropertyFacet.None"/> — never a guess and never an exception.</para></summary>
    PropertyFacet Facet(string canonical, string? unit, IReadOnlyList<string>? choices);
}

/// <summary>A kind was asked for that this build cannot serve — either RESERVED (an ordinal held for a future
/// implementation) or UNKNOWN (a corrupt value, or one written by a newer producer).
///
/// <para>A consumer that must not fail — a public page reading an artifact a newer writer produced — should not be
/// catching this: it should be reading through <see cref="PropertyKindRef"/> and
/// <see cref="PropertyKindCatalog.Facet(PropertyKindRef,string,string?,System.Collections.Generic.IReadOnlyList{string})"/>,
/// which are total. This exception is for the WRITE side, where an unusable kind is a rejection somebody must
/// see.</para></summary>
public sealed class PropertyKindException(string message, PropertyValueKind kind, bool isReserved)
    : Exception(message)
{
    /// <summary>The kind that could not be served.</summary>
    public PropertyValueKind Kind { get; } = kind;

    /// <summary>True when the ordinal is permanently reserved but not yet implemented; false when it is not a
    /// member of this build's catalogue at all.</summary>
    public bool IsReserved { get; } = isReserved;
}

/// <summary>A <see cref="PropertyValueKind"/> AS READ FROM A WIRE that a newer producer may have written.
///
/// <para><b>Why this exists.</b> A producer and a consumer of typed values deploy independently, so there is
/// guaranteed to be a window in which the producer emits a kind the consumer's copy of this package does not know
/// — the day a kind is added, which is the whole point of the catalogue being extensible. Deserialising straight
/// into the CLR enum turns that window into a parse failure, and a parse failure on a read path takes the page
/// down. Trading one defect for a worse one.</para>
///
/// <para>So the typed read is TOTAL. An unrecognised token lands here as <see cref="Unknown"/> carrying the raw
/// text: it still RENDERS (a producer that emitted the value also emitted its own rendering of it), it never
/// produces a facet or a comparison, and the consumer logs it once as the signal that it is behind the
/// producer.</para></summary>
[System.Text.Json.Serialization.JsonConverter(
    typeof(ModelingEvolution.JsonParsableConverter.JsonParsableConverter<PropertyKindRef>))]
public readonly record struct PropertyKindRef : IParsable<PropertyKindRef>
{
    /// <summary>What an unrecognised token reads as. Carries no kind and an empty token, and is what
    /// <c>default(PropertyKindRef)</c> is — a field nobody assigned is honestly unknown, not accidentally
    /// <see cref="PropertyValueKind.Text"/>.</summary>
    public static readonly PropertyKindRef Unknown = default;

    private readonly string? _token;

    private PropertyKindRef(PropertyValueKind? known, string? token)
    {
        Known = known;
        _token = token;
    }

    /// <summary>The kind, when this build knows it; null otherwise.</summary>
    public PropertyValueKind? Known { get; }

    /// <summary>Exactly the text the wire carried, so an unrecognised kind can be NAMED in a log rather than
    /// reported as "something we did not understand". Empty on <see cref="Unknown"/> itself.</summary>
    public string Token => _token ?? "";

    /// <summary>Whether this build knows the kind.</summary>
    public bool IsKnown => Known.HasValue;

    /// <summary>Whether this is <paramref name="kind"/>. False for anything unknown, without the caller having to
    /// null-check first.</summary>
    public bool Is(PropertyValueKind kind) => Known == kind;

    /// <summary>A reference to a kind this build knows.</summary>
    public static PropertyKindRef Of(PropertyValueKind kind) => new(kind, kind.ToString());

    /// <summary>Reads a token TOTALLY: a member name (case-insensitively) or its ordinal becomes a known kind;
    /// anything else — including null and blank — becomes <see cref="Unknown"/> carrying the raw text. Never
    /// throws.</summary>
    public static PropertyKindRef Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return Unknown;
        var text = token.Trim();
        // A NUMERIC token must still name a DECLARED member: Enum.TryParse accepts any integer for any enum, so
        // "99" would otherwise become a "known" kind with no member, which is worse than an honest Unknown.
        if (Enum.TryParse<PropertyValueKind>(text, ignoreCase: true, out var kind) && Enum.IsDefined(kind))
            return new PropertyKindRef(kind, text);
        return new PropertyKindRef(null, text);
    }

    /// <summary>Something to print when there is nothing to print — the token, or <c>"Unknown"</c> when the wire
    /// carried none. Deliberately NOT <see cref="ToString"/>: that one is the serialised form and must round-trip
    /// exactly, and a blank kind that came back as the word "Unknown" would not be the value that was read.</summary>
    public string Display => Token.Length == 0 ? "Unknown" : Token;

    /// <summary>The token, VERBATIM — this is the serialised form, so reading it back yields the same value it was
    /// written from, blank included. For something a human should look at, use <see cref="Display"/>.</summary>
    public override string ToString() => Token;

    /// <summary>Total, like <see cref="Read"/> — this never throws, because an unrecognised kind is a state, not a
    /// failure.</summary>
    public static PropertyKindRef Parse(string s, IFormatProvider? provider = null) => Read(s);

    /// <summary>Total, like <see cref="Read"/>: always returns true, with <paramref name="result"/> set to
    /// <see cref="Unknown"/> when the token names nothing.</summary>
    public static bool TryParse(string? s, IFormatProvider? provider, out PropertyKindRef result)
    {
        result = Read(s);
        return true;
    }
}

/// <summary>The <see cref="PropertyValueKind"/> → <see cref="IPropertyKind"/> map. Stateless and pure, so
/// <see cref="Default"/> is safe to use from anywhere — an aggregate a framework constructs without DI, a static
/// helper, a background scan — while the same instance is registered as the DI singleton a UI injects.</summary>
public sealed class PropertyKindCatalog
{
    /// <summary>The single shared instance. Everything reaches for this directly; DI hands out the same object.</summary>
    public static readonly PropertyKindCatalog Default = new();

    private readonly FrozenDictionary<PropertyValueKind, IPropertyKind> _kindByValue;
    private readonly IPropertyKind[] _all;

    /// <summary>Builds the catalogue. There is nothing to configure — prefer <see cref="Default"/>.</summary>
    public PropertyKindCatalog()
    {
        IPropertyKind[] kinds =
        [
            // ── primitives (dimensionless: the property definition's unit LABEL is appended on render) ───────────
            new ParsedKind(PropertyValueKind.Text, null, false, FacetShape.Opaque, NonBlank),
            new ParsedKind(PropertyValueKind.LongText, null, false, FacetShape.Opaque, NonBlank),
            // NO AllowThousands, and any decimal/group separator is REJECTED outright. Under a pl-PL culture a user
            // types "1,5" meaning one-and-a-half; with AllowThousands the invariant parser reads that comma as
            // grouping and silently stores 15 — a 10× corruption from one keystroke, while the Decimal kind reads
            // the SAME keystroke as 1.5. An integer property must reject it, not guess.
            new ParsedKind(PropertyValueKind.Integer, null, false, FacetShape.Scalar, ParseInteger),
            new ParsedKind(PropertyValueKind.Decimal, null, false, FacetShape.Scalar, s =>
                UnitFormat.TryNumber(s, null, out var v) ? UnitFormat.Number(v) : null),
            new ParsedKind(PropertyValueKind.Boolean, null, false, FacetShape.Choice, ParseBoolean, FormatBoolean),
            new ParsedKind(PropertyValueKind.Url, null, false, FacetShape.Opaque, s =>
                Uri.TryCreate(s?.Trim(), UriKind.Absolute, out var u)
                && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps) ? u.ToString() : null),

            // ── enumerations — choices are DATA on the property definition ───────────────────────────────────────
            new ChoiceKind(PropertyValueKind.Enum, multi: false),
            new ChoiceKind(PropertyValueKind.EnumSet, multi: true),

            // ── money (a money-typed SPEC attribute, never a node's price) ───────────────────────────────────────
            // A display formatter would put a pl-PL grouped, comma-decimal string in the same `canonical` field
            // every other kind fills with an invariant dot form — a consumer parsing it invariantly reads
            // 3 299,00 as 329900 — and would leave a public page unable to name the currency. So this row
            // canonicalises INVARIANT and ALWAYS currency-qualified: "3299.00 PLN".
            new ParsedKind(PropertyValueKind.Money, null, true, FacetShape.Scalar, ParseMoney),

            // ── the house value-classes (Dimensions.cs). Each row's unit and facet shape come from the type's own
            //    [PropertyKind] attribute, so the two cannot drift. ByteSize/Size2D are RESERVED and deliberately
            //    absent — see the enum. ────────────────────────────────────────────────────────────────────────
            ValueKind.Of<Mass>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Mass, v.Kilograms, Mass.BaseUnit, v.Qualifier)),
            ValueKind.Of<Length>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Length, v.Millimeters, Length.BaseUnit, v.Qualifier)),
            ValueKind.Of<Percent>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Percent, v.Value, Percent.Unit, v.Qualifier)),
            // A range's unit comes from the PROPERTY DEFINITION and is NOT normalised, so it travels on the facet
            // and amperes are never silently compared with volts. The canonical usually repeats it ("30 – 500 A");
            // when it does not, the definition's declared label IS the unit, and dropping it here would make two
            // bare intervals in different dimensions look comparable — the exact coercion this refuses elsewhere.
            ValueKind.Of<ValueRange>((v, unit) => PropertyFacet.OfInterval(PropertyValueKind.Range,
                v.Unit is null && !string.IsNullOrWhiteSpace(unit) ? v with { Unit = unit.Trim() } : v)),
            ValueKind.Of<Current>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Current, v.Amperes, Current.Unit, v.Qualifier)),
            ValueKind.Of<Voltage>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Voltage, v.Volts, Voltage.Unit, v.Qualifier)),
            ValueKind.Of<Frequency>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Frequency, v.Hertz, Frequency.Unit, v.Qualifier)),
            ValueKind.Of<Size3D>((v, _) =>
                PropertyFacet.OfComposite(PropertyValueKind.Size3D, [v.X, v.Y, v.Z], v.Unit)),
            ValueKind.Of<Power>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Power, v.KiloVoltAmperes, Power.Unit, v.Qualifier)),
            ValueKind.Of<Duration>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Duration, v.Milliseconds, Duration.Unit, v.Qualifier)),
            ValueKind.Of<Angle>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Angle, v.Degrees, Angle.Unit, v.Qualifier)),
            ValueKind.Of<Speed>((v, _) =>
                PropertyFacet.OfScalar(PropertyValueKind.Speed, v.MetresPerSecond, Speed.Unit, v.Qualifier)),
        ];
        _all = kinds;
        _kindByValue = kinds.ToFrozenDictionary(k => k.Kind);
    }

    /// <summary>Ordinals that are permanently RESERVED but not yet implemented. Declaring them freezes the wire
    /// format without taking a dependency nothing uses; a value cannot be stored under one until somebody
    /// implements it against a real requirement.</summary>
    public static readonly IReadOnlySet<PropertyValueKind> Reserved = new HashSet<PropertyValueKind>
    {
        PropertyValueKind.ByteSize, PropertyValueKind.Size2D
    }.ToFrozenSet();

    /// <summary>The descriptor for a kind. A RESERVED kind and an unknown ordinal both fail LOUDLY and distinctly —
    /// a reserved kind is a "not built yet" answer, an unknown ordinal is a corrupt or future wire value.
    ///
    /// <para>This is the WRITE side's accessor. A read path that must survive a newer producer uses
    /// <see cref="TryGet"/> or <see cref="Facet(PropertyKindRef,string,string?,IReadOnlyList{string})"/>.</para></summary>
    /// <exception cref="PropertyKindException">The kind is reserved, or is not a member of this build.</exception>
    public IPropertyKind For(PropertyValueKind kind)
    {
        if (_kindByValue.TryGetValue(kind, out var k)) return k;
        if (Reserved.Contains(kind))
            throw new PropertyKindException(
                $"Property value kind '{kind}' is reserved but not yet supported. Its ordinal ({(int)kind}) is held " +
                $"for a future implementation — implement its IPropertyKind descriptor before using it.",
                kind, isReserved: true);
        throw new PropertyKindException(
            $"Unknown property value kind '{kind}' (ordinal {(int)kind}).", kind, isReserved: false);
    }

    /// <summary>The descriptor for a kind, or false for a reserved or unknown one. The total counterpart to
    /// <see cref="For"/>, for a read path that must not throw.</summary>
    public bool TryGet(PropertyValueKind kind, out IPropertyKind descriptor) =>
        _kindByValue.TryGetValue(kind, out descriptor!);

    /// <summary>Whether a kind can actually carry a value today (i.e. it is registered, not merely reserved).</summary>
    public bool IsSupported(PropertyValueKind kind) => _kindByValue.ContainsKey(kind);

    /// <summary>Every descriptor, in catalogue order — a kind picker's source.</summary>
    public IReadOnlyList<IPropertyKind> All => _all;

    /// <summary>The facet for a value of a known kind. Returns <see cref="PropertyFacet.None"/> for a reserved kind
    /// or a canonical that does not parse — never throws.</summary>
    public PropertyFacet Facet(PropertyValueKind kind, string canonical, string? unit,
        IReadOnlyList<string>? choices) =>
        TryGet(kind, out var descriptor) ? descriptor.Facet(canonical, unit, choices) : PropertyFacet.None;

    /// <summary>The facet for a value read off a wire — <b>the total entry point</b>. A kind this build does not
    /// know yields <see cref="PropertyFacet.None"/>, so an unrecognised kind refuses every comparison instead of
    /// answering one from a type nobody understands.</summary>
    public PropertyFacet Facet(PropertyKindRef kind, string canonical, string? unit,
        IReadOnlyList<string>? choices) =>
        kind.Known is { } known ? Facet(known, canonical, unit, choices) : PropertyFacet.None;

    private static string? NonBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>A whole number, invariant, with NO separator tolerance — see the catalogue row for why.</summary>
    private static string? ParseInteger(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var cleaned = s.Trim().Replace(" ", "").Replace(" ", "").Replace(" ", "");
        // A ',' or '.' means the user meant a fraction (or a grouping we refuse to guess at) — reject, never round.
        if (cleaned.Contains(',') || cleaned.Contains('.')) return null;
        return long.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>The currencies a Money canonical may name. A LITERAL list, not a reference to somebody's
    /// <c>Currency</c> enum: this package models units, and taking a dependency on a consumer's domain to spell
    /// five ISO 4217 codes would be exactly the coupling it exists to avoid. Widening it is additive and safe —
    /// a code added here was previously rejected, never mis-read.</summary>
    private static readonly string[] Currencies = ["PLN", "EUR", "USD", "GBP", "CHF"];

    /// <summary>The domestic default when a Money canonical names no currency. Poland is where the values this
    /// package was extracted from are quoted; an explicit ISO code always wins.</summary>
    private const string DefaultCurrency = "PLN";

    /// <summary>Invariant, always currency-qualified: "3299.00 PLN", "10390.00 EUR". Accepts a pl-PL display form
    /// (<c>"10 390,00 EUR"</c>) on the way in so a value pasted from a UI still lands, but never stores it.</summary>
    private static string? ParseMoney(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var text = s.Trim();

        var currency = DefaultCurrency;
        foreach (var code in Currencies)
        {
            if (text.Length > code.Length && text.EndsWith(code, StringComparison.OrdinalIgnoreCase))
            {
                currency = code;
                text = text[..^code.Length];
                break;
            }
        }

        text = text.Replace("zł", "", StringComparison.OrdinalIgnoreCase);
        // UnitFormat normalises pl grouping/comma then parses invariant — the same rule every other kind uses, so
        // one keystroke cannot mean two different numbers across two kinds.
        return UnitFormat.TryNumber(text, null, out var amount)
            ? $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}"
            : null;
    }

    /// <summary>A money facet compares WITHIN a currency: the unit is the ISO code, so 100 EUR is never ordered
    /// against 100 PLN by a package that has no rate and no business inventing one.</summary>
    private static PropertyFacet MoneyFacet(string canonical)
    {
        var parts = canonical.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return PropertyFacet.None;
        return decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? PropertyFacet.OfScalar(PropertyValueKind.Money, amount, parts[1])
            : PropertyFacet.None;
    }

    private static string? ParseBoolean(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "true" or "yes" or "1" or "tak" => "true",
        "false" or "no" or "0" or "nie" => "false",
        _ => null
    };

    private static string FormatBoolean(string canonical, string? unit) => canonical == "true" ? "Yes" : "No";

    /// <summary>The facet a dimensionless or primitive kind produces from its NORMALISED canonical. Kept beside the
    /// catalogue rows rather than inside <see cref="ParsedKind"/> so every row's whole behaviour reads in one place.</summary>
    internal static PropertyFacet PrimitiveFacet(PropertyValueKind kind, string normalized, string? unit) =>
        kind switch
        {
            PropertyValueKind.Integer or PropertyValueKind.Decimal =>
                decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                    ? PropertyFacet.OfScalar(kind, number, unit)
                    : PropertyFacet.None,
            // Boolean is the one Choice kind whose domain IS known at compile time, so it always has one.
            PropertyValueKind.Boolean => PropertyFacet.OfChoice(kind, [normalized], ["true", "false"]),
            PropertyValueKind.Money => MoneyFacet(normalized),
            PropertyValueKind.Text or PropertyValueKind.LongText or PropertyValueKind.Url =>
                PropertyFacet.OfText(kind, normalized),
            _ => PropertyFacet.None,
        };
}

/// <summary>A kind backed by a delegate: validity IS "the delegate normalises it", and the canonical form is what
/// the delegate returns. One row per kind — the whole reuse-or-author decision lives in the catalogue above.</summary>
internal sealed class ParsedKind(
    PropertyValueKind kind,
    string? defaultUnit,
    bool unitIsIntrinsic,
    FacetShape facetShape,
    Func<string?, string?> normalize,
    Func<string, string?, string>? format = null) : IPropertyKind
{
    public PropertyValueKind Kind => kind;
    public string? DefaultUnit => defaultUnit;
    public bool RequiresChoices => false;
    public FacetShape FacetShape => facetShape;
    public SearchOperator Operators => FacetOperators.For(facetShape);

    public bool TryValidate(string canonical, IReadOnlyList<string>? choices, out string? error)
    {
        if (choices is { Count: > 0 })
        {
            error = $"{kind} does not take a choice list.";
            return false;
        }
        if (normalize(canonical) is null)
        {
            error = $"'{canonical}' is not a valid {kind}.";
            return false;
        }
        error = null;
        return true;
    }

    public string Canonicalize(string canonical, IReadOnlyList<string>? choices) => normalize(canonical) ?? canonical;

    public string Format(string canonical, string? unit, IReadOnlyList<string>? choices)
    {
        var normalized = normalize(canonical) ?? canonical;
        if (format is not null) return format(normalized, unit);
        // A dimensioned kind already carries its unit in the canonical form ("10 kg"); a dimensionless one takes
        // the property definition's display label ("1300" + "mm").
        return unitIsIntrinsic || string.IsNullOrWhiteSpace(unit) ? normalized : $"{normalized} {unit}";
    }

    public PropertyFacet Facet(string canonical, string? unit, IReadOnlyList<string>? choices) =>
        normalize(canonical) is { } normalized
            ? PropertyKindCatalog.PrimitiveFacet(kind, normalized, unit)
            : PropertyFacet.None;
}

/// <summary>Factory for <see cref="ValueKind{T}"/>, so a catalogue row reads
/// <c>ValueKind.Of&lt;Mass&gt;(…)</c> rather than restating the type twice.</summary>
internal static class ValueKind
{
    public static IPropertyKind Of<T>(Func<T, string?, PropertyFacet> facet) where T : struct, IParsable<T> =>
        new ValueKind<T>(facet);
}

/// <summary>A kind backed by a real value type: validity IS "the backing type parses it", the canonical form is
/// that type's <c>ToString</c>, and the unit and facet shape come from the type's own
/// <see cref="PropertyKindAttribute"/> — read once here so the catalogue row and the attribute cannot disagree.</summary>
internal sealed class ValueKind<T> : IPropertyKind where T : struct, IParsable<T>
{
    private readonly Func<T, string?, PropertyFacet> _facet;
    private readonly bool _unitIsIntrinsic;

    public ValueKind(Func<T, string?, PropertyFacet> facet)
    {
        _facet = facet;
        var declaration = typeof(T).GetCustomAttribute<PropertyKindAttribute>()
            ?? throw new InvalidOperationException(
                $"{typeof(T).Name} backs a catalogue row but carries no [PropertyKind] attribute, so its unit and "
                + "facet shape are undeclared.");
        Kind = declaration.Kind;
        DefaultUnit = declaration.DefaultUnit;
        FacetShape = declaration.Facet;
        _unitIsIntrinsic = declaration.UnitIsIntrinsic;
    }

    public PropertyValueKind Kind { get; }
    public string? DefaultUnit { get; }
    public bool RequiresChoices => false;
    public FacetShape FacetShape { get; }
    public SearchOperator Operators => FacetOperators.For(FacetShape);

    public bool TryValidate(string canonical, IReadOnlyList<string>? choices, out string? error)
    {
        if (choices is { Count: > 0 })
        {
            error = $"{Kind} does not take a choice list.";
            return false;
        }
        if (!T.TryParse(canonical, null, out _))
        {
            error = $"'{canonical}' is not a valid {Kind}.";
            return false;
        }
        error = null;
        return true;
    }

    public string Canonicalize(string canonical, IReadOnlyList<string>? choices) =>
        T.TryParse(canonical, null, out var value) ? value.ToString() ?? canonical : canonical;

    public string Format(string canonical, string? unit, IReadOnlyList<string>? choices)
    {
        var normalized = Canonicalize(canonical, choices);
        return _unitIsIntrinsic || string.IsNullOrWhiteSpace(unit) ? normalized : $"{normalized} {unit}";
    }

    public PropertyFacet Facet(string canonical, string? unit, IReadOnlyList<string>? choices) =>
        T.TryParse(canonical, null, out var value) ? _facet(value, unit) : PropertyFacet.None;
}

/// <summary>An enumeration kind whose choices are DATA on the property definition. <c>Enum</c> holds one choice
/// key; <c>EnumSet</c> holds several as a CSV, re-ordered to the choice-list's own order so the canonical form is
/// STABLE regardless of the order they were ticked in.</summary>
internal sealed class ChoiceKind(PropertyValueKind kind, bool multi) : IPropertyKind
{
    public PropertyValueKind Kind => kind;
    public string? DefaultUnit => null;
    public bool RequiresChoices => true;
    public FacetShape FacetShape => FacetShape.Choice;
    public SearchOperator Operators => FacetOperators.Choice;

    public bool TryValidate(string canonical, IReadOnlyList<string>? choices, out string? error)
    {
        if (choices is null || choices.Count == 0)
        {
            error = $"{kind} requires the property definition's choice list.";
            return false;
        }
        var tokens = ChoiceTokens.Split(canonical);
        if (tokens.Count == 0)
        {
            error = "No choice selected.";
            return false;
        }
        if (!multi && tokens.Count > 1)
        {
            error = $"{kind} accepts exactly one choice.";
            return false;
        }
        foreach (var t in tokens)
            if (!Contains(choices, t))
            {
                error = $"'{t}' is not one of the property's choices.";
                return false;
            }
        if (multi && tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count() != tokens.Count)
        {
            error = "A choice may only be selected once.";
            return false;
        }
        error = null;
        return true;
    }

    public string Canonicalize(string canonical, IReadOnlyList<string>? choices)
    {
        var tokens = ChoiceTokens.Split(canonical);
        if (choices is null || choices.Count == 0) return string.Join(", ", tokens);
        // Re-order to the declared choice order and adopt the declared casing — the canonical form must not depend
        // on the order the boxes were ticked in (CI-1: the same selection is always the same string).
        var ordered = choices.Where(c => tokens.Any(t => string.Equals(t, c, StringComparison.OrdinalIgnoreCase)));
        return string.Join(", ", ordered);
    }

    public string Format(string canonical, string? unit, IReadOnlyList<string>? choices) =>
        Canonicalize(canonical, choices);

    public PropertyFacet Facet(string canonical, string? unit, IReadOnlyList<string>? choices) =>
        PropertyFacet.OfChoice(kind, ChoiceTokens.Split(Canonicalize(canonical, choices)), choices);

    private static bool Contains(IReadOnlyList<string> choices, string token)
    {
        for (var i = 0; i < choices.Count; i++)
            if (string.Equals(choices[i], token, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

/// <summary>THE tokeniser for an <see cref="PropertyValueKind.Enum"/> / <see cref="PropertyValueKind.EnumSet"/>
/// canonical, public because a consumer that substitutes localised labels token-by-token MUST split exactly as the
/// kind does.
///
/// <para>A choice key may itself contain a slash — <c>"MIG/MAG"</c> — so the separator is a COMMA, never a slash. A
/// second tokeniser somewhere else would be one slash-split away from cutting <c>MIG/MAG</c> in half; there is one,
/// and this is it.</para></summary>
public static class ChoiceTokens
{
    /// <summary>The tokens of a choice canonical, trimmed, empty entries dropped. Never null.</summary>
    public static IReadOnlyList<string> Split(string? canonical) =>
        string.IsNullOrWhiteSpace(canonical)
            ? []
            : canonical.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
