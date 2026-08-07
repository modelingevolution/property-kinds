namespace ModelingEvolution.PropertyKinds.Tests;

// =============================================================================
//  THE VERSION-SKEW GUARD.
//
//  The producer of typed values and the consumer of them deploy independently, so there is guaranteed
//  to be a window in which the producer emits a kind the consumer's copy of this package does not know
//  — the day a kind is added, which is the whole point of the catalogue being extensible.
//
//  Deserialising straight into the CLR enum turns that window into a parse failure, and a parse failure
//  on a read path takes the page down: one defect traded for a worse one. So the typed read is TOTAL,
//  and these tests feed it a kind the library does not know.
// =============================================================================
public class PropertyKindRefTests
{
    [Theory]
    [InlineData("Range", PropertyValueKind.Range)]
    [InlineData("range", PropertyValueKind.Range)]
    [InlineData("SIZE3D", PropertyValueKind.Size3D)]
    [InlineData("  Mass  ", PropertyValueKind.Mass)]
    [InlineData("16", PropertyValueKind.Range)]
    public void A_known_token_reads_as_its_kind(string token, PropertyValueKind expected)
    {
        var read = PropertyKindRef.Read(token);
        read.IsKnown.Should().BeTrue();
        read.Known.Should().Be(expected);
        read.Is(expected).Should().BeTrue();
    }

    [Fact]
    public void A_kind_this_build_does_not_know_reads_as_unknown_and_never_throws()
    {
        // "Torque" is the shape of the real failure: a kind added to the producer's catalogue and shipped before
        // the consumer was redeployed.
        var read = PropertyKindRef.Read("Torque");
        read.IsKnown.Should().BeFalse();
        read.Known.Should().BeNull();
        read.Is(PropertyValueKind.Text).Should().BeFalse("an unknown kind is not accidentally ordinal zero");
    }

    [Fact]
    public void An_unknown_token_is_carried_verbatim_so_it_can_be_named_in_a_log()
    {
        // "the website is behind the ERP" is only actionable if the log says WHICH kind. A generic "unrecognised
        // value" would leave an operator diffing two enums by hand.
        PropertyKindRef.Read("Torque").Token.Should().Be("Torque");
        PropertyKindRef.Read("Torque").ToString().Should().Be("Torque");
    }

    [Fact]
    public void An_ordinal_with_no_member_is_unknown_rather_than_a_kind_that_does_not_exist()
    {
        // Enum.TryParse accepts ANY integer for any enum. Without the IsDefined guard, "99" would become a "known"
        // kind with no descriptor — worse than an honest Unknown, because it passes IsKnown.
        var read = PropertyKindRef.Read("99");
        read.IsKnown.Should().BeFalse();
        read.Token.Should().Be("99");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_token_is_the_unknown_sentinel(string? token)
    {
        var read = PropertyKindRef.Read(token);
        read.Should().Be(PropertyKindRef.Unknown);
        read.Token.Should().BeEmpty();
        read.ToString().Should().Be("Unknown", "a rendered cell and a log line both need something to say");
    }

    [Fact]
    public void The_default_value_is_unknown_not_the_zero_ordinal()
    {
        // A struct field nobody assigned must not silently claim to be Text — the first ordinal is a real kind, and
        // defaulting to it would type every unset value as a string.
        default(PropertyKindRef).IsKnown.Should().BeFalse();
        default(PropertyKindRef).Token.Should().BeEmpty();
    }

    [Fact]
    public void Every_member_of_the_catalogue_round_trips_through_its_own_token()
    {
        foreach (var kind in Enum.GetValues<PropertyValueKind>())
        {
            var reference = PropertyKindRef.Of(kind);
            reference.Token.Should().Be(kind.ToString());
            PropertyKindRef.Read(reference.ToString()).Known.Should().Be(kind);
        }
    }

    [Fact]
    public void Parsing_is_total_because_an_unrecognised_kind_is_a_state_and_not_a_failure()
    {
        PropertyKindRef.TryParse("Torque", null, out var parsed).Should().BeTrue();
        parsed.IsKnown.Should().BeFalse();

        var act = () => PropertyKindRef.Parse("Torque");
        act.Should().NotThrow("a read path that can throw is a read path that can take a page down");
    }

    [Fact]
    public void An_unknown_kind_refuses_every_facet_rather_than_guessing_one()
    {
        // The consequence that matters. The row still renders — the producer emitted its own display string — but
        // no disclosure is ever computed from a kind nobody understands.
        var unknown = PropertyKindRef.Read("Torque");
        var facet = PropertyKindCatalog.Default.Facet(unknown, "42 Nm", "Nm", null);
        facet.Should().BeSameAs(PropertyFacet.None);
        facet.IsAnswerable.Should().BeFalse();
        facet.Operators.Should().Be(SearchOperator.None);
    }

    [Fact]
    public void A_known_kind_read_off_a_wire_still_facets_normally()
    {
        var known = PropertyKindRef.Read("Range");
        PropertyKindCatalog.Default.Facet(known, "30 – 500 A", null, null)
            .Shape.Should().Be(FacetShape.Interval);
    }
}
