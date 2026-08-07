using System.Collections.Frozen;
using System.Reflection;

namespace ModelingEvolution.PropertyKinds;

// =============================================================================
//  What an attribute can and cannot label — the boundary this package turns on.
//
//  An attribute can only label a type that EXISTS AT COMPILE TIME. That is not a style preference;
//  it is the whole constraint. A `PropertyValueKind` member exists at compile time, so it can carry
//  a localised label. A *property definition* — "Enclosure length", "Dimensions", "Optical path" —
//  does NOT: it is a data row an administrator creates at runtime, and no attribute can label a row
//  that did not exist when the assembly was built.
//
//  So this package ships NO attribute keyed by language for a property's NAME or for an enum
//  CHOICE's label. Not "discouraged" — ABSENT, so it cannot be reached for. Those are data in
//  whatever registry owns the property definitions, and a fourth authority for a string that
//  already has three is exactly the defect this design exists to remove.
//
//  See design-typed-property-contract.md §3 and §4.
// =============================================================================

/// <summary>Declares which <see cref="PropertyValueKind"/> a value type backs, together with the metadata the
/// catalogue row for that kind is built from. Applied to the twelve value structs in <c>Dimensions.cs</c>.
///
/// <para>This is the SINGLE authority for a dimensioned kind's default unit and facet shape:
/// <see cref="PropertyKindCatalog"/> reads the attribute rather than restating it, so the two cannot drift.
/// The kinds with no backing struct in this package (the primitives, the enumerations and
/// <see cref="PropertyValueKind.Money"/>) declare the same metadata inline on their catalogue row, because
/// there is no type there to hang an attribute on.</para></summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PropertyKindAttribute(PropertyValueKind kind) : Attribute
{
    /// <summary>The catalogue member this type backs.</summary>
    public PropertyValueKind Kind { get; } = kind;

    /// <summary>The unit the kind's canonical form carries intrinsically ("kg", "mm", "A", "%"), or null when the
    /// kind is dimensionless and the property definition supplies a display unit label instead.</summary>
    public string? DefaultUnit { get; init; }

    /// <summary>Which family of search operators values of this kind support.</summary>
    public FacetShape Facet { get; init; } = FacetShape.None;

    /// <summary>Whether the canonical form already carries its own unit ("10 kg"), in which case a property
    /// definition's display label is NOT appended on render.</summary>
    public bool UnitIsIntrinsic { get; init; } = true;
}

/// <summary>A localised display name for a <see cref="PropertyValueKind"/>, applied to the enum member itself —
/// e.g. <c>[KindDisplay("Zakres", "pl")]</c>. Repeat it once per language.
///
/// <para><b>This labels the KIND, never a property.</b> It is what a search UI's operator picker needs
/// ("Zakres: od … do …"). <b>It is not a specification-table heading and must never be used as one</b>: a heading
/// names a property — "Enclosure length", "Wymiary" — and a property is a data row whose name an administrator can
/// change without recompiling anything. Reaching for this text to head a table would publish a stale name the day
/// somebody renames the property, and would do it silently.</para></summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class KindDisplayAttribute(string text, string language) : Attribute
{
    /// <summary>The kind's name in <see cref="Language"/>.</summary>
    public string Text { get; } = text;

    /// <summary>An IETF language tag — "en", "pl", "de". Matched case-insensitively, primary subtag only.</summary>
    public string Language { get; } = language;
}

/// <summary>Resolves a <see cref="PropertyValueKind"/>'s localised name from its <see cref="KindDisplayAttribute"/>s.
/// Read once into a frozen table at first use; pure and allocation-free thereafter.
///
/// <para>Read the warning on <see cref="KindDisplayAttribute"/> before using this. It names the KIND — "Range",
/// "Zakres", "Bereich" — and never the property that happens to be of that kind.</para></summary>
public static class PropertyKindDisplay
{
    /// <summary>The language every kind is guaranteed to have a label in, and therefore the fallback.</summary>
    public const string FallbackLanguage = "en";

    private static readonly FrozenDictionary<(PropertyValueKind Kind, string Language), string> Labels = Read();

    /// <summary>The kind's name in <paramref name="language"/>, falling back to <see cref="FallbackLanguage"/> and
    /// then to the enum member's own name. Never throws and never returns null or empty — a caller rendering an
    /// operator picker gets something readable for every member, including a reserved one.
    ///
    /// <para><paramref name="language"/> is matched on its PRIMARY SUBTAG, so "pl-PL", "pl" and "PL" all resolve
    /// the Polish label. A null or blank language is the fallback language.</para></summary>
    public static string For(PropertyValueKind kind, string? language)
    {
        var tag = PrimarySubtag(language);
        if (tag.Length > 0 && Labels.TryGetValue((kind, tag), out var text)) return text;
        if (Labels.TryGetValue((kind, FallbackLanguage), out var fallback)) return fallback;
        return kind.ToString();
    }

    /// <summary>Whether an explicit label was authored for this kind in this language — as opposed to the answer
    /// <see cref="For"/> gave being a fallback. A caller that must not present English text as Polish asks this.</summary>
    public static bool HasLabel(PropertyValueKind kind, string? language)
    {
        var tag = PrimarySubtag(language);
        return tag.Length > 0 && Labels.ContainsKey((kind, tag));
    }

    /// <summary>Every language any kind carries a label in, lower-cased primary subtags, unordered.</summary>
    public static IReadOnlySet<string> Languages { get; } =
        Labels.Keys.Select(k => k.Language).ToFrozenSet(StringComparer.Ordinal);

    private static string PrimarySubtag(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "";
        var text = language.Trim();
        var dash = text.IndexOf('-');
        if (dash > 0) text = text[..dash];
        return text.ToLowerInvariant();
    }

    private static FrozenDictionary<(PropertyValueKind, string), string> Read()
    {
        var map = new Dictionary<(PropertyValueKind, string), string>();
        foreach (var field in typeof(PropertyValueKind).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var kind = (PropertyValueKind)field.GetValue(null)!;
            foreach (var attribute in field.GetCustomAttributes<KindDisplayAttribute>())
                map[(kind, PrimarySubtag(attribute.Language))] = attribute.Text;
        }
        return map.ToFrozenDictionary();
    }
}
