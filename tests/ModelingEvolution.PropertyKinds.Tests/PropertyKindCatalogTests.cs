using System.Collections.Immutable;
using System.Globalization;

namespace ModelingEvolution.PropertyKinds.Tests;

// The CLOSED, code-defined PropertyValueKind catalogue. These tests defend CI-1: the wire ordinals are frozen
// forever, every member has a descriptor, and a canonical form round-trips through its backing type.
//
// PORTED VERBATIM from Erp.Web.Tests/Products/PropertyKindCatalogTests.cs when the catalogue was extracted into
// this package. Only two things changed: the namespace, and the exception For() throws — the ERP mapped an
// unusable kind to its own 400-valued domain rejection, and a units library has no domain to reject into. The
// ERP still asserts that mapping, on its side of the boundary, where it belongs.
public class PropertyKindCatalogTests
{
    private static readonly PropertyKindCatalog Catalog = PropertyKindCatalog.Default;

    // ── CI-1 · the ordinals are the WIRE FORMAT. Reordering the enum silently re-types every stored value ────────
    [Theory]
    [InlineData(PropertyValueKind.Text, 0)]
    [InlineData(PropertyValueKind.LongText, 1)]
    [InlineData(PropertyValueKind.Integer, 2)]
    [InlineData(PropertyValueKind.Decimal, 3)]
    [InlineData(PropertyValueKind.Boolean, 4)]
    [InlineData(PropertyValueKind.Url, 5)]
    [InlineData(PropertyValueKind.Enum, 6)]
    [InlineData(PropertyValueKind.EnumSet, 7)]
    [InlineData(PropertyValueKind.Money, 8)]
    [InlineData(PropertyValueKind.ByteSize, 9)]
    [InlineData(PropertyValueKind.Angle, 10)]
    [InlineData(PropertyValueKind.Speed, 11)]
    [InlineData(PropertyValueKind.Size2D, 12)]
    [InlineData(PropertyValueKind.Mass, 13)]
    [InlineData(PropertyValueKind.Length, 14)]
    [InlineData(PropertyValueKind.Percent, 15)]
    [InlineData(PropertyValueKind.Range, 16)]
    [InlineData(PropertyValueKind.Current, 17)]
    [InlineData(PropertyValueKind.Voltage, 18)]
    [InlineData(PropertyValueKind.Frequency, 19)]
    [InlineData(PropertyValueKind.Size3D, 20)]
    [InlineData(PropertyValueKind.Power, 21)]
    [InlineData(PropertyValueKind.Duration, 22)]
    public void Wire_ordinals_are_frozen(PropertyValueKind kind, int ordinal) =>
        ((int)kind).Should().Be(ordinal,
            "PropertyValueKind serializes as an INTEGER; changing an ordinal re-types every value already stored");

    [Fact]
    public void Every_kind_is_either_registered_or_explicitly_reserved()
    {
        // No third state: a member is usable, or it is a permanently held ordinal. A member that is neither would be
        // a silent trap that only surfaces the first time somebody stores a value under it.
        foreach (var kind in Enum.GetValues<PropertyValueKind>())
            (Catalog.IsSupported(kind) ^ PropertyKindCatalog.Reserved.Contains(kind)).Should().BeTrue($"{kind}");
    }

    [Theory]
    [InlineData(PropertyValueKind.ByteSize)]
    [InlineData(PropertyValueKind.Size2D)]
    public void A_reserved_kind_fails_loudly_and_says_it_is_reserved(PropertyValueKind kind)
    {
        // These four hold their ordinals so the wire format never shifts, but nothing in the catalogue needs them, so
        // no descriptor and no package dependency exists. Using one must be an explicit "not built yet", never a
        // silent acceptance and never a confusing "unknown kind".
        Catalog.IsSupported(kind).Should().BeFalse();
        var act = () => Catalog.For(kind);
        var thrown = act.Should().Throw<PropertyKindException>()
            .Which;
        thrown.Message.Should().Contain("reserved").And.Contain("not yet supported");
        thrown.IsReserved.Should().BeTrue("a reserved ordinal is 'not built yet', not 'we do not know what that is'");
        thrown.Kind.Should().Be(kind);
    }

    [Fact]
    public void An_ordinal_outside_the_catalog_fails_as_unknown_not_as_reserved()
    {
        var act = () => Catalog.For((PropertyValueKind)999);
        var thrown = act.Should().Throw<PropertyKindException>().Which;
        thrown.Message.Should().Contain("Unknown");
        thrown.IsReserved.Should().BeFalse("999 is not a held ordinal — it is a corrupt or future wire value");
    }

    // =============================================================================
    //  THE GUARD THE FROZEN WIRE FORMAT NEEDS.
    //
    //  Program.cs installs pl-PL PROCESS-WIDE, but the xunit host never runs Program.cs, so every other test in this
    //  suite runs under the machine default and can be green while production silently corrupts. This theory runs
    //  EVERY registered kind's own canonical form under BOTH cultures and asserts the two properties the design
    //  makes normative:
    //     (1) a kind ACCEPTS the canonical it just produced  — F2 design Risk 1: "TryValidate must never reject a
    //         value it once accepted";
    //     (2) canonicalisation is IDEMPOTENT                 — or a re-save drifts the stored value.
    //
    //  Mutation guard: point any row at a culture-sensitive ToString() (e.g. Drawing's Speed/Degree, or
    //  Money.ToString()) and the pl-PL case fails immediately.
    // =============================================================================
    public static TheoryData<PropertyValueKind, string> EveryRegisteredKindsCanonical() => new()
    {
        { PropertyValueKind.Text, "IP54" },
        { PropertyValueKind.LongText, "500 A / 39 V 60% @ 40 °C; 387 A / 33.5 V 100% @ 40 °C" },
        { PropertyValueKind.Integer, "6" },
        { PropertyValueKind.Decimal, "0.95" },
        { PropertyValueKind.Boolean, "true" },
        { PropertyValueKind.Url, "https://example.com/" },
        { PropertyValueKind.Enum, "TIG" },
        { PropertyValueKind.EnumSet, "MIG/MAG, TIG" },
        { PropertyValueKind.Money, "3299.00 PLN" },
        { PropertyValueKind.Mass, "10 kg" },
        { PropertyValueKind.Length, "±0.05 mm" },
        { PropertyValueKind.Percent, "60 %" },
        { PropertyValueKind.Range, "30 – 500 A" },
        { PropertyValueKind.Current, "500 A" },
        { PropertyValueKind.Voltage, "85 V" },
        { PropertyValueKind.Frequency, "25 Hz" },
        { PropertyValueKind.Size3D, "620 × 300 × 480 mm" },
        { PropertyValueKind.Power, "24 kVA" },
        { PropertyValueKind.Duration, "450 ms" },
        { PropertyValueKind.Angle, "±360°" },
        { PropertyValueKind.Speed, "1.5 m/s" },
    };

    [Theory]
    [MemberData(nameof(EveryRegisteredKindsCanonical))]
    public void A_canonical_survives_the_apps_own_culture(PropertyValueKind kind, string canonical)
    {
        foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("pl-PL") })
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = culture;
                var descriptor = Catalog.For(kind);

                descriptor.TryValidate(canonical, ChoicesFor(kind), out var error)
                    .Should().BeTrue($"{kind} must accept its own canonical '{canonical}' under {CultureLabel(culture)}: {error}");

                var once = descriptor.Canonicalize(canonical, ChoicesFor(kind));
                once.Should().Be(canonical, $"{kind} canonical must be a fixed point under {CultureLabel(culture)}");
                descriptor.Canonicalize(once, ChoicesFor(kind)).Should().Be(once);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }

    [Fact]
    public void Every_registered_kind_is_covered_by_the_culture_theory()
    {
        // Without this, adding a kind and forgetting its row would leave the new ordinal untested — which is exactly
        // how the four reused kinds shipped broken.
        var covered = EveryRegisteredKindsCanonical().Select(row => (PropertyValueKind)row[0]!);
        var registered = Enum.GetValues<PropertyValueKind>().Where(Catalog.IsSupported);
        covered.Should().BeEquivalentTo(registered);
    }

    private static string CultureLabel(CultureInfo c) => string.IsNullOrEmpty(c.Name) ? "invariant" : c.Name;

    private static ImmutableList<string>? ChoicesFor(PropertyValueKind kind) =>
        Catalog.For(kind).RequiresChoices ? Processes : null;

    // ── Finding #4 · the pl-PL keystroke that used to become a 10× integer ──────────────────────────────────────
    [Theory]
    [InlineData("1,5")]
    [InlineData("1,05")]
    [InlineData("0,5")]
    [InlineData("1.5")]
    public void An_integer_rejects_anything_with_a_decimal_separator_rather_than_guessing(string input)
    {
        // "1,5" is one-and-a-half to a Polish user. Reading the comma as grouping stored 15 — a 10× corruption from
        // a single keystroke, and the Decimal kind read the SAME keystroke as 1.5.
        Catalog.For(PropertyValueKind.Integer).TryValidate(input, null, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void An_integer_still_accepts_space_grouping_because_a_space_is_never_a_decimal_separator()
    {
        // Unlike ',' and '.', a space is unambiguous in every culture the ERP runs under, so accepting it is not a
        // guess. Rejecting it too would make a perfectly clear "1 300" fail for no reason.
        Catalog.For(PropertyValueKind.Integer).Canonicalize("1 300", null).Should().Be("1300");
    }

    [Fact]
    public void The_same_keystroke_never_means_two_different_numbers_across_kinds()
    {
        var decimalKind = Catalog.For(PropertyValueKind.Decimal);
        decimalKind.TryValidate("1,5", null, out _).Should().BeTrue();
        decimalKind.Canonicalize("1,5", null).Should().Be("1.5");
        Catalog.For(PropertyValueKind.Integer).TryValidate("1,5", null, out _).Should().BeFalse();
    }

    // ── Finding #5 · Money is stored invariant and always currency-qualified ────────────────────────────────────
    [Theory]
    [InlineData("3299,00", "3299.00 PLN")]              // pl display form, no currency token
    [InlineData("3 299,00 zł", "3299.00 PLN")]           // with the Polish symbol
    [InlineData("10 390,00 EUR", "10390.00 EUR")]        // Money.ToString()'s own output
    [InlineData("10390.00 EUR", "10390.00 EUR")]         // already canonical
    [InlineData("42611", "42611.00 PLN")]
    public void Money_canonicalises_invariant_and_always_names_its_currency(string input, string canonical)
    {
        var descriptor = Catalog.For(PropertyValueKind.Money);
        descriptor.TryValidate(input, null, out var error).Should().BeTrue($"'{input}': {error}");
        descriptor.Canonicalize(input, null).Should().Be(canonical);
    }

    [Fact]
    public void A_money_canonical_is_never_ambiguous_with_the_other_kinds_dot_form()
    {
        // Every other kind writes an invariant dot form into the SAME `canonical` field. If Money wrote
        // "3 299,00" a consumer parsing that field invariantly would read 329900.
        var canonical = Catalog.For(PropertyValueKind.Money).Canonicalize("3 299,00", null);
        canonical.Should().NotContain(",").And.Contain("PLN");
    }

    // ── Finding #7 · a trailing standard reference is rejected, not silently discarded ──────────────────────────
    [Fact]
    public void A_value_carrying_a_standard_reference_is_rejected_rather_than_quietly_dropping_it()
    {
        // "±0.05 mm (ISO 9283)" — the standard is what makes two vendors' repeatability comparable, so silently
        // discarding it would be worse than refusing the value. The reference belongs on the feature-type's
        // description; the VALUE is "±0.05 mm".
        Catalog.For(PropertyValueKind.Length).TryValidate("±0.05 mm (ISO 9283)", null, out var error)
            .Should().BeFalse();
        error.Should().NotBeNull();
        Catalog.For(PropertyValueKind.Length).TryValidate("±0.05 mm", null, out _).Should().BeTrue();
    }

    // ── Power and Duration convert so they stay comparable ─────────────────────────────────────────────────────
    [Theory]
    [InlineData("24 kVA", "24 kVA")]
    [InlineData("22.3 kVA", "22.3 kVA")]
    [InlineData("16800 VA", "16.8 kVA")]     // VA converts; W deliberately does NOT (different physical quantity)
    public void Power_normalises_to_kVA(string input, string canonical) =>
        Catalog.For(PropertyValueKind.Power).Canonicalize(input, null).Should().Be(canonical);

    [Fact]
    public void Power_refuses_watts_because_real_power_is_not_apparent_power()
    {
        Catalog.For(PropertyValueKind.Power).TryValidate("24 kW", null, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Theory]
    [InlineData("450 ms", "450 ms")]
    [InlineData("~450 ms", "≈450 ms")]       // the vendor's hedge survives
    [InlineData("0.5 s", "500 ms")]
    public void Duration_normalises_to_milliseconds(string input, string canonical) =>
        Catalog.For(PropertyValueKind.Duration).Canonicalize(input, null).Should().Be(canonical);

    // ── #11 · a vendor's unit is preserved, so a headline spec never renders as an absurd base-unit number ───────
    [Theory]
    [InlineData("100 m", "100 m")]           // cable.run.max — "100000 mm" on the public page would be unreadable
    [InlineData("1400 mm", "1400 mm")]
    [InlineData("±0.05 mm", "±0.05 mm")]
    [InlineData("2.5 cm", "2.5 cm")]
    [InlineData("1 km", "1 km")]
    [InlineData("1400", "1400 mm")]          // a bare number is the base unit
    public void Length_keeps_the_unit_the_vendor_wrote(string input, string canonical) =>
        Catalog.For(PropertyValueKind.Length).Canonicalize(input, null).Should().Be(canonical);

    [Fact]
    public void Lengths_written_in_different_units_still_compare()
    {
        // Presentation is the vendor's; the MAGNITUDE is normalised, so ordering is never unit-dependent.
        var cableRun = Length.Parse("100 m");
        var reach = Length.Parse("1400 mm");
        cableRun.Millimeters.Should().Be(100_000m);
        cableRun.Should().BeGreaterThan(reach);
        Length.Parse("100 cm").Millimeters.Should().Be(Length.Parse("1 m").Millimeters);
    }

    [Theory]
    [InlineData("40 kg", "40 kg")]
    [InlineData("≈40 kg", "≈40 kg")]
    [InlineData("500 g", "500 g")]
    [InlineData("1.2 t", "1.2 t")]
    public void Mass_keeps_the_unit_the_vendor_wrote(string input, string canonical) =>
        Catalog.For(PropertyValueKind.Mass).Canonicalize(input, null).Should().Be(canonical);

    [Fact]
    public void Masses_written_in_different_units_still_compare()
    {
        Mass.Parse("500 g").Kilograms.Should().Be(0.5m);
        Mass.Parse("1.2 t").Should().BeGreaterThan(Mass.Parse("40 kg"));
    }

    // ── #14 · degrees bind tight everywhere, so a scalar and a range render alike side by side ──────────────────
    [Fact]
    public void An_angle_and_an_angular_range_render_degrees_the_same_way()
    {
        // These two appear SIDE BY SIDE on the cobot axis — axis 6 as a symmetric ±, axes 2 and 4 as intervals.
        // Rendering one "±360°" and the other "-265 – 85 °" would undermine the comparison the kind exists for.
        var scalar = Catalog.For(PropertyValueKind.Angle).Canonicalize("±360°", null);
        var range = Catalog.For(PropertyValueKind.Range).Canonicalize("−265° ~ +85°", null);

        scalar.Should().Be("±360°");
        range.Should().Be("-265 – 85°");
        range.Should().NotContain(" °", "the degree sign binds tight to the number, exactly as it does on a scalar");
    }

    [Fact]
    public void A_unit_that_does_not_bind_tight_keeps_its_space_in_a_range() =>
        Catalog.For(PropertyValueKind.Range).Canonicalize("30–500 A", null).Should().Be("30 – 500 A");

    // ── Angle · the kind that carries the FR10 360° price premium ───────────────────────────────────────────────
    [Theory]
    [InlineData("±360°", "±360°")]
    [InlineData("±175°", "±175°")]
    [InlineData("±160°", "±160°")]
    [InlineData("175°", "175°")]
    [InlineData("±360 °", "±360°")]          // a stray space is tolerated in, never stored
    public void Angle_round_trips_the_joint_limits(string input, string canonical) =>
        Catalog.For(PropertyValueKind.Angle).Canonicalize(input, null).Should().Be(canonical);

    [Fact]
    public void The_FR10_360_premium_is_a_sortable_comparison_not_a_string_difference()
    {
        // The FR10 360° is a separate SKU at +1 000 EUR whose ENTIRE difference from the base FR10 is axis 6.
        // If this value were Text, the one field justifying a ~10% premium could not be sorted or compared.
        var baseFr10 = Angle.Parse("±175°");
        var fr10_360 = Angle.Parse("±360°");

        fr10_360.Should().BeGreaterThan(baseFr10);
        baseFr10.IsSymmetric.Should().BeTrue("a ± axis limit swings both ways — it is not a measurement tolerance");
        fr10_360.Sweep.Should().Be(720m, "±360° is a full turn in each direction");
        baseFr10.Sweep.Should().Be(350m);
    }

    [Fact]
    public void An_asymmetric_axis_limit_is_a_Range_in_degrees_and_still_compares()
    {
        // Fairino states axes 2 and 4 as "+85° / −265°" — not symmetric, so it is an interval, not a ± scalar.
        var axis = ValueRange.Parse("−265° ~ +85°");
        axis.Min.Should().Be(-265m);
        axis.Max.Should().Be(85m);
        axis.Unit.Should().Be("°");
        axis.Contains(0m).Should().BeTrue();
    }

    // ── Speed · TCP speed, and wire feed as a Range ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("1.5 m/s", "1.5 m/s")]
    [InlineData("2 m/s", "2 m/s")]
    [InlineData("24 m/min", "0.4 m/s")]      // converts, so speeds stay comparable
    [InlineData("1500 mm/s", "1.5 m/s")]
    public void Speed_normalises_to_metres_per_second(string input, string canonical) =>
        Catalog.For(PropertyValueKind.Speed).Canonicalize(input, null).Should().Be(canonical);

    [Fact]
    public void Wire_feed_range_keeps_the_unit_the_vendor_quotes_it_in()
    {
        // 0.8–24 m/min is quoted in m/min and compared only against other wire feeds, so Range preserves the unit
        // rather than converting it into an unnatural m/s figure.
        var feed = ValueRange.Parse("0.8 – 24 m/min");
        feed.Min.Should().Be(0.8m);
        feed.Max.Should().Be(24m);
        feed.Unit.Should().Be("m/min");
    }

    // ── Neither Angle nor Speed may reach a culture-sensitive Drawing type ──────────────────────────────────────
    [Fact]
    public void Angle_and_Speed_are_implemented_in_house_not_over_the_broken_Drawing_types()
    {
        // Drawing's Degree<double>/Speed<double> format through the ambient culture: under pl-PL they write "0,5°"
        // and "1,5 mm/min" and then REJECT their own output. Speed<T> is also hard-wired to mm/min, so it cannot
        // express m/s at all. This asserts the canonical is invariant regardless of the ambient culture.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            new Angle(0.5m).ToString().Should().Be("0.5°", "never \"0,5°\"");
            new Speed(1.5m).ToString().Should().Be("1.5 m/s", "never \"1,5 m/s\"");
            Angle.Parse("0.5°").Degrees.Should().Be(0.5m);
            Speed.Parse("1.5 m/s").MetresPerSecond.Should().Be(1.5m);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Only_the_enumeration_kinds_require_choices()
    {
        foreach (var descriptor in Catalog.All)
            descriptor.RequiresChoices.Should().Be(
                descriptor.Kind is PropertyValueKind.Enum or PropertyValueKind.EnumSet,
                $"{descriptor.Kind} choice requirement");
    }

    // ── The REAL datasheet values from catalog-dataset.md §4. If a kind cannot carry one of these, the catalogue
    //    cannot describe the products we actually sell — and the ordinals freeze on first store. ─────────────────
    [Theory]
    // Fairino FR cobots
    [InlineData(PropertyValueKind.Mass, "10 kg", "10 kg")]                       // payload.rated FR10
    [InlineData(PropertyValueKind.Mass, "≈40 kg", "≈40 kg")]                     // weight FR10 — the vendor's hedge
    [InlineData(PropertyValueKind.Mass, "≈85 kg", "≈85 kg")]                     // weight FR20
    [InlineData(PropertyValueKind.Length, "1400 mm", "1400 mm")]                 // reach FR10
    [InlineData(PropertyValueKind.Length, "±0.05 mm", "±0.05 mm")]               // repeatability
    [InlineData(PropertyValueKind.Range, "0–45 °C", "0 – 45 °C")]                // temperature.working
    [InlineData(PropertyValueKind.Integer, "6", "6")]                            // axes
    // Megmeet Artsen Plus power sources
    [InlineData(PropertyValueKind.Current, "500 A", "500 A")]                     // current.output.max Plus 500
    [InlineData(PropertyValueKind.Current, "387 A", "387 A")]                     // current.at.100pct.duty Plus 500
    [InlineData(PropertyValueKind.Range, "30–500 A", "30 – 500 A")]              // current.output.rated
    [InlineData(PropertyValueKind.Range, "45–65 Hz", "45 – 65 Hz")]              // frequency.supply
    [InlineData(PropertyValueKind.Range, "12–45 V", "12 – 45 V")]                // voltage.output.rated
    [InlineData(PropertyValueKind.Range, "−39 °C ~ +50 °C", "-39 – 50 °C")]      // temperature.working (U+2212 sign)
    [InlineData(PropertyValueKind.Voltage, "85 V", "85 V")]                       // voltage.no-load
    [InlineData(PropertyValueKind.Percent, "60 %", "60 %")]                       // duty cycle percentage
    [InlineData(PropertyValueKind.Size3D, "620 × 300 × 480 mm", "620 × 300 × 480 mm")]  // dimensions
    // Rocket Retina cameras
    [InlineData(PropertyValueKind.Size3D, "110 × 43 × 43 mm", "110 × 43 × 43 mm")]
    [InlineData(PropertyValueKind.Size3D, "153 x 43 x 47 mm", "153 × 43 × 47 mm")]      // 'x' normalised to '×'
    [InlineData(PropertyValueKind.Range, "80 A – 350 A+", "80 – 350 A+")]        // current.range, open upper bound
    // Rocket Neuron compute
    [InlineData(PropertyValueKind.Frequency, "25 Hz", "25 Hz")]                   // steering.frequency (full)
    [InlineData(PropertyValueKind.Frequency, "3 Hz", "3 Hz")]                     // steering.frequency (Mini)
    [InlineData(PropertyValueKind.Range, "150–250 ms", "150 – 250 ms")]          // latency.output
    public void Every_real_datasheet_value_round_trips(PropertyValueKind kind, string input, string canonical)
    {
        var descriptor = Catalog.For(kind);
        descriptor.TryValidate(input, null, out var error).Should().BeTrue($"'{input}' must be a valid {kind}: {error}");
        descriptor.Canonicalize(input, null).Should().Be(canonical);
        // …and the canonical must itself be a fixed point, or a re-save would drift the stored value.
        descriptor.Canonicalize(canonical, null).Should().Be(canonical);
    }

    // ── The qualifier rule: a vendor's hedge must survive, because sharpening it invents a claim ─────────────────
    [Fact]
    public void An_approximation_is_never_silently_sharpened_into_a_hard_number()
    {
        // Fairino prints "≈40 kg". Storing "40 kg" would assert a precision Fairino explicitly declined to give —
        // on a public configurator that is a spec misrepresentation.
        var mass = Mass.Parse("≈40 kg");
        mass.Qualifier.Should().Be(ValueQualifier.Approximate);
        mass.Kilograms.Should().Be(40m, "the magnitude is still comparable for sorting");
        mass.ToString().Should().Be("≈40 kg");
    }

    [Fact]
    public void An_open_ended_bound_keeps_its_plus()
    {
        var current = Current.Parse("350 A+");
        current.Qualifier.Should().Be(ValueQualifier.AtLeast);
        current.Amperes.Should().Be(350m);
        current.ToString().Should().Be("350 A+", "'350 A' would understate a bound the vendor left open");
    }

    [Fact]
    public void A_tolerance_is_not_a_distance()
    {
        var repeatability = Length.Parse("±0.05 mm");
        repeatability.IsTolerance.Should().BeTrue();
        Length.Parse("1400 mm").IsTolerance.Should().BeFalse();
        // Same magnitude, different meaning — they must not compare equal.
        Length.Parse("±0.05 mm").Should().NotBe(Length.Parse("0.05 mm"));
    }

    [Theory]
    [InlineData("≈40 kg", ValueQualifier.Approximate)]
    [InlineData("~40 kg", ValueQualifier.Approximate)]
    [InlineData("±40 kg", ValueQualifier.Tolerance)]
    [InlineData("≥40 kg", ValueQualifier.AtLeast)]
    [InlineData("40 kg+", ValueQualifier.AtLeast)]
    [InlineData("≤40 kg", ValueQualifier.AtMost)]
    [InlineData("40 kg", ValueQualifier.Exact)]
    public void Every_qualifier_marker_a_datasheet_uses_is_recognised(string input, ValueQualifier expected) =>
        Mass.Parse(input).Qualifier.Should().Be(expected);

    // ── Range: generic on purpose, so every dimension stays filterable ──────────────────────────────────────────
    [Fact]
    public void A_range_exposes_numeric_bounds_so_it_stays_searchable()
    {
        var rated = ValueRange.Parse("30–500 A");
        rated.Min.Should().Be(30m);
        rated.Max.Should().Be(500m);
        rated.Unit.Should().Be("A");
        rated.Contains(400m).Should().BeTrue();
        rated.Contains(600m).Should().BeFalse();
        rated.Overlaps(ValueRange.Parse("400–800 A")).Should().BeTrue();
    }

    [Fact]
    public void An_open_upper_bound_extends_to_infinity()
    {
        var range = ValueRange.Parse("80 A – 350 A+");
        range.UpperOpenEnded.Should().BeTrue();
        range.Contains(500m).Should().BeTrue("the vendor wrote 350 A+, so above 350 A is in scope");
    }

    [Fact]
    public void A_negative_lower_bound_is_not_mistaken_for_the_separator()
    {
        // "−39 °C ~ +50 °C" uses U+2212 MINUS SIGN. Splitting on it would yield "" and "39 °C ~ +50 °C".
        var range = ValueRange.Parse("−39 °C ~ +50 °C");
        range.Min.Should().Be(-39m);
        range.Max.Should().Be(50m);
        range.Unit.Should().Be("°C");
    }

    [Fact]
    public void A_range_with_max_below_min_is_rejected()
    {
        Catalog.For(PropertyValueKind.Range).TryValidate("500–30 A", null, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void A_single_scalar_is_not_a_range()
    {
        Catalog.For(PropertyValueKind.Range).TryValidate("400 A", null, out _).Should().BeFalse();
    }

    // ── Size3D: our dimensions are always triples, which is why Drawing's 2-D Size does not fit ─────────────────
    [Fact]
    public void Size3D_keeps_the_vendors_component_order_and_exposes_a_comparable_extent()
    {
        var enclosure = Size3D.Parse("153 × 43 × 47 mm");
        (enclosure.X, enclosure.Y, enclosure.Z).Should().Be((153m, 43m, 47m));
        enclosure.Unit.Should().Be("mm");
        enclosure.MaxExtent.Should().Be(153m, "'will it fit past the torch' is a max-extent question");
        enclosure.Volume.Should().Be(153m * 43m * 47m);
    }

    [Fact]
    public void A_two_component_size_is_rejected_rather_than_guessed()
    {
        Catalog.For(PropertyValueKind.Size3D).TryValidate("153 × 43 mm", null, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    // ── Tolerant on the way in, canonical on the way out ───────────────────────────────────────────────────────
    [Theory]
    [InlineData(PropertyValueKind.Mass, "10 kg", "10 kg")]
    [InlineData(PropertyValueKind.Mass, "10kg", "10 kg")]           // tolerated on the way in, canonical on the way out
    [InlineData(PropertyValueKind.Mass, "12,5 kg", "12.5 kg")]      // Polish decimal comma
    [InlineData(PropertyValueKind.Length, "1300 mm", "1300 mm")]
    [InlineData(PropertyValueKind.Length, "±0.03 mm", "±0.03 mm")]  // a tolerance keeps its marker
    [InlineData(PropertyValueKind.Percent, "60 %", "60 %")]
    [InlineData(PropertyValueKind.Percent, "60%", "60 %")]
    [InlineData(PropertyValueKind.Range, "30–400 A", "30 – 400 A")]
    [InlineData(PropertyValueKind.Range, "30-400 A", "30 – 400 A")]  // hyphen accepted, en dash is canonical
    [InlineData(PropertyValueKind.Integer, "1 300", "1300")]
    [InlineData(PropertyValueKind.Decimal, "0,5", "0.5")]
    [InlineData(PropertyValueKind.Boolean, "yes", "true")]
    public void A_valid_value_canonicalizes_to_the_stored_form(PropertyValueKind kind, string input, string canonical)
    {
        var descriptor = Catalog.For(kind);
        descriptor.TryValidate(input, null, out var error).Should().BeTrue($"'{input}' should be a valid {kind}: {error}");
        descriptor.Canonicalize(input, null).Should().Be(canonical);
    }

    // ── F2-AT-7 · a value that does not parse under its kind is rejected ────────────────────────────────────────
    [Theory]
    [InlineData(PropertyValueKind.Mass, "heavy")]
    [InlineData(PropertyValueKind.Length, "long")]
    [InlineData(PropertyValueKind.Percent, "120 %")]        // out of the 0–100 domain
    [InlineData(PropertyValueKind.Percent, "-1 %")]
    [InlineData(PropertyValueKind.Range, "400–30 A")]        // max below min is not an interval
    [InlineData(PropertyValueKind.Range, "400 A")]           // not an interval at all
    [InlineData(PropertyValueKind.Current, "lots")]
    [InlineData(PropertyValueKind.Voltage, "mains")]
    [InlineData(PropertyValueKind.Frequency, "fast")]
    [InlineData(PropertyValueKind.Size3D, "big")]
    [InlineData(PropertyValueKind.Integer, "1.5")]
    [InlineData(PropertyValueKind.Url, "not a url")]
    [InlineData(PropertyValueKind.Url, "ftp://example.com")]
    [InlineData(PropertyValueKind.Boolean, "maybe")]
    [InlineData(PropertyValueKind.Text, "   ")]
    public void An_invalid_value_is_rejected_with_a_reason(PropertyValueKind kind, string input)
    {
        Catalog.For(kind).TryValidate(input, null, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace("the failure must say what was expected");
    }

    // ── Round-trip: ToString ► Parse ► ToString is stable (the CI-1 guarantee the F5 index will rely on) ─────────
    [Theory]
    [InlineData(PropertyValueKind.Mass, "10 kg")]
    [InlineData(PropertyValueKind.Length, "1300 mm")]
    [InlineData(PropertyValueKind.Length, "±0.03 mm")]
    [InlineData(PropertyValueKind.Percent, "60 %")]
    [InlineData(PropertyValueKind.Range, "30 – 400 A")]
    [InlineData(PropertyValueKind.Current, "500 A")]
    [InlineData(PropertyValueKind.Voltage, "85 V")]
    [InlineData(PropertyValueKind.Frequency, "25 Hz")]
    [InlineData(PropertyValueKind.Size3D, "620 × 300 × 480 mm")]
    public void The_canonical_form_is_a_fixed_point(PropertyValueKind kind, string canonical)
    {
        var descriptor = Catalog.For(kind);
        var once = descriptor.Canonicalize(canonical, null);
        descriptor.Canonicalize(once, null).Should().Be(once).And.Be(canonical);
    }

    // ── The new value classes themselves ────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Mass_round_trips_through_its_parse_and_ToString()
    {
        Mass.Parse("10 kg").Kilograms.Should().Be(10m);
        new Mass(12.5m).ToString().Should().Be("12.5 kg");
    }

    [Fact]
    public void Length_distinguishes_a_measurement_from_a_tolerance()
    {
        var reach = Length.Parse("1300 mm");
        reach.Millimeters.Should().Be(1300m);
        reach.IsTolerance.Should().BeFalse();

        var repeatability = Length.Parse("±0.03 mm");
        repeatability.Millimeters.Should().Be(0.03m);
        repeatability.IsTolerance.Should().BeTrue("a repeatability is a symmetric tolerance, not a distance");
        repeatability.ToString().Should().Be("±0.03 mm");
    }

    [Fact]
    public void Percent_exposes_its_fraction_and_rejects_out_of_domain_values()
    {
        Percent.Parse("60 %").AsFraction.Should().Be(0.6m);
        Percent.TryParse("101 %", null, out _).Should().BeFalse();
    }

    [Fact]
    public void A_range_answers_overlap_for_the_F5_facet()
    {
        var range = ValueRange.Parse("30–400 A");
        range.Min.Should().Be(30m);
        range.Max.Should().Be(400m);
        range.Overlaps(new ValueRange(200m, 500m, "A")).Should().BeTrue();
        range.Overlaps(new ValueRange(500m, 600m, "A")).Should().BeFalse();
    }

    // ── enum choices are DATA on the property definition, validated against that list ─────────────────────────
    private static readonly ImmutableList<string> Processes = ImmutableList.Create("MIG/MAG", "TIG", "MMA");

    [Fact]
    public void An_enum_value_must_be_one_of_the_declared_choices()
    {
        var descriptor = Catalog.For(PropertyValueKind.Enum);
        descriptor.TryValidate("TIG", Processes, out _).Should().BeTrue();
        descriptor.TryValidate("SAW", Processes, out var error).Should().BeFalse();
        error.Should().Contain("SAW");
    }

    [Fact]
    public void An_enum_accepts_exactly_one_choice_an_enumset_accepts_several()
    {
        Catalog.For(PropertyValueKind.Enum).TryValidate("TIG, MMA", Processes, out _).Should().BeFalse();
        Catalog.For(PropertyValueKind.EnumSet).TryValidate("TIG, MMA", Processes, out _).Should().BeTrue();
    }

    [Fact]
    public void An_enumset_canonical_follows_the_declared_choice_order_not_the_tick_order()
    {
        // Two admins ticking the same two boxes in different orders MUST produce the same stored string — otherwise
        // the same specification would compare unequal and split a search facet in two.
        var descriptor = Catalog.For(PropertyValueKind.EnumSet);
        descriptor.Canonicalize("TIG, MIG/MAG", Processes).Should().Be("MIG/MAG, TIG");
        descriptor.Canonicalize("MIG/MAG, TIG", Processes).Should().Be("MIG/MAG, TIG");
    }

    [Fact]
    public void A_choice_key_may_contain_a_slash_because_the_separator_is_a_comma()
    {
        // "MIG/MAG" is ONE process, not two — a naive slash split would silently invent a choice.
        Catalog.For(PropertyValueKind.Enum).TryValidate("MIG/MAG", Processes, out _).Should().BeTrue();
    }

    [Fact]
    public void An_enumset_rejects_the_same_choice_twice()
    {
        Catalog.For(PropertyValueKind.EnumSet).TryValidate("TIG, TIG", Processes, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void An_enumeration_without_its_choice_list_is_rejected_rather_than_accepting_anything()
    {
        Catalog.For(PropertyValueKind.Enum).TryValidate("TIG", null, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void A_non_enumeration_kind_rejects_a_choice_list()
    {
        Catalog.For(PropertyValueKind.Mass).TryValidate("10 kg", Processes, out var error).Should().BeFalse();
        error.Should().Contain("choice");
    }

    // ── Format · the render side (FR-9) ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void A_dimensioned_kind_renders_its_intrinsic_unit_and_ignores_the_display_label()
    {
        Catalog.For(PropertyValueKind.Mass).Format("10 kg", "lbs", null).Should().Be("10 kg");
    }

    [Fact]
    public void A_dimensionless_kind_appends_the_property_definitions_display_label()
    {
        Catalog.For(PropertyValueKind.Integer).Format("6", "axes", null).Should().Be("6 axes");
        Catalog.For(PropertyValueKind.Integer).Format("6", null, null).Should().Be("6");
    }

    [Fact]
    public void A_boolean_renders_as_yes_or_no()
    {
        Catalog.For(PropertyValueKind.Boolean).Format("true", null, null).Should().Be("Yes");
        Catalog.For(PropertyValueKind.Boolean).Format("false", null, null).Should().Be("No");
    }

    [Fact]
    public void An_enumset_renders_its_choices_comma_separated_in_declared_order()
    {
        Catalog.For(PropertyValueKind.EnumSet).Format("TIG, MIG/MAG", null, Processes).Should().Be("MIG/MAG, TIG");
    }
}
