using System.Collections.Immutable;
using System.Globalization;

namespace ModelingEvolution.PropertyKinds.Tests;

// =============================================================================
//  The operator model. The point of typing a value at all is that ">= / in / between" become
//  answerable; over "30 – 380 A" they are a regular expression.
//
//  What every test here is really defending is the difference between "no" and "I cannot tell you".
//  TryMatch returns those separately, and a facet that collapsed them would let a public page state a
//  relation it derived from two values that were never comparable.
// =============================================================================
public class PropertyFacetTests
{
    private static readonly PropertyKindCatalog Catalog = PropertyKindCatalog.Default;
    private static readonly ImmutableList<string> Processes = ImmutableList.Create("MIG/MAG", "TIG", "MMA");

    private static PropertyFacet Facet(PropertyValueKind kind, string canonical, string? unit = null,
        IReadOnlyList<string>? choices = null) => Catalog.Facet(kind, canonical, unit, choices);

    // ── Every kind lands in exactly one shape, and its shape is what decides its operators ──────────────────────
    [Theory]
    [InlineData(PropertyValueKind.Text, "IP54", FacetShape.Opaque)]
    [InlineData(PropertyValueKind.LongText, "a long note", FacetShape.Opaque)]
    [InlineData(PropertyValueKind.Url, "https://example.com/", FacetShape.Opaque)]
    [InlineData(PropertyValueKind.Integer, "6", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Decimal, "0.95", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Boolean, "true", FacetShape.Choice)]
    [InlineData(PropertyValueKind.Money, "3299.00 PLN", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Mass, "10 kg", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Length, "1300 mm", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Percent, "60 %", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Current, "500 A", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Voltage, "85 V", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Frequency, "25 Hz", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Power, "24 kVA", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Duration, "450 ms", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Angle, "±360°", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Speed, "1.5 m/s", FacetShape.Scalar)]
    [InlineData(PropertyValueKind.Size3D, "620 × 300 × 480 mm", FacetShape.Composite)]
    [InlineData(PropertyValueKind.Range, "30 – 500 A", FacetShape.Interval)]
    public void A_value_reduces_to_its_kinds_facet_shape(PropertyValueKind kind, string canonical, FacetShape shape)
    {
        var facet = Facet(kind, canonical, choices: null);
        facet.Shape.Should().Be(shape);
        facet.Kind.Should().Be(kind);
        facet.Operators.Should().Be(FacetOperators.For(shape));
    }

    [Fact]
    public void An_enumeration_reduces_to_a_choice()
    {
        Facet(PropertyValueKind.Enum, "TIG", choices: Processes).Shape.Should().Be(FacetShape.Choice);
        Facet(PropertyValueKind.EnumSet, "TIG, MMA", choices: Processes).Shape.Should().Be(FacetShape.Choice);
    }

    [Fact]
    public void Every_registered_kind_produces_a_facet_that_can_answer_something()
    {
        // A kind whose descriptor forgot its shape would silently refuse every search operator over every value of
        // that kind — a facet UI with one row missing and nothing failing.
        foreach (var descriptor in Catalog.All)
            descriptor.FacetShape.Should().NotBe(FacetShape.None, $"{descriptor.Kind} must declare a facet shape");
    }

    [Theory]
    [InlineData(FacetShape.Scalar, SearchOperator.GreaterThanOrEqual)]
    [InlineData(FacetShape.Scalar, SearchOperator.Between)]
    [InlineData(FacetShape.Interval, SearchOperator.Overlaps)]
    [InlineData(FacetShape.Interval, SearchOperator.Within)]
    [InlineData(FacetShape.Composite, SearchOperator.FitsWithin)]
    [InlineData(FacetShape.Choice, SearchOperator.In)]
    [InlineData(FacetShape.Opaque, SearchOperator.ContainsText)]
    public void A_shape_admits_its_own_operators(FacetShape shape, SearchOperator op) =>
        FacetOperators.For(shape).HasFlag(op).Should().BeTrue();

    [Theory]
    [InlineData(FacetShape.Scalar, SearchOperator.Overlaps)]
    [InlineData(FacetShape.Interval, SearchOperator.GreaterThan)]
    [InlineData(FacetShape.Choice, SearchOperator.LessThan)]
    [InlineData(FacetShape.Opaque, SearchOperator.GreaterThanOrEqual)]
    [InlineData(FacetShape.Composite, SearchOperator.In)]
    public void A_shape_refuses_an_operator_that_means_nothing_for_it(FacetShape shape, SearchOperator op) =>
        FacetOperators.For(shape).HasFlag(op).Should().BeFalse();

    // ── Scalars ────────────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(SearchOperator.GreaterThan, true)]
    [InlineData(SearchOperator.GreaterThanOrEqual, true)]
    [InlineData(SearchOperator.LessThan, false)]
    [InlineData(SearchOperator.Equal, false)]
    [InlineData(SearchOperator.NotEqual, true)]
    public void A_scalar_answers_the_six_comparisons(SearchOperator op, bool expected)
    {
        var payload = Facet(PropertyValueKind.Mass, "10 kg");
        var threshold = Facet(PropertyValueKind.Mass, "5 kg");
        payload.TryMatch(op, threshold, out var matches).Should().BeTrue("a scalar can answer this");
        matches.Should().Be(expected);
    }

    [Fact]
    public void A_scalar_compares_on_the_normalised_magnitude_not_on_the_unit_it_was_written_in()
    {
        // "500 g" and "0.5 kg" are the same mass. A comparison that refused them because the strings differ would
        // be as wrong as one that compared 500 against 0.5.
        var grams = Facet(PropertyValueKind.Mass, "500 g");
        var kilos = Facet(PropertyValueKind.Mass, "0.5 kg");
        grams.TryMatch(SearchOperator.Equal, kilos, out var equal).Should().BeTrue();
        equal.Should().BeTrue();
    }

    [Fact]
    public void A_scalar_is_between_an_interval()
    {
        var current = Facet(PropertyValueKind.Current, "400 A");
        var window = Facet(PropertyValueKind.Range, "30 – 500 A");
        current.TryMatch(SearchOperator.Between, window, out var inside).Should().BeTrue();
        inside.Should().BeTrue();

        Facet(PropertyValueKind.Current, "600 A").TryMatch(SearchOperator.Between, window, out var outside)
            .Should().BeTrue();
        outside.Should().BeFalse();
    }

    // ── Intervals — the shape that replaced a 50-line regex on the website ──────────────────────────────────────
    [Fact]
    public void An_interval_answers_overlap_containment_and_enclosure()
    {
        var tested = Facet(PropertyValueKind.Range, "30 – 380 A");
        var welded = Facet(PropertyValueKind.Range, "30 – 500 A");

        tested.TryMatch(SearchOperator.Overlaps, welded, out var overlaps).Should().BeTrue();
        overlaps.Should().BeTrue();

        tested.TryMatch(SearchOperator.Within, welded, out var within).Should().BeTrue();
        within.Should().BeTrue("30–380 A sits wholly inside 30–500 A");

        welded.TryMatch(SearchOperator.Within, tested, out var reversed).Should().BeTrue();
        reversed.Should().BeFalse("500 A is outside what was tested — this is the whole M-5 disclosure");

        tested.TryMatch(SearchOperator.Contains, Facet(PropertyValueKind.Current, "400 A"), out var contains)
            .Should().BeTrue();
        contains.Should().BeFalse();
    }

    [Fact]
    public void An_open_upper_bound_encloses_a_bounded_interval_above_it()
    {
        // "80 – 350 A+" is the vendor saying "and above". A range ending at 400 A therefore IS within it, and the
        // arithmetic must say so rather than stopping at the printed 350.
        var open = Facet(PropertyValueKind.Range, "80 – 350 A+");
        var bounded = Facet(PropertyValueKind.Range, "100 – 400 A");
        bounded.TryMatch(SearchOperator.Within, open, out var within).Should().BeTrue();
        within.Should().BeTrue();
    }

    [Fact]
    public void An_unbounded_interval_is_not_within_a_bounded_one()
    {
        var open = Facet(PropertyValueKind.Range, "80 – 350 A+");
        var bounded = Facet(PropertyValueKind.Range, "30 – 500 A");
        open.TryMatch(SearchOperator.Within, bounded, out var within).Should().BeTrue();
        within.Should().BeFalse("an open bound reaches past any ceiling a bounded range can offer");
    }

    // ── Composites ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void A_composite_fits_within_a_box_it_can_be_turned_to_enter()
    {
        // A 153 × 43 × 47 camera goes into a 50 × 60 × 200 opening — but only if "fits" compares sorted extents.
        // Comparing X to X would answer a question about the datasheet's writing order.
        var camera = Facet(PropertyValueKind.Size3D, "153 × 43 × 47 mm");
        var opening = Facet(PropertyValueKind.Size3D, "50 × 60 × 200 mm");
        camera.TryMatch(SearchOperator.FitsWithin, opening, out var fits).Should().BeTrue();
        fits.Should().BeTrue();

        var tooSmall = Facet(PropertyValueKind.Size3D, "50 × 60 × 100 mm");
        camera.TryMatch(SearchOperator.FitsWithin, tooSmall, out var jams).Should().BeTrue();
        jams.Should().BeFalse();
    }

    [Fact]
    public void A_composite_is_between_an_interval_on_every_axis()
    {
        var camera = Facet(PropertyValueKind.Size3D, "153 × 43 × 47 mm");
        camera.TryMatch(SearchOperator.Between, Facet(PropertyValueKind.Range, "40 – 200 mm"), out var inside)
            .Should().BeTrue();
        inside.Should().BeTrue();

        camera.TryMatch(SearchOperator.Between, Facet(PropertyValueKind.Range, "40 – 100 mm"), out var outside)
            .Should().BeTrue();
        outside.Should().BeFalse("153 mm is outside 40–100 mm and one axis out is out");
    }

    // ── Choices ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void A_choice_answers_the_four_set_relations()
    {
        var selected = Facet(PropertyValueKind.EnumSet, "MIG/MAG, TIG", choices: Processes);
        var asked = Facet(PropertyValueKind.EnumSet, "MIG/MAG, TIG, MMA", choices: Processes);

        selected.TryMatch(SearchOperator.In, asked, out var isIn).Should().BeTrue();
        isIn.Should().BeTrue();

        selected.TryMatch(SearchOperator.AnyOf, Facet(PropertyValueKind.Enum, "TIG", choices: Processes),
            out var any).Should().BeTrue();
        any.Should().BeTrue();

        selected.TryMatch(SearchOperator.AllOf, asked, out var all).Should().BeTrue();
        all.Should().BeFalse("MMA was asked for and is not selected");

        selected.TryMatch(SearchOperator.NotIn, Facet(PropertyValueKind.Enum, "MMA", choices: Processes),
            out var notIn).Should().BeTrue();
        notIn.Should().BeTrue();
    }

    [Fact]
    public void A_choice_key_containing_a_slash_stays_one_token()
    {
        // "MIG/MAG" is ONE process. A tokeniser that split on the slash would answer AnyOf("MAG") with true.
        var selected = Facet(PropertyValueKind.Enum, "MIG/MAG", choices: Processes);
        selected.Selected.Should().ContainSingle().Which.Should().Be("MIG/MAG");
    }

    [Fact]
    public void A_choice_carries_the_declared_domain_when_it_was_given_one()
    {
        var facet = Facet(PropertyValueKind.Enum, "TIG", choices: Processes);
        facet.HasDomain.Should().BeTrue();
        facet.Domain.Should().Equal("MIG/MAG", "TIG", "MMA");
    }

    [Fact]
    public void A_choice_read_without_its_declared_list_still_compares_but_cannot_be_enumerated()
    {
        // The declared choice list is DATA on the property definition, and a wire format carrying only the value has
        // not carried it. That costs the CHECKBOXES, not the arithmetic — and the caller can tell which it lost.
        var facet = Facet(PropertyValueKind.Enum, "TIG", choices: null);
        facet.HasDomain.Should().BeFalse();
        facet.Selected.Should().Equal("TIG");
        facet.TryMatch(SearchOperator.AnyOf, Facet(PropertyValueKind.Enum, "TIG", choices: null), out var any)
            .Should().BeTrue();
        any.Should().BeTrue();
    }

    [Fact]
    public void A_boolean_is_the_one_choice_whose_domain_is_known_at_compile_time()
    {
        var facet = Facet(PropertyValueKind.Boolean, "true");
        facet.HasDomain.Should().BeTrue();
        facet.Domain.Should().Equal("true", "false");
    }

    // ── Opaque ─────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Text_answers_substring_containment_and_nothing_else()
    {
        var note = Facet(PropertyValueKind.LongText, "500 A / 39 V 60% @ 40 °C");
        note.TryMatch(SearchOperator.ContainsText, Facet(PropertyValueKind.LongText, "39 v"), out var found)
            .Should().BeTrue();
        found.Should().BeTrue("containment is ordinal and case-insensitive");

        note.Supports(SearchOperator.GreaterThan).Should().BeFalse();
        note.TryMatch(SearchOperator.GreaterThan, note, out _).Should().BeFalse();
    }

    // ── REFUSAL — the half that a page's correctness actually rests on ──────────────────────────────────────────
    [Fact]
    public void Two_ranges_in_different_units_are_refused_a_comparison_rather_than_coerced()
    {
        // A range's unit comes from the property definition, so nothing normalises amperes and volts onto a common
        // magnitude. Answering "30–500 A overlaps 12–45 V" with `true` would be arithmetic on unrelated quantities.
        var amps = Facet(PropertyValueKind.Range, "30 – 500 A");
        var volts = Facet(PropertyValueKind.Range, "12 – 45 V");
        amps.TryMatch(SearchOperator.Overlaps, volts, out var matches).Should().BeFalse("the question is unanswerable");
        matches.Should().BeFalse();
    }

    [Fact]
    public void A_bare_interval_takes_its_unit_from_the_property_definition()
    {
        // A canonical usually repeats the unit ("30 – 500 A"). When it does not, the definition's declared label IS
        // the unit — and dropping it would make a bare current range and a bare voltage range look comparable,
        // which is the coercion every other refusal here exists to prevent.
        var amps = Facet(PropertyValueKind.Range, "30-500", unit: "A");
        amps.Unit.Should().Be("A");

        var volts = Facet(PropertyValueKind.Range, "12-45", unit: "V");
        amps.TryMatch(SearchOperator.Overlaps, volts, out _).Should().BeFalse();

        // …and a unit written INSIDE the canonical still wins, because that is what the value actually says.
        Facet(PropertyValueKind.Range, "30 – 500 A", unit: "V").Unit.Should().Be("A");
    }

    [Fact]
    public void Two_dimensionless_scalars_with_different_unit_labels_are_refused()
    {
        var axes = Facet(PropertyValueKind.Integer, "6", unit: "axes");
        var ports = Facet(PropertyValueKind.Integer, "4", unit: "ports");
        axes.TryMatch(SearchOperator.GreaterThan, ports, out _).Should().BeFalse();

        var morePorts = Facet(PropertyValueKind.Integer, "8", unit: "ports");
        ports.TryMatch(SearchOperator.LessThan, morePorts, out var fewer).Should().BeTrue();
        fewer.Should().BeTrue();
    }

    [Fact]
    public void Money_is_never_ordered_across_currencies_by_a_package_that_has_no_rate()
    {
        var pln = Facet(PropertyValueKind.Money, "3299.00 PLN");
        var eur = Facet(PropertyValueKind.Money, "3299.00 EUR");
        pln.TryMatch(SearchOperator.GreaterThan, eur, out _).Should().BeFalse();

        pln.Unit.Should().Be("PLN");
        pln.TryMatch(SearchOperator.LessThan, Facet(PropertyValueKind.Money, "9900.00 PLN"), out var cheaper)
            .Should().BeTrue();
        cheaper.Should().BeTrue();
    }

    [Fact]
    public void A_tolerance_is_refused_a_comparison_against_a_magnitude()
    {
        // ±0.05 mm of repeatability and 0.05 mm of reach are the same number and different quantities. Ordering
        // them would report a robot's precision as a distance.
        var repeatability = Facet(PropertyValueKind.Length, "±0.05 mm");
        var reach = Facet(PropertyValueKind.Length, "0.05 mm");
        repeatability.TryMatch(SearchOperator.Equal, reach, out _).Should().BeFalse();

        repeatability.TryMatch(SearchOperator.LessThan, Facet(PropertyValueKind.Length, "±0.1 mm"), out var tighter)
            .Should().BeTrue("two tolerances ARE comparable with each other");
        tighter.Should().BeTrue();
    }

    [Fact]
    public void A_hedge_that_only_softens_the_precision_still_compares()
    {
        // "≈40 kg" is the same quantity as "40 kg", stated less sharply. Refusing that comparison would drop a
        // sortable spec off a page for a reason nobody could see.
        var hedged = Facet(PropertyValueKind.Mass, "≈40 kg");
        var stated = Facet(PropertyValueKind.Mass, "40 kg");
        hedged.TryMatch(SearchOperator.Equal, stated, out var equal).Should().BeTrue();
        equal.Should().BeTrue();
    }

    [Fact]
    public void An_operator_asked_of_the_wrong_operand_shape_is_refused()
    {
        var window = Facet(PropertyValueKind.Range, "30 – 500 A");
        var current = Facet(PropertyValueKind.Current, "400 A");
        // `contains` takes a scalar; `overlaps` takes an interval. Handing over the other one is a question the
        // facet has not been asked, and answering it anyway would be a guess about intent.
        window.TryMatch(SearchOperator.Overlaps, current, out _).Should().BeFalse();
        window.TryMatch(SearchOperator.Contains, window, out _).Should().BeFalse();
    }

    [Fact]
    public void A_canonical_that_does_not_parse_yields_no_facet_rather_than_a_zero()
    {
        var facet = Facet(PropertyValueKind.Mass, "heavy");
        facet.Should().BeSameAs(PropertyFacet.None);
        facet.IsAnswerable.Should().BeFalse();
        facet.Operators.Should().Be(SearchOperator.None);
        facet.TryMatch(SearchOperator.Equal, Facet(PropertyValueKind.Mass, "10 kg"), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(PropertyValueKind.ByteSize)]
    [InlineData(PropertyValueKind.Size2D)]
    public void A_reserved_kind_yields_no_facet_instead_of_throwing_on_a_read_path(PropertyValueKind kind)
    {
        // Facet() is the READ side and must be total: For() is the one that fails loudly.
        Facet(kind, "whatever").Should().BeSameAs(PropertyFacet.None);
    }

    [Fact]
    public void Supports_answers_one_operator_at_a_time_not_a_combination()
    {
        var scalar = Facet(PropertyValueKind.Mass, "10 kg");
        scalar.Supports(SearchOperator.Equal).Should().BeTrue();
        scalar.Supports(SearchOperator.None).Should().BeFalse();
        scalar.Supports(SearchOperator.Equal | SearchOperator.LessThan)
            .Should().BeFalse("a caller asks for one operator; a mask would let two answer as one");
    }

    // ── The pl-PL guard, applied to the facet layer too ─────────────────────────────────────────────────────────
    [Fact]
    public void A_facets_arithmetic_does_not_move_with_the_ambient_culture()
    {
        // The apps that consume this install pl-PL process-wide, and the xunit host does not. A facet built under
        // pl-PL that disagreed with one built under invariant would make a public disclosure culture-dependent.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Facet(PropertyValueKind.Decimal, "0.95").Scalar.Should().Be(0.95m);
            Facet(PropertyValueKind.Money, "3299.00 PLN").Scalar.Should().Be(3299.00m);
            var interval = Facet(PropertyValueKind.Range, "30 – 500 A").Interval;
            interval.Should().NotBeNull();
            interval!.Value.Min.Should().Be(30m);
            interval.Value.Max.Should().Be(500m);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [MemberData(nameof(PropertyKindCatalogTests.EveryRegisteredKindsCanonical),
        MemberType = typeof(PropertyKindCatalogTests))]
    public void Every_registered_kinds_own_canonical_produces_an_answerable_facet(
        PropertyValueKind kind, string canonical)
    {
        // The pairing the design makes normative: whatever Canonicalize writes, Facet must be able to read. A kind
        // whose two halves disagreed would store values nothing could ever search.
        var descriptor = Catalog.For(kind);
        var choices = descriptor.RequiresChoices ? Processes : null;
        var normalized = descriptor.Canonicalize(canonical, choices);
        descriptor.Facet(normalized, null, choices).IsAnswerable
            .Should().BeTrue($"{kind} canonical '{normalized}' must reduce to a facet");
    }
}
