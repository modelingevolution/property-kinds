using System.Reflection;

namespace ModelingEvolution.PropertyKinds.Tests;

// =============================================================================
//  The attributes, and the boundary they must not cross.
//
//  An attribute can only label a type that exists at COMPILE TIME. A PropertyValueKind member does; a
//  property definition — "Enclosure length", "Wymiary" — does not, because it is a data row somebody
//  creates at runtime. So this package labels kinds and refuses to label properties, and the last test
//  here asserts the refusal by reflection rather than by convention.
// =============================================================================
public class KindMetadataTests
{
    private static readonly PropertyKindCatalog Catalog = PropertyKindCatalog.Default;

    private static IEnumerable<Type> BackingTypes =>
        typeof(PropertyValueKind).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<PropertyKindAttribute>() is not null);

    // ── The attribute is the single authority for a backing type's unit and shape ───────────────────────────────
    [Fact]
    public void Every_backing_type_declares_the_kind_it_backs_and_the_catalogue_agrees()
    {
        // The catalogue row READS this attribute rather than restating it, so a disagreement is structurally
        // impossible — this asserts the wiring, i.e. that a type carrying the attribute is actually registered.
        var declared = BackingTypes.Select(t => t.GetCustomAttribute<PropertyKindAttribute>()!.Kind).ToArray();
        declared.Should().OnlyHaveUniqueItems("two types backing one kind would make the catalogue ambiguous");

        foreach (var kind in declared)
        {
            Catalog.IsSupported(kind).Should().BeTrue($"{kind} has a backing type and must be registered");
            var descriptor = Catalog.For(kind);
            var attribute = BackingTypes.Single(t => t.GetCustomAttribute<PropertyKindAttribute>()!.Kind == kind)
                .GetCustomAttribute<PropertyKindAttribute>()!;
            descriptor.DefaultUnit.Should().Be(attribute.DefaultUnit, $"{kind} unit");
            descriptor.FacetShape.Should().Be(attribute.Facet, $"{kind} facet shape");
        }
    }

    [Fact]
    public void The_twelve_house_value_classes_all_carry_the_attribute()
    {
        // A value struct that lost its attribute would fail at catalogue construction — but only if it is still
        // wired in. This names the set so removing a row is a deliberate act rather than an omission.
        BackingTypes.Select(t => t.Name).Should().BeEquivalentTo(
            "Mass", "Length", "Percent", "ValueRange", "Current", "Voltage",
            "Frequency", "Size3D", "Power", "Duration", "Angle", "Speed");
    }

    [Fact]
    public void A_kind_with_no_backing_type_still_declares_its_facet_shape_on_its_catalogue_row()
    {
        // The primitives, the enumerations and Money have no struct to hang an attribute on, so their metadata is
        // inline. Neither route may leave a kind shapeless.
        var attributed = BackingTypes.Select(t => t.GetCustomAttribute<PropertyKindAttribute>()!.Kind).ToHashSet();
        foreach (var descriptor in Catalog.All.Where(d => !attributed.Contains(d.Kind)))
            descriptor.FacetShape.Should().NotBe(FacetShape.None, $"{descriptor.Kind} declares no facet shape");
    }

    // ── The localised kind labels ──────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(PropertyValueKind.Range, "pl", "Zakres")]
    [InlineData(PropertyValueKind.Range, "en", "Range")]
    [InlineData(PropertyValueKind.Range, "de", "Bereich")]
    [InlineData(PropertyValueKind.Size3D, "pl", "Wymiary")]
    [InlineData(PropertyValueKind.Mass, "pl", "Masa")]
    [InlineData(PropertyValueKind.Current, "de", "Stromstärke")]
    public void A_kind_names_itself_in_each_language(PropertyValueKind kind, string language, string expected) =>
        PropertyKindDisplay.For(kind, language).Should().Be(expected);

    [Theory]
    [InlineData("pl-PL", "Zakres")]
    [InlineData("PL", "Zakres")]
    [InlineData("de-AT", "Bereich")]
    public void A_full_culture_tag_resolves_on_its_primary_subtag(string tag, string expected) =>
        PropertyKindDisplay.For(PropertyValueKind.Range, tag).Should().Be(expected);

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unlabelled_language_falls_back_to_english_rather_than_to_nothing(string? language)
    {
        PropertyKindDisplay.For(PropertyValueKind.Range, language).Should().Be("Range");
        PropertyKindDisplay.HasLabel(PropertyValueKind.Range, language)
            .Should().BeFalse("a caller that must not present English as French can tell");
        PropertyKindDisplay.HasLabel(PropertyValueKind.Range, "pl").Should().BeTrue();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    [InlineData("de")]
    public void Every_member_including_the_reserved_ones_has_a_label_in_every_language(string language)
    {
        // A kind picker renders EVERY member, reserved ones included — a blank row would read as a bug in the UI
        // rather than as an ordinal being held.
        foreach (var kind in Enum.GetValues<PropertyValueKind>())
            PropertyKindDisplay.HasLabel(kind, language).Should().BeTrue($"{kind} in {language}");
    }

    [Fact]
    public void The_languages_the_package_carries_are_exactly_the_three_it_claims() =>
        PropertyKindDisplay.Languages.Should().BeEquivalentTo("en", "pl", "de");

    // ── THE BOUNDARY. This is the one that has to fail if somebody adds the tempting attribute. ─────────────────
    [Fact]
    public void The_package_ships_no_language_keyed_attribute_that_could_label_a_property()
    {
        // KindDisplayAttribute is deliberately restricted to enum FIELDS. Widening it to a class or a property —
        // so it could sit on something standing in for a property definition — would make it a FOURTH authority for
        // a string that already has one (the property registry's own label data), and it would go stale silently
        // the moment an administrator renamed the property.
        var usage = typeof(KindDisplayAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Field,
            "a kind label may sit on an enum member and nowhere else");

        // And no other language-keyed attribute exists to reach for instead.
        var languageAttributes = typeof(PropertyValueKind).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Attribute)))
            .Where(t => t.GetConstructors()
                .Any(c => c.GetParameters().Any(p => p.Name is "language" or "culture" or "lang")))
            .Select(t => t.Name);
        languageAttributes.Should().BeEquivalentTo(nameof(KindDisplayAttribute));
    }
}
