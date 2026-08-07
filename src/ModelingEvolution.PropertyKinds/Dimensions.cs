using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using ModelingEvolution.JsonParsableConverter;

namespace ModelingEvolution.PropertyKinds;

// =============================================================================
//  The house value-classes backing the dimensioned PropertyValueKinds.
//  Authored per the `writing-value-class` skill: readonly record struct : IParsable<T>,
//  [JsonConverter(JsonParsableConverter<T>)], unit-aware ToString, tolerant TryParse
//  (Polish decimal comma + non-breaking spaces).
//
//  CI-1 (serialization stable forever): a typed value persists as the CANONICAL STRING these types
//  produce. TryParse must therefore NEVER stop accepting a form it once accepted — only widen it.
//  ToString is the canonical form new values are written in.
//
//  THE QUALIFIER RULE (the highest-consequence decision here). Vendor datasheets hedge:
//  "±0.05 mm", "≈40 kg", "350 A+". Stripping the marker turns a hedge into a hard claim the
//  vendor DECLINED to make — on a public configurator that is a spec misrepresentation. So every
//  scalar carries a ValueQualifier that ROUND-TRIPS in the canonical string. The qualifier is
//  encoded IN the canonical, never as its own wire field, so supporting one costs no schema change.
// =============================================================================

/// <summary>How precisely a scalar is claimed. Encoded in the canonical string (never a separate wire field), so the
/// vendor's own hedge survives storage and render. <see cref="Exact"/> is the default and prints nothing.</summary>
public enum ValueQualifier
{
    /// <summary>The value as stated: "1300 mm".</summary>
    Exact,
    /// <summary>A symmetric tolerance: "±0.05 mm" — a repeatability, not a distance.</summary>
    Tolerance,
    /// <summary>An approximation the vendor declined to sharpen: "≈40 kg".</summary>
    Approximate,
    /// <summary>An open-ended lower bound: "350 A+".</summary>
    AtLeast,
    /// <summary>An upper bound: "≤5 ms".</summary>
    AtMost,
}

/// <summary>A mass in kilograms — <see cref="PropertyValueKind.Mass"/> (a cobot's payload).
/// Canonical: <c>"10 kg"</c>, <c>"≈40 kg"</c>.</summary>
[PropertyKind(PropertyValueKind.Mass, DefaultUnit = Mass.BaseUnit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Mass>))]
public readonly record struct Mass(
    decimal Kilograms, ValueQualifier Qualifier = ValueQualifier.Exact, string Unit = Mass.BaseUnit)
    : IParsable<Mass>, IComparable<Mass>
{
    /// <summary>The unit the magnitude is normalised to, so two masses written differently still compare.</summary>
    public const string BaseUnit = "kg";
    private static readonly (string Suffix, decimal ToBase)[] Units = [("kg", 1m), ("t", 1000m), ("g", 0.001m)];

    /// <summary>Zero kilograms.</summary>
    public static readonly Mass Zero = new(0m);

    public static implicit operator decimal(Mass m) => m.Kilograms;
    public static explicit operator Mass(decimal kilograms) => new(kilograms);

    // Comparison is ALWAYS on the base magnitude, so 500 g and 0.5 kg compare equal however they were written.
    public int CompareTo(Mass other) => Kilograms.CompareTo(other.Kilograms);

    public override string ToString() => UnitFormat.ScalarIn(Kilograms, Qualifier, Unit, Units);

    public static Mass Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Mass)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Mass result)
    {
        if (UnitFormat.TryScalarIn(s, Units, out var baseValue, out var qualifier, out var unit))
        { result = new Mass(baseValue, qualifier, unit); return true; }
        result = default;
        return false;
    }
}

/// <summary>A length in millimetres — <see cref="PropertyValueKind.Length"/> (a reach, a repeatability).
/// Canonical: <c>"1300 mm"</c>, <c>"±0.03 mm"</c>.</summary>
[PropertyKind(PropertyValueKind.Length, DefaultUnit = Length.BaseUnit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Length>))]
public readonly record struct Length(
    decimal Millimeters, ValueQualifier Qualifier = ValueQualifier.Exact, string Unit = Length.BaseUnit)
    : IParsable<Length>, IComparable<Length>
{
    /// <summary>The unit the magnitude is normalised to.</summary>
    public const string BaseUnit = "mm";
    // Longest suffix first so "mm" is never read as "m". km is included because cable runs are quoted in metres and
    // somebody will eventually write a fibre run in km.
    private static readonly (string Suffix, decimal ToBase)[] Units =
        [("mm", 1m), ("cm", 10m), ("km", 1_000_000m), ("m", 1000m)];

    /// <summary>Zero millimetres.</summary>
    public static readonly Length Zero = new(0m);

    /// <summary>Whether this is a symmetric tolerance (a repeatability) rather than a distance.</summary>
    public bool IsTolerance => Qualifier == ValueQualifier.Tolerance;

    public static implicit operator decimal(Length l) => l.Millimeters;
    public static explicit operator Length(decimal millimeters) => new(millimeters);

    // Comparison is ALWAYS on the base magnitude, so a 100 m cable run and a 1400 mm reach still order correctly.
    public int CompareTo(Length other) => Millimeters.CompareTo(other.Millimeters);

    // The VENDOR'S unit is preserved: a cable run is "100 m" on the datasheet and must render "100 m" on a public
    // page, not "100000 mm". Normalising the magnitude keeps it comparable; normalising the PRESENTATION would make
    // a headline spec unreadable.
    public override string ToString() => UnitFormat.ScalarIn(Millimeters, Qualifier, Unit, Units);

    public static Length Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Length)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Length result)
    {
        if (UnitFormat.TryScalarIn(s, Units, out var baseValue, out var qualifier, out var unit))
        { result = new Length(baseValue, qualifier, unit); return true; }
        result = default;
        return false;
    }
}

/// <summary>A percentage, 0–100 — <see cref="PropertyValueKind.Percent"/> (duty cycle %, efficiency).
/// Canonical: <c>"60 %"</c>. Outside 0–100 is rejected.</summary>
[PropertyKind(PropertyValueKind.Percent, DefaultUnit = Percent.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Percent>))]
public readonly record struct Percent(decimal Value, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Percent>, IComparable<Percent>
{
    /// <summary>The percent sign.</summary>
    public const string Unit = "%";

    /// <summary>Zero per cent.</summary>
    public static readonly Percent Zero = new(0m);

    public static implicit operator decimal(Percent p) => p.Value;
    public static explicit operator Percent(decimal value) => new(value);

    /// <summary>The fraction form (60 % → 0.60) for arithmetic.</summary>
    public decimal AsFraction => Value / 100m;

    public int CompareTo(Percent other) => Value.CompareTo(other.Value);
    public override string ToString() => UnitFormat.Scalar(Value, Qualifier, Unit);

    public static Percent Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Percent)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Percent result)
    {
        if (UnitFormat.TryScalar(s, Unit, out var value, out var qualifier) && value >= 0m && value <= 100m)
        { result = new Percent(value, qualifier); return true; }
        result = default;
        return false;
    }
}

/// <summary>A current in amperes — <see cref="PropertyValueKind.Current"/>. The most decision-relevant welding
/// number there is: <c>current.output.max</c> and, crucially, <c>current.at.100pct.duty</c> — an Artsen Plus 500
/// derates to 387 A at 100 % duty, LESS than a Plus 400's 400 A, so a configurator sorting on model number gets the
/// comparison exactly backwards. Canonical: <c>"500 A"</c>, <c>"350 A+"</c>.</summary>
[PropertyKind(PropertyValueKind.Current, DefaultUnit = Current.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Current>))]
public readonly record struct Current(decimal Amperes, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Current>, IComparable<Current>
{
    /// <summary>The ampere symbol.</summary>
    public const string Unit = "A";

    /// <summary>Zero amperes.</summary>
    public static readonly Current Zero = new(0m);

    public static implicit operator decimal(Current c) => c.Amperes;
    public static explicit operator Current(decimal amperes) => new(amperes);

    public int CompareTo(Current other) => Amperes.CompareTo(other.Amperes);
    public override string ToString() => UnitFormat.Scalar(Amperes, Qualifier, Unit);

    public static Current Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Current)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Current result)
    {
        if (UnitFormat.TryScalar(s, Unit, out var value, out var qualifier))
        { result = new Current(value, qualifier); return true; }
        result = default;
        return false;
    }
}

/// <summary>A voltage in volts — <see cref="PropertyValueKind.Voltage"/> (<c>voltage.no-load</c> 85 V).
/// Canonical: <c>"85 V"</c>.</summary>
[PropertyKind(PropertyValueKind.Voltage, DefaultUnit = Voltage.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Voltage>))]
public readonly record struct Voltage(decimal Volts, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Voltage>, IComparable<Voltage>
{
    /// <summary>The volt symbol.</summary>
    public const string Unit = "V";

    /// <summary>Zero volts.</summary>
    public static readonly Voltage Zero = new(0m);

    public static implicit operator decimal(Voltage v) => v.Volts;
    public static explicit operator Voltage(decimal volts) => new(volts);

    public int CompareTo(Voltage other) => Volts.CompareTo(other.Volts);
    public override string ToString() => UnitFormat.Scalar(Volts, Qualifier, Unit);

    public static Voltage Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Voltage)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Voltage result)
    {
        if (UnitFormat.TryScalar(s, Unit, out var value, out var qualifier))
        { result = new Voltage(value, qualifier); return true; }
        result = default;
        return false;
    }
}

/// <summary>A frequency in hertz — <see cref="PropertyValueKind.Frequency"/>. Canonical: <c>"25 Hz"</c>.</summary>
[PropertyKind(PropertyValueKind.Frequency, DefaultUnit = Frequency.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Frequency>))]
public readonly record struct Frequency(decimal Hertz, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Frequency>, IComparable<Frequency>
{
    /// <summary>The hertz symbol.</summary>
    public const string Unit = "Hz";

    /// <summary>Zero hertz.</summary>
    public static readonly Frequency Zero = new(0m);

    public static implicit operator decimal(Frequency f) => f.Hertz;
    public static explicit operator Frequency(decimal hertz) => new(hertz);

    public int CompareTo(Frequency other) => Hertz.CompareTo(other.Hertz);
    public override string ToString() => UnitFormat.Scalar(Hertz, Qualifier, Unit);

    public static Frequency Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Frequency)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Frequency result)
    {
        if (UnitFormat.TryScalar(s, Unit, out var value, out var qualifier))
        { result = new Frequency(value, qualifier); return true; }
        result = default;
        return false;
    }
}

/// <summary>An angle in degrees — <see cref="PropertyValueKind.Angle"/>. Implemented HERE rather than over
/// <c>Drawing.Degree&lt;double&gt;</c>, which formats through the ambient culture and therefore cannot parse its own
/// output under a pl-PL culture (it writes <c>"0,5°"</c> then rejects it).
///
/// A <c>±</c> here means a SYMMETRIC limit (the joint swings both ways), not a measurement tolerance. An ASYMMETRIC
/// limit (<c>+85° / −265°</c>) is a <see cref="PropertyValueKind.Range"/> with unit <c>°</c>.
/// Canonical: <c>"±360°"</c>, <c>"175°"</c>.</summary>
[PropertyKind(PropertyValueKind.Angle, DefaultUnit = Angle.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Angle>))]
public readonly record struct Angle(decimal Degrees, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Angle>, IComparable<Angle>
{
    /// <summary>The degree sign. Written TIGHT against the number — "±360°", never "±360 °".</summary>
    public const string Unit = "°";

    /// <summary>Zero degrees.</summary>
    public static readonly Angle Zero = new(0m);

    /// <summary>Whether this is a symmetric ± limit rather than a single stated angle.</summary>
    public bool IsSymmetric => Qualifier == ValueQualifier.Tolerance;

    /// <summary>The total swing a symmetric limit permits (±360° → 720°), which is what actually compares between
    /// a joint limited to ±175° and one limited to ±360°.</summary>
    public decimal Sweep => IsSymmetric ? Degrees * 2m : Degrees;

    public static implicit operator decimal(Angle a) => a.Degrees;
    public static explicit operator Angle(decimal degrees) => new(degrees);

    public int CompareTo(Angle other) => Degrees.CompareTo(other.Degrees);
    public override string ToString() => UnitFormat.Scalar(Degrees, Qualifier, Unit, space: false);

    public static Angle Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Angle)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Angle result)
    {
        if (UnitFormat.TryScalar(s, Unit, out var value, out var qualifier))
        { result = new Angle(value, qualifier); return true; }
        result = default;
        return false;
    }
}

/// <summary>A linear speed in metres per second — <see cref="PropertyValueKind.Speed"/>. Implemented HERE rather
/// than over <c>Drawing.Speed&lt;double&gt;</c>, which is hard-wired to mm/min (so it cannot express m/s at all) and
/// formats through the ambient culture. <c>m/min</c>, <c>mm/s</c> and <c>mm/min</c> are accepted and converted, so
/// every speed stays comparable. Canonical: <c>"1.5 m/s"</c>.</summary>
[PropertyKind(PropertyValueKind.Speed, DefaultUnit = Speed.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Speed>))]
public readonly record struct Speed(decimal MetresPerSecond, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Speed>, IComparable<Speed>
{
    /// <summary>Metres per second.</summary>
    public const string Unit = "m/s";

    /// <summary>Zero metres per second.</summary>
    public static readonly Speed Zero = new(0m);

    public static implicit operator decimal(Speed s) => s.MetresPerSecond;
    public static explicit operator Speed(decimal metresPerSecond) => new(metresPerSecond);

    public int CompareTo(Speed other) => MetresPerSecond.CompareTo(other.MetresPerSecond);
    public override string ToString() => UnitFormat.Scalar(MetresPerSecond, Qualifier, Unit);

    public static Speed Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Speed)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Speed result)
    {
        result = default;
        if (s is null) return false;
        var text = s.Trim();
        var scale = 1m;
        // Longest suffix first — "mm/min" must not be read as "m/min", nor "mm/s" as "m/s".
        if (text.EndsWith("mm/min", StringComparison.OrdinalIgnoreCase))
        { scale = 1m / 60_000m; text = text[..^6].TrimEnd() + " m/s"; }
        else if (text.EndsWith("mm/s", StringComparison.OrdinalIgnoreCase))
        { scale = 0.001m; text = text[..^4].TrimEnd() + " m/s"; }
        else if (text.EndsWith("m/min", StringComparison.OrdinalIgnoreCase))
        { scale = 1m / 60m; text = text[..^5].TrimEnd() + " m/s"; }
        if (!UnitFormat.TryScalar(text, Unit, out var value, out var qualifier)) return false;
        // Round the conversion so a canonical stays a fixed point (24 m/min -> 0.4 m/s, not 0.39999…).
        result = new Speed(Math.Round(value * scale, 6, MidpointRounding.AwayFromZero), qualifier);
        return true;
    }
}

/// <summary>An apparent power in kVA — <see cref="PropertyValueKind.Power"/> (<c>power.rated</c> 24 kVA).
/// <c>VA</c> is accepted and converted; <c>W</c>/<c>kW</c> are NOT — real power and apparent power are different
/// physical quantities and conflating them would invent a fact. A wattage spec uses
/// <see cref="PropertyValueKind.Decimal"/> with the property's unit label until a real-power kind is justified.
/// Canonical: <c>"24 kVA"</c>.</summary>
[PropertyKind(PropertyValueKind.Power, DefaultUnit = Power.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Power>))]
public readonly record struct Power(decimal KiloVoltAmperes, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Power>, IComparable<Power>
{
    /// <summary>Kilovolt-amperes.</summary>
    public const string Unit = "kVA";

    /// <summary>Zero kVA.</summary>
    public static readonly Power Zero = new(0m);

    public static implicit operator decimal(Power p) => p.KiloVoltAmperes;
    public static explicit operator Power(decimal kva) => new(kva);

    public int CompareTo(Power other) => KiloVoltAmperes.CompareTo(other.KiloVoltAmperes);
    public override string ToString() => UnitFormat.Scalar(KiloVoltAmperes, Qualifier, Unit);

    public static Power Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Power)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Power result)
    {
        result = default;
        if (s is null) return false;
        // "VA" must be tried BEFORE "kVA" would swallow it, and a bare number is read as kVA.
        var text = s.Trim();
        var scale = 1m;
        if (text.EndsWith("kVA", StringComparison.OrdinalIgnoreCase)) { /* canonical */ }
        else if (text.EndsWith("VA", StringComparison.OrdinalIgnoreCase))
        {
            scale = 0.001m;
            text = text[..^2].TrimEnd() + " kVA";   // re-tag so the shared scalar parser strips a known unit
        }
        if (!UnitFormat.TryScalar(text, Unit, out var value, out var qualifier)) return false;
        result = new Power(value * scale, qualifier);
        return true;
    }
}

/// <summary>A duration in milliseconds — <see cref="PropertyValueKind.Duration"/> (<c>latency.output</c> ~450 ms).
/// <c>s</c> and <c>min</c> are accepted and converted, so every duration stays directly comparable.
/// Canonical: <c>"450 ms"</c>, <c>"≈450 ms"</c>.</summary>
[PropertyKind(PropertyValueKind.Duration, DefaultUnit = Duration.Unit, Facet = FacetShape.Scalar)]
[JsonConverter(typeof(JsonParsableConverter<Duration>))]
public readonly record struct Duration(decimal Milliseconds, ValueQualifier Qualifier = ValueQualifier.Exact)
    : IParsable<Duration>, IComparable<Duration>
{
    /// <summary>Milliseconds.</summary>
    public const string Unit = "ms";

    /// <summary>Zero milliseconds.</summary>
    public static readonly Duration Zero = new(0m);

    public static implicit operator decimal(Duration d) => d.Milliseconds;
    public static explicit operator Duration(decimal milliseconds) => new(milliseconds);

    public int CompareTo(Duration other) => Milliseconds.CompareTo(other.Milliseconds);
    public override string ToString() => UnitFormat.Scalar(Milliseconds, Qualifier, Unit);

    public static Duration Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Duration)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Duration result)
    {
        result = default;
        if (s is null) return false;
        var text = s.Trim();
        var scale = 1m;
        // Longest unit first — "ms" must not be mistaken for "s".
        if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase)) { /* canonical */ }
        else if (text.EndsWith("min", StringComparison.OrdinalIgnoreCase))
        { scale = 60_000m; text = text[..^3].TrimEnd() + " ms"; }
        else if (text.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        { scale = 1_000m; text = text[..^1].TrimEnd() + " ms"; }
        if (!UnitFormat.TryScalar(text, Unit, out var value, out var qualifier)) return false;
        result = new Duration(value * scale, qualifier);
        return true;
    }
}

/// <summary>A three-component size — <see cref="PropertyValueKind.Size3D"/>. Every dimension the motivating
/// catalogue holds is a TRIPLE (<c>620 × 300 × 480 mm</c>, camera <c>110 × 43 × 43 mm</c>), which is precisely why
/// a 2-D <c>Size</c> does not fit. <see cref="X"/>/<see cref="Y"/>/<see cref="Z"/> are the vendor's stated
/// dimensions IN THE VENDOR'S STATED ORDER — we do not label which is length, width or height, because datasheets
/// disagree and guessing would invent a fact we were not given. Canonical: <c>"620 × 300 × 480 mm"</c>.</summary>
[PropertyKind(PropertyValueKind.Size3D, DefaultUnit = "mm", Facet = FacetShape.Composite)]
[JsonConverter(typeof(JsonParsableConverter<Size3D>))]
public readonly record struct Size3D(decimal X, decimal Y, decimal Z, string Unit = "mm") : IParsable<Size3D>
{
    private const char Times = '×';
    private static readonly char[] TimesSeparators = [Times, 'x', 'X', '*'];

    /// <summary>The bounding volume — the only ordering that is meaningful without axis semantics.</summary>
    public decimal Volume => X * Y * Z;

    /// <summary>The largest stated dimension — "will it fit past the torch?" is a max-extent question.</summary>
    public decimal MaxExtent => Math.Max(X, Math.Max(Y, Z));

    public override string ToString() =>
        $"{UnitFormat.Number(X)} {Times} {UnitFormat.Number(Y)} {Times} {UnitFormat.Number(Z)} {Unit}".TrimEnd();

    public static Size3D Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(Size3D)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Size3D result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        // Accept ×, x and * as the separator — datasheets use all three.
        var parts = s.Split(TimesSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;

        // The unit trails the LAST component ("620 × 300 × 480 mm"); a bare triple defaults to mm.
        var unit = UnitFormat.TrailingUnit(parts[2]);
        if (!UnitFormat.TryNumber(parts[0], null, out var x)) return false;
        if (!UnitFormat.TryNumber(parts[1], null, out var y)) return false;
        if (!UnitFormat.TryNumber(parts[2], unit, out var z)) return false;

        result = new Size3D(x, y, z, string.IsNullOrEmpty(unit) ? "mm" : unit);
        return true;
    }
}

/// <summary>A numeric interval with its unit — <see cref="PropertyValueKind.Range"/>. Deliberately GENERIC rather
/// than one range type per dimension: a range's dimension comes from the PROPERTY DEFINITION's declared unit, so
/// <c>CurrentRange</c>/<c>VoltageRange</c>/<c>TemperatureRange</c>/… would be a combinatorial family of
/// near-identical types. One kind covers <c>30 – 500 A</c>, <c>0 – 45 °C</c>, <c>45 – 65 Hz</c>,
/// <c>150 – 250 ms</c> and <c>12 – 45 V</c>, and every one of them stays numerically range-searchable.
/// Canonical: <c>"30 – 500 A"</c>; an open upper bound keeps the vendor's <c>+</c>: <c>"80 – 350 A+"</c>.
///
/// <para><b>Naming, stated once so it does not read as a typo:</b> the KIND is
/// <see cref="PropertyValueKind.Range"/>; the STRUCT backing it is <c>ValueRange</c>, because
/// <c>System.Range</c> is taken.</para></summary>
[PropertyKind(PropertyValueKind.Range, Facet = FacetShape.Interval)]
[JsonConverter(typeof(JsonParsableConverter<ValueRange>))]
public readonly record struct ValueRange(decimal Min, decimal Max, string? Unit = null, bool UpperOpenEnded = false)
    : IParsable<ValueRange>
{
    private const char EnDash = '–';
    // Hoisted: a search index re-parses these in bulk, and a per-parse char[] is a free allocation to remove.
    private static readonly char[] DashSeparators = [EnDash, '—'];

    /// <summary>Well-formed = max ≥ min.</summary>
    public bool IsWellFormed => Max >= Min;

    /// <summary>Whether this interval overlaps another — the range-facet predicate.</summary>
    public bool Overlaps(ValueRange other) => Min <= other.Max && other.Min <= Max;

    /// <summary>Whether a scalar falls inside (an open upper bound extends to +∞).</summary>
    public bool Contains(decimal value) => value >= Min && (UpperOpenEnded || value <= Max);

    /// <summary>Whether this interval lies wholly inside <paramref name="other"/> (an open upper bound on
    /// <paramref name="other"/> extends its ceiling to +∞; an open upper bound on THIS one is unbounded and can
    /// therefore only sit inside another unbounded interval).</summary>
    public bool Within(ValueRange other) =>
        Min >= other.Min && (other.UpperOpenEnded || (!UpperOpenEnded && Max <= other.Max));

    public override string ToString()
    {
        var body = $"{UnitFormat.Number(Min)} {EnDash} {UnitFormat.Number(Max)}";
        // The unit binds exactly as it does on a scalar — "−265 – 85°", never "−265 – 85 °" — because an axis limit
        // and an Angle appear SIDE BY SIDE, which is the whole point of typing them.
        var withUnit = string.IsNullOrEmpty(Unit) ? body
            : UnitFormat.BindsTight(Unit) ? $"{body}{Unit}" : $"{body} {Unit}";
        return UpperOpenEnded ? withUnit + "+" : withUnit;
    }

    public static ValueRange Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var r)) throw new FormatException($"Invalid {nameof(ValueRange)}: {s}");
        return r;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ValueRange result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        // U+2212 MINUS SIGN is a SIGN, not a separator — datasheets write "−39 °C ~ +50 °C". Normalise it first so
        // the separator search below cannot mistake a negative lower bound for the range separator.
        var text = s.Trim().Replace('−', '-');

        var openEnded = text.EndsWith('+');
        if (openEnded) text = text[..^1].TrimEnd();

        var i = SeparatorIndex(text);
        if (i <= 0) return false;

        var left = text[..i].Trim();
        var right = text[(i + 1)..].Trim();
        // "80 A – 350 A+" — the upper bound may carry its own trailing marker.
        if (right.EndsWith('+')) { openEnded = true; right = right[..^1].TrimEnd(); }
        if (left.Length == 0 || right.Length == 0) return false;

        var unit = UnitFormat.TrailingUnit(right);
        if (!UnitFormat.TryNumber(right, unit, out var max)) return false;
        // The lower bound may repeat the unit ("−39 °C ~ +50 °C") or omit it ("30 – 500 A").
        if (!UnitFormat.TryNumber(left, UnitFormat.TrailingUnit(left), out var min)) return false;

        var range = new ValueRange(min, max, string.IsNullOrEmpty(unit) ? null : unit, openEnded);
        if (!range.IsWellFormed) return false;
        result = range;
        return true;
    }

    // '~' and the dashes are unambiguous separators. A plain '-' only separates when it is not the leading sign of
    // the first bound, so "-39 - 50 °C" still splits at the SECOND hyphen.
    private static int SeparatorIndex(string text)
    {
        var i = text.IndexOf('~');
        if (i > 0) return i;
        i = text.IndexOfAny(DashSeparators);
        if (i > 0) return i;
        for (var j = 1; j < text.Length; j++)
            if (text[j] == '-' && text[j - 1] != 'e' && text[j - 1] != 'E')
                return j;
        return -1;
    }
}

/// <summary>Shared, tolerant number ↔ unit ↔ qualifier handling for the value-classes. Internal: this is the
/// package's own arithmetic, not part of its contract.</summary>
internal static class UnitFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The canonical numeric form: invariant culture, no trailing zeros ("10", "0.03", "1300").</summary>
    public static string Number(decimal value) =>
        value == decimal.Truncate(value) && Math.Abs(value) < 1_000_000_000m
            ? decimal.Truncate(value).ToString(Invariant)
            : value.ToString("0.############", Invariant);

    /// <summary>Renders a qualified scalar: "10 kg", "±0.03 mm", "≈40 kg", "350 A+", "≤5 ms". <paramref name="space"/>
    /// is false for units written tight against the number, as the degree sign is ("±360°", never "±360 °").</summary>
    public static string Scalar(decimal value, ValueQualifier qualifier, string unit, bool space = true)
    {
        var body = space ? $"{Number(value)} {unit}" : $"{Number(value)}{unit}";
        return qualifier switch
        {
            ValueQualifier.Tolerance => $"±{body}",
            ValueQualifier.Approximate => $"≈{body}",
            ValueQualifier.AtMost => $"≤{body}",
            // The vendor's own notation for an open lower bound is a TRAILING plus ("350 A+"), so that is the canonical.
            ValueQualifier.AtLeast => $"{body}+",
            _ => body,
        };
    }

    /// <summary>Units that are written TIGHT against the number — "±360°", never "±360 °". Applied identically by
    /// the scalar formatter and by <see cref="ValueRange"/>, so the same quantity cannot render two ways on one
    /// page.</summary>
    public static bool BindsTight(string? unit) => unit == "°";

    /// <summary>Renders a base-unit magnitude back in <paramref name="displayUnit"/> — the unit the vendor quoted —
    /// so a 100 m cable run renders "100 m" and not "100000 mm". Falls back to the base unit if the display unit is
    /// not one this type knows.</summary>
    public static string ScalarIn(decimal baseValue, ValueQualifier qualifier, string? displayUnit,
        (string Suffix, decimal ToBase)[] units)
    {
        foreach (var (suffix, toBase) in units)
            if (string.Equals(suffix, displayUnit, StringComparison.Ordinal))
                return Scalar(baseValue / toBase, qualifier, suffix, !BindsTight(suffix));
        var (baseSuffix, _) = units[0];
        return Scalar(baseValue, qualifier, baseSuffix, !BindsTight(baseSuffix));
    }

    /// <summary>Parses a qualified scalar carrying any of <paramref name="units"/>, returning the magnitude in the
    /// BASE unit (so values compare across units) plus the unit the vendor actually wrote (so it renders back the
    /// way they wrote it). Suffixes are matched in the order given — put longer ones first.</summary>
    public static bool TryScalarIn(string? s, (string Suffix, decimal ToBase)[] units,
        out decimal baseValue, out ValueQualifier qualifier, out string unit)
    {
        baseValue = 0m;
        qualifier = ValueQualifier.Exact;
        unit = units[0].Suffix;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var text = s.Trim();
        var trailingPlus = text.EndsWith('+');
        var body = trailingPlus ? text[..^1].TrimEnd() : text;

        foreach (var (suffix, toBase) in units)
        {
            if (!body.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!TryScalar(text, suffix, out var value, out var q)) return false;
            baseValue = value * toBase;
            qualifier = q;
            unit = suffix;
            return true;
        }

        // No recognised unit at all — accept a bare number as the base unit rather than rejecting it.
        if (!TryScalar(text, units[0].Suffix, out var bare, out var bareQualifier)) return false;
        baseValue = bare;
        qualifier = bareQualifier;
        return true;
    }

    /// <summary>Parses "[qualifier]&lt;number&gt; [unit][+]", preserving the vendor's hedge rather than sharpening
    /// it.</summary>
    public static bool TryScalar(string? s, string unit, out decimal value, out ValueQualifier qualifier)
    {
        value = 0m;
        qualifier = ValueQualifier.Exact;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var text = s.Trim().Replace('−', '-');

        // A trailing '+' is an open lower bound ("350 A+"); read it before the unit is stripped.
        if (text.EndsWith('+')) { qualifier = ValueQualifier.AtLeast; text = text[..^1].TrimEnd(); }

        // Leading markers. '~' and '≈' both mean approximate; '>'/'≥' and '<'/'≤' bound it.
        if (text.Length > 0)
        {
            switch (text[0])
            {
                case '±': qualifier = ValueQualifier.Tolerance; text = text[1..]; break;
                case '≈':
                case '~': qualifier = ValueQualifier.Approximate; text = text[1..]; break;
                case '≥':
                case '>': qualifier = ValueQualifier.AtLeast; text = text[1..]; break;
                case '≤':
                case '<': qualifier = ValueQualifier.AtMost; text = text[1..]; break;
            }
        }

        return TryNumber(text, unit, out value);
    }

    /// <summary>The trailing unit token of a value ("500 A" → "A", "0–45 °C" → "°C", "1300" → ""). Everything after
    /// the last digit counts as unit text.</summary>
    public static string TrailingUnit(string s)
    {
        var t = s.Trim();
        var i = t.Length;
        while (i > 0 && !char.IsDigit(t[i - 1])) i--;
        return t[i..].Trim();
    }

    /// <summary>Removes a trailing unit label (case-insensitive) and surrounding whitespace.</summary>
    public static string StripUnit(string s, string? unit)
    {
        var t = s.Trim();
        if (!string.IsNullOrEmpty(unit) && t.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            t = t[..^unit.Length];
        return t.Trim();
    }

    /// <summary>Parses "&lt;number&gt; [unit]" tolerantly: the unit is optional, the decimal separator may be a dot
    /// or a Polish comma, and spaces / non-breaking spaces are ignored as digit grouping.</summary>
    public static bool TryNumber(string? s, string? unit, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var cleaned = StripUnit(s, unit)
            .Replace(" ", "").Replace(" ", "").Replace(" ", "")
            .Replace(',', '.');
        if (cleaned.StartsWith('+')) cleaned = cleaned[1..];
        // Always INVARIANT: the input was just normalised to a dot separator, so a culture-specific provider (pl-PL
        // reads '.' as a group separator) would mis-parse it. The canonical form is invariant by definition (CI-1).
        return decimal.TryParse(cleaned, NumberStyles.Number, Invariant, out value);
    }
}
