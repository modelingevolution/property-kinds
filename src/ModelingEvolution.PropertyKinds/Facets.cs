namespace ModelingEvolution.PropertyKinds;

// =============================================================================
//  The operator model — the reason a typed value must not cross a boundary as prose.
//
//  ">= / in / between over `30 – 380 A`" is a regular expression, not a query. A kind knows what
//  its values MEAN, so it is the kind — and only the kind — that can say which comparisons are
//  answerable over them. This file is that statement, and nothing more: it is NOT a search engine,
//  an index or a query language. It gives a later feature a typed, total predicate to build on.
//
//  TOTALITY IS THE CONTRACT. Every entry point here answers "I cannot tell you" rather than
//  guessing: an unknown kind, a reserved kind, a canonical that does not parse, two values in
//  incompatible units and an operator a shape does not support all end in the SAME place — a
//  refusal the caller can see. A facet that quietly returns `false` where it meant "unanswerable"
//  is how a disclosure disappears from a public page with nothing failing.
// =============================================================================

/// <summary>The family of comparisons a kind's values admit. Five shapes, because five is all the data has.</summary>
public enum FacetShape
{
    /// <summary>No comparison is defined — an unknown kind, a reserved kind, or a canonical that did not parse.
    /// A facet of this shape supports no operator and refuses every match.</summary>
    None = 0,

    /// <summary>One number in a known unit: mass, length, percent, current, voltage, frequency, power, duration,
    /// angle, speed, integer, decimal, money.</summary>
    Scalar = 1,

    /// <summary>A numeric interval with a unit — <see cref="PropertyValueKind.Range"/>.</summary>
    Interval = 2,

    /// <summary>n ordered scalars — <see cref="PropertyValueKind.Size3D"/>.</summary>
    Composite = 3,

    /// <summary>An ordered token set — <see cref="PropertyValueKind.Enum"/>,
    /// <see cref="PropertyValueKind.EnumSet"/>, <see cref="PropertyValueKind.Boolean"/>.</summary>
    Choice = 4,

    /// <summary>Text with no order — <see cref="PropertyValueKind.Text"/>,
    /// <see cref="PropertyValueKind.LongText"/>, <see cref="PropertyValueKind.Url"/>.</summary>
    Opaque = 5,
}

/// <summary>The comparisons a facet can be asked to answer. A flags enum so a shape declares its whole repertoire
/// in one value, and a UI can offer exactly the operators the selected property supports.</summary>
[Flags]
public enum SearchOperator
{
    /// <summary>No operator.</summary>
    None = 0,

    /// <summary>Equal. Scalar only — two numbers in a compatible unit.</summary>
    Equal = 1 << 0,

    /// <summary>Not equal. Scalar only.</summary>
    NotEqual = 1 << 1,

    /// <summary>Strictly less than. Scalar only.</summary>
    LessThan = 1 << 2,

    /// <summary>Less than or equal. Scalar only.</summary>
    LessThanOrEqual = 1 << 3,

    /// <summary>Strictly greater than. Scalar only.</summary>
    GreaterThan = 1 << 4,

    /// <summary>Greater than or equal. Scalar only.</summary>
    GreaterThanOrEqual = 1 << 5,

    /// <summary>Inside an interval. The operand is an <see cref="FacetShape.Interval"/> facet.
    /// <para>On a <see cref="FacetShape.Composite"/> receiver this is the design's "between per axis": EVERY
    /// component must fall inside the one operand interval.</para></summary>
    Between = 1 << 6,

    /// <summary>Two intervals share at least one point. Interval receiver, interval operand.</summary>
    Overlaps = 1 << 7,

    /// <summary>An interval contains a scalar. Interval receiver, <see cref="FacetShape.Scalar"/> operand.</summary>
    Contains = 1 << 8,

    /// <summary>An interval lies wholly inside another. Interval receiver, interval operand.</summary>
    Within = 1 << 9,

    /// <summary>A composite fits inside another, allowing re-orientation: each sorted extent is no larger than the
    /// operand's corresponding sorted extent. Composite receiver, composite operand.</summary>
    FitsWithin = 1 << 10,

    /// <summary>Every selected token is in the operand's set — for a single-choice property, "the value is one of
    /// these". Choice receiver, choice operand.</summary>
    In = 1 << 11,

    /// <summary>No selected token is in the operand's set. Choice receiver, choice operand.</summary>
    NotIn = 1 << 12,

    /// <summary>At least one selected token is in the operand's set. Choice receiver, choice operand.</summary>
    AnyOf = 1 << 13,

    /// <summary>Every token of the operand is selected — the receiver is a superset. Choice receiver, choice
    /// operand.</summary>
    AllOf = 1 << 14,

    /// <summary>Substring containment, ordinal and case-insensitive. Opaque receiver, opaque operand.
    /// <para>Named apart from <see cref="Contains"/> deliberately: an interval containing a number and a string
    /// containing a substring are two different questions, and one flag for both would let a caller ask the wrong
    /// one and get an answer.</para></summary>
    ContainsText = 1 << 15,
}

/// <summary>Which operators each <see cref="FacetShape"/> admits.</summary>
public static class FacetOperators
{
    /// <summary>Scalar: the six comparisons plus <see cref="SearchOperator.Between"/>.</summary>
    public const SearchOperator Scalar =
        SearchOperator.Equal | SearchOperator.NotEqual |
        SearchOperator.LessThan | SearchOperator.LessThanOrEqual |
        SearchOperator.GreaterThan | SearchOperator.GreaterThanOrEqual |
        SearchOperator.Between;

    /// <summary>Interval: overlaps, contains a scalar, within another interval.</summary>
    public const SearchOperator Interval =
        SearchOperator.Overlaps | SearchOperator.Contains | SearchOperator.Within;

    /// <summary>Composite: between (per axis) and fits-within.</summary>
    public const SearchOperator Composite = SearchOperator.Between | SearchOperator.FitsWithin;

    /// <summary>Choice: the four set relations.</summary>
    public const SearchOperator Choice =
        SearchOperator.In | SearchOperator.NotIn | SearchOperator.AnyOf | SearchOperator.AllOf;

    /// <summary>Opaque: substring containment only. There is no order to compare on.</summary>
    public const SearchOperator Opaque = SearchOperator.ContainsText;

    /// <summary>The operator set a shape admits.</summary>
    public static SearchOperator For(FacetShape shape) => shape switch
    {
        FacetShape.Scalar => Scalar,
        FacetShape.Interval => Interval,
        FacetShape.Composite => Composite,
        FacetShape.Choice => Choice,
        FacetShape.Opaque => Opaque,
        _ => SearchOperator.None,
    };
}

/// <summary>One value, reduced to the comparisons it can answer. Produced by
/// <see cref="PropertyKindCatalog.Facet(PropertyValueKind,string,string?,System.Collections.Generic.IReadOnlyList{string})"/>
/// and by <see cref="IPropertyKind.Facet"/>; pure, immutable and free of I/O, culture and any notion of what the
/// value describes.</summary>
public sealed record PropertyFacet
{
    /// <summary>The refusal. Every operator is unsupported and every match is unanswerable — which is what an
    /// unknown kind, a reserved kind and an unparseable canonical all produce.</summary>
    public static readonly PropertyFacet None = new(FacetShape.None, null, null);

    private PropertyFacet(FacetShape shape, PropertyValueKind? kind, string? unit)
    {
        Shape = shape;
        Kind = kind;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        Operators = FacetOperators.For(shape);
    }

    /// <summary>Which family of comparisons this value admits.</summary>
    public FacetShape Shape { get; }

    /// <summary>The kind the value was read under, or null when it could not be typed at all.</summary>
    public PropertyValueKind? Kind { get; }

    /// <summary>THE UNIT TWO FACETS MUST AGREE ON TO BE COMPARED — the unit the magnitudes are expressed in, not
    /// the unit they were written in.
    ///
    /// <para>For a dimensioned kind that normalises (mass, length, …) this is the kind's base unit, so
    /// <c>500 g</c> and <c>0.5 kg</c> compare equal despite being written differently. For
    /// <see cref="PropertyValueKind.Range"/> it is the unit inside the canonical, which comes from the property
    /// definition and is NOT normalised — so amperes are never silently compared with volts. For a dimensionless
    /// kind it is the property definition's own unit label, and for a choice or a text value it is null.</para></summary>
    public string? Unit { get; }

    /// <summary>The operators <see cref="Shape"/> admits. A UI offers exactly these.</summary>
    public SearchOperator Operators { get; }

    /// <summary>The vendor's hedge, carried so a comparison cannot silently sharpen it. A <see
    /// cref="ValueQualifier.Tolerance"/> is a DIFFERENT QUANTITY from a magnitude — <c>±0.03 mm</c> of
    /// repeatability is not <c>0.03 mm</c> of reach — so a comparison between a tolerance and a non-tolerance is
    /// refused rather than answered.</summary>
    public ValueQualifier Qualifier { get; private init; } = ValueQualifier.Exact;

    /// <summary>The number, for a <see cref="FacetShape.Scalar"/>; null otherwise.</summary>
    public decimal? Scalar { get; private init; }

    /// <summary>The interval, for a <see cref="FacetShape.Interval"/>; null otherwise.</summary>
    public ValueRange? Interval { get; private init; }

    /// <summary>The ordered components, for a <see cref="FacetShape.Composite"/>; empty otherwise.</summary>
    public IReadOnlyList<decimal> Components { get; private init; } = [];

    /// <summary>The selected tokens, for a <see cref="FacetShape.Choice"/>; empty otherwise.</summary>
    public IReadOnlyList<string> Selected { get; private init; } = [];

    /// <summary>Every token the property COULD have carried, in the order it declares them — the answer to "what
    /// else could this be", which is what a facet UI draws its checkboxes from.
    ///
    /// <para><b>Empty is a real and expected state.</b> The declared choice list is DATA on the property
    /// definition, and a wire format that carries only the value has not carried it. Evaluation of
    /// <see cref="SearchOperator.In"/> and its siblings does not need it and still works; ENUMERATION does, and
    /// <see cref="HasDomain"/> is how a caller finds out before drawing an empty filter panel.</para></summary>
    public IReadOnlyList<string> Domain { get; private init; } = [];

    /// <summary>The text, for a <see cref="FacetShape.Opaque"/>; null otherwise.</summary>
    public string? Text { get; private init; }

    /// <summary>Whether this facet knows the full set of tokens the property could carry. False on a
    /// <see cref="FacetShape.Choice"/> whose declared choice list was not supplied.</summary>
    public bool HasDomain => Domain.Count > 0;

    /// <summary>Whether this facet can answer anything at all.</summary>
    public bool IsAnswerable => Shape != FacetShape.None;

    /// <summary>Whether this facet admits <paramref name="op"/>. Answers false for a combination of flags — an
    /// operator is asked one at a time.</summary>
    public bool Supports(SearchOperator op) =>
        op != SearchOperator.None && (op & (op - 1)) == 0 && (Operators & op) == op;

    /// <summary>Evaluates <paramref name="op"/> against <paramref name="operand"/>.
    ///
    /// <para><b>Returns false when the question cannot be answered</b> — an unsupported operator, the wrong
    /// operand shape, incompatible units, or a tolerance compared against a magnitude — and leaves
    /// <paramref name="matches"/> false. Returns true when it WAS answered, and
    /// <paramref name="matches"/> then carries the answer. A caller that collapses the two loses the distinction
    /// between "no" and "I do not know", which is the distinction this whole package exists to preserve.</para></summary>
    public bool TryMatch(SearchOperator op, PropertyFacet operand, out bool matches)
    {
        matches = false;
        if (operand is null || !Supports(op)) return false;
        if (!IsComparableWith(operand, op)) return false;

        switch (Shape)
        {
            case FacetShape.Scalar:
                if (Scalar is not { } left) return false;
                if (op == SearchOperator.Between)
                {
                    if (operand.Interval is not { } window) return false;
                    matches = window.Contains(left);
                    return true;
                }
                if (operand.Scalar is not { } right) return false;
                matches = op switch
                {
                    SearchOperator.Equal => left == right,
                    SearchOperator.NotEqual => left != right,
                    SearchOperator.LessThan => left < right,
                    SearchOperator.LessThanOrEqual => left <= right,
                    SearchOperator.GreaterThan => left > right,
                    SearchOperator.GreaterThanOrEqual => left >= right,
                    _ => false,
                };
                return true;

            case FacetShape.Interval:
                if (Interval is not { } self) return false;
                if (op == SearchOperator.Contains)
                {
                    if (operand.Scalar is not { } point) return false;
                    matches = self.Contains(point);
                    return true;
                }
                if (operand.Interval is not { } other) return false;
                matches = op == SearchOperator.Overlaps ? self.Overlaps(other) : self.Within(other);
                return true;

            case FacetShape.Composite:
                if (Components.Count == 0) return false;
                if (op == SearchOperator.Between)
                {
                    // "between per axis": one interval, applied to every component.
                    if (operand.Interval is not { } bounds) return false;
                    matches = Components.All(bounds.Contains);
                    return true;
                }
                // FitsWithin: sorted extent against sorted extent, so a box that fits only after being turned
                // still fits. Comparing X to X would answer a question about the datasheet's writing order
                // rather than about the geometry.
                if (operand.Components.Count != Components.Count) return false;
                var mine = Components.Order().ToArray();
                var theirs = operand.Components.Order().ToArray();
                matches = !mine.Where((t, i) => t > theirs[i]).Any();
                return true;

            case FacetShape.Choice:
                if (operand.Selected.Count == 0) return false;
                var set = new HashSet<string>(operand.Selected, StringComparer.OrdinalIgnoreCase);
                matches = op switch
                {
                    SearchOperator.In => Selected.Count > 0 && Selected.All(set.Contains),
                    SearchOperator.NotIn => !Selected.Any(set.Contains),
                    SearchOperator.AnyOf => Selected.Any(set.Contains),
                    SearchOperator.AllOf => operand.Selected.All(
                        t => Selected.Contains(t, StringComparer.OrdinalIgnoreCase)),
                    _ => false,
                };
                return true;

            case FacetShape.Opaque:
                if (Text is null || operand.Text is null) return false;
                matches = Text.Contains(operand.Text, StringComparison.OrdinalIgnoreCase);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Whether two facets are in the same measurement space, so a comparison between them means something.
    /// See <see cref="Unit"/> for what "same" is, and <see cref="Qualifier"/> for why a tolerance is refused.</summary>
    private bool IsComparableWith(PropertyFacet operand, SearchOperator op)
    {
        if (!operand.IsAnswerable) return false;
        if (!string.Equals(Unit, operand.Unit, StringComparison.OrdinalIgnoreCase)) return false;

        // A hedge that changes the QUANTITY, not just its precision. ±0.03 mm and 0.03 mm are not the same
        // measurement and must not order against each other; ≈, ≥ and ≤ hedge the same quantity and do.
        var tolerance = Qualifier == ValueQualifier.Tolerance;
        if (tolerance != (operand.Qualifier == ValueQualifier.Tolerance)) return false;

        // Which operand shape each operator takes. Stated once, here, so a caller cannot get an answer to a
        // question it did not ask.
        var expected = op switch
        {
            SearchOperator.Between => FacetShape.Interval,
            SearchOperator.Contains => FacetShape.Scalar,
            _ => Shape,
        };
        return operand.Shape == expected;
    }

    /// <summary>A scalar in <paramref name="unit"/> — the unit that governs comparability, i.e. the kind's base
    /// unit or the property definition's label, never the unit the value happened to be written in.</summary>
    public static PropertyFacet OfScalar(PropertyValueKind kind, decimal value, string? unit,
        ValueQualifier qualifier = ValueQualifier.Exact) =>
        new(FacetShape.Scalar, kind, unit) { Scalar = value, Qualifier = qualifier };

    /// <summary>An interval. Its unit comes from the range itself, which is where the property definition put it.</summary>
    public static PropertyFacet OfInterval(PropertyValueKind kind, ValueRange range) =>
        new(FacetShape.Interval, kind, range.Unit) { Interval = range };

    /// <summary>n ordered components in one unit.</summary>
    public static PropertyFacet OfComposite(PropertyValueKind kind, IReadOnlyList<decimal> components, string? unit) =>
        new(FacetShape.Composite, kind, unit) { Components = [.. components] };

    /// <summary>A selection out of an ordered token set. <paramref name="domain"/> is the property definition's
    /// declared choice list and may be empty — see <see cref="Domain"/> for what that costs and what it does not.</summary>
    public static PropertyFacet OfChoice(PropertyValueKind kind, IReadOnlyList<string> selected,
        IReadOnlyList<string>? domain) =>
        new(FacetShape.Choice, kind, null) { Selected = [.. selected], Domain = domain is null ? [] : [.. domain] };

    /// <summary>Unordered text.</summary>
    public static PropertyFacet OfText(PropertyValueKind kind, string text) =>
        new(FacetShape.Opaque, kind, null) { Text = text };
}
