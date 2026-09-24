namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The checks that need the whole document: what a condition points at, what a
/// calculation reads, what a limit is measured against, and whether the
/// calculations run in a circle (T-24).
///
/// A field parses fine on its own and still leaves the renderer with nothing
/// to draw: a condition on a field that was deleted, a sum over a column that
/// holds dates, a value that computes itself. All three reach the database if
/// nobody asks these questions at save time.
/// </summary>
internal static class FormSchemaReferences
{
    /// <summary>
    /// Competition settings a limit may be measured against, written in the
    /// document as "competition.maxGrantAmount". They stay OUT of the
    /// definition on purpose: the same form serves competitions with different
    /// ceilings, and a limit copied into the document would be the number that
    /// is wrong next year.
    /// </summary>
    private static readonly HashSet<string> CompetitionParameters =
        new(StringComparer.Ordinal)
        {
            "competition.maxGrantAmount",
            "competition.minGrantAmount",
            "competition.totalPoolAmount",
            "competition.maxIndirectCostPercent",
            "competition.maxInstitutionalDevelopmentPercent",
            "competition.maxAverageAnnualRevenue",
        };

    /// <summary>
    /// The competition settings a percentage ceiling may take its percentage
    /// from (FormLimit.PercentFrom). Only thresholds: a percentage taken from
    /// an amount would read 9000 as nine thousand per cent.
    /// </summary>
    private static readonly HashSet<string> PercentParameters =
        new(StringComparer.Ordinal)
        {
            "competition.maxIndirectCostPercent",
            "competition.maxInstitutionalDevelopmentPercent",
        };

    public static void Check(FormJsonReader reader, FormDocument document)
    {
        var fields = document.AllFields().ToList();
        var byKey = new Dictionary<string, FormFieldPath>(StringComparer.Ordinal);
        var order = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < fields.Count; index++)
        {
            byKey[fields[index].Key] = fields[index];
            order[fields[index].Key] = index;
        }

        CheckSectionConditions(reader, document, byKey, order);

        foreach (var entry in fields)
        {
            CheckCondition(reader, entry, byKey, order);
            CheckCalculation(reader, entry, byKey);
            CheckLimits(reader, entry, byKey);
        }

        CheckCalculationCycles(reader, fields, byKey);
    }

    /// <summary>
    /// The key a reference written inside a table means. A column says
    /// "liczba" about its own row, and the same word outside the table means a
    /// field of the form: resolving the sibling first is what lets the budget
    /// row multiply units by price without naming its own table.
    /// </summary>
    private static string Resolve(
        string reference,
        FormField? owningTable,
        IReadOnlyDictionary<string, FormFieldPath> byKey)
    {
        if (owningTable is null)
        {
            return reference;
        }

        var sibling = $"{owningTable.Key}.{reference}";

        return byKey.ContainsKey(sibling) ? sibling : reference;
    }

    private static void CheckSectionConditions(
        FormJsonReader reader,
        FormDocument document,
        IReadOnlyDictionary<string, FormFieldPath> byKey,
        IReadOnlyDictionary<string, int> order)
    {
        for (var index = 0; index < document.Sections.Count; index++)
        {
            var section = document.Sections[index];

            if (section.VisibleWhen is not { } condition)
            {
                continue;
            }

            // The position of the section is the position of its first field.
            // Counting declared fields instead would drift by one per table
            // column, because a column is a field of the document too, and a
            // section standing after the budget table would be refused for
            // pointing at an answer given above it.
            CheckConditionTarget(
                reader,
                condition,
                $"sekcja \"{section.Key}\"",
                order[section.Fields[0].Key],
                owningTable: null,
                $"$.sections[{index}]",
                byKey,
                order);
        }
    }

    private static void CheckCondition(
        FormJsonReader reader,
        FormFieldPath entry,
        IReadOnlyDictionary<string, FormFieldPath> byKey,
        IReadOnlyDictionary<string, int> order)
    {
        if (entry.Field.VisibleWhen is not { } condition)
        {
            return;
        }

        CheckConditionTarget(
            reader,
            condition,
            $"pole \"{entry.Key}\"",
            order[entry.Key],
            entry.Table,
            entry.JsonPath,
            byKey,
            order);
    }

    private static void CheckConditionTarget(
        FormJsonReader reader,
        FormCondition condition,
        string named,
        int position,
        FormField? owningTable,
        string jsonPath,
        IReadOnlyDictionary<string, FormFieldPath> byKey,
        IReadOnlyDictionary<string, int> order)
    {
        var path = $"{jsonPath}.visibleWhen";
        var key = Resolve(condition.Field, owningTable, byKey);

        if (!byKey.TryGetValue(key, out var target))
        {
            reader.Add(
                path,
                $"Warunek widoczności dla {named} wskazuje na pole "
                + $"\"{condition.Field}\", którego w formularzu nie ma.");
            return;
        }

        if (order[key] >= position)
        {
            reader.Add(
                path,
                $"Warunek widoczności dla {named} wskazuje na pole "
                + $"\"{condition.Field}\", które stoi niżej w formularzu: "
                + "odsłonić może tylko odpowiedź już udzielona.");
            return;
        }

        if (target.Table is not null && !ReferenceEquals(target.Table, owningTable))
        {
            reader.Add(
                path,
                $"Warunek widoczności dla {named} wskazuje na kolumnę tabeli "
                + $"\"{condition.Field}\", która ma tyle wartości, ile wierszy.");
            return;
        }

        if (target.Field.Type == FormFieldType.YesNo)
        {
            foreach (var value in condition.EqualsAnyOf.Where(
                value => value is not ("true" or "false")))
            {
                reader.Add(
                    path,
                    $"Warunek dla {named}: pole \"{condition.Field}\" odpowiada "
                    + $"się tak albo nie, a warunek czeka na \"{value}\".");
            }

            return;
        }

        if (!FormFieldTypes.IsChoice(target.Field.Type))
        {
            reader.Add(
                path,
                $"Warunek widoczności dla {named} wskazuje na pole "
                + $"\"{condition.Field}\", którego odpowiedzi nie da się porównać "
                + "z listą wartości.");
            return;
        }

        foreach (var value in condition.EqualsAnyOf.Where(
            value => target.Field.Options.All(option => option.Value != value)))
        {
            reader.Add(
                path,
                $"Warunek dla {named}: pole \"{condition.Field}\" nie ma opcji "
                + $"o wartości \"{value}\", więc warunek nigdy się nie spełni.");
        }
    }

    private static void CheckCalculation(
        FormJsonReader reader,
        FormFieldPath entry,
        IReadOnlyDictionary<string, FormFieldPath> byKey)
    {
        if (entry.Field.Calculation is not { } calculation)
        {
            return;
        }

        var path = $"{entry.JsonPath}.calculation";

        foreach (var operand in calculation.Operands)
        {
            var key = Resolve(operand, entry.Table, byKey);

            if (key == entry.Key)
            {
                reader.Add(
                    path,
                    $"Pole wyliczane \"{entry.Key}\" liczy się samo z siebie.");
                continue;
            }

            if (!byKey.TryGetValue(key, out var source))
            {
                reader.Add(
                    path,
                    $"Pole wyliczane \"{entry.Key}\" czyta pole \"{operand}\", "
                    + "którego w formularzu nie ma.");
                continue;
            }

            if (!FormFieldTypes.IsNumeric(source.Field.Type))
            {
                reader.Add(
                    path,
                    $"Pole wyliczane \"{entry.Key}\" czyta pole \"{operand}\", "
                    + "które nie jest liczbą.");
                continue;
            }

            CheckOperandPlacement(reader, entry, calculation, source, operand, path);
        }
    }

    /// <summary>
    /// Where the operand is allowed to stand. A sum reaches down a table
    /// column, or adds up fields outside any table (T-31: the total of the
    /// three cost tables); everything else works inside one row, or outside
    /// tables entirely. A product reading a whole column has no single value
    /// to multiply, and that is a definition, not a fill-in mistake.
    /// </summary>
    private static void CheckOperandPlacement(
        FormJsonReader reader,
        FormFieldPath entry,
        FormCalculation calculation,
        FormFieldPath source,
        string operand,
        string path)
    {
        if (calculation.Kind == FormCalculationKind.Sum)
        {
            if (source.Table is null && entry.Table is not null)
            {
                reader.Add(
                    path,
                    $"Pole wyliczane \"{entry.Key}\" sumuje pole \"{operand}\", "
                    + "które nie jest kolumną tabeli.");
            }

            return;
        }

        if (source.Table is not null && !ReferenceEquals(source.Table, entry.Table))
        {
            reader.Add(
                path,
                $"Pole wyliczane \"{entry.Key}\" czyta kolumnę \"{operand}\" z "
                + "innej tabeli niż własna, a kolumna ma tyle wartości, ile wierszy.");
        }
    }

    private static void CheckLimits(
        FormJsonReader reader,
        FormFieldPath entry,
        IReadOnlyDictionary<string, FormFieldPath> byKey)
    {
        foreach (var limit in entry.Field.Limits)
        {
            var path = $"{entry.JsonPath}.limits";

            if (limit.PercentFrom is { } percentFrom && !PercentParameters.Contains(percentFrom))
            {
                reader.Add(
                    path,
                    $"Limit pola \"{entry.Key}\" bierze procent z ustawienia "
                    + $"\"{percentFrom}\", które nie jest progiem procentowym konkursu.");
            }

            if (limit.Basis.StartsWith("competition.", StringComparison.Ordinal))
            {
                if (!CompetitionParameters.Contains(limit.Basis))
                {
                    reader.Add(
                        path,
                        $"Limit pola \"{entry.Key}\" odwołuje się do ustawienia "
                        + $"konkursu \"{limit.Basis}\", którego konkurs nie ma.");
                }

                continue;
            }

            if (!byKey.TryGetValue(
                Resolve(limit.Basis, entry.Table, byKey),
                out var basis))
            {
                reader.Add(
                    path,
                    $"Limit pola \"{entry.Key}\" mierzy je względem pola "
                    + $"\"{limit.Basis}\", którego w formularzu nie ma.");
                continue;
            }

            if (!FormFieldTypes.IsNumeric(basis.Field.Type))
            {
                reader.Add(
                    path,
                    $"Limit pola \"{entry.Key}\" mierzy je względem pola "
                    + $"\"{limit.Basis}\", które nie jest liczbą.");
            }
        }
    }

    /// <summary>
    /// A calculation reading a calculation that reads the first one back. The
    /// renderer would recompute the pair until the browser gives up, so the
    /// document is refused instead.
    /// </summary>
    private static void CheckCalculationCycles(
        FormJsonReader reader,
        IReadOnlyList<FormFieldPath> fields,
        IReadOnlyDictionary<string, FormFieldPath> byKey)
    {
        var settled = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in fields.Where(field => field.Field.Calculation is not null))
        {
            Walk(entry.Key, []);
        }

        void Walk(string key, HashSet<string> visiting)
        {
            if (settled.Contains(key) || !byKey.TryGetValue(key, out var entry))
            {
                return;
            }

            if (entry.Field.Calculation is not { } calculation)
            {
                settled.Add(key);
                return;
            }

            if (!visiting.Add(key))
            {
                if (reported.Add(key))
                {
                    reader.Add(
                        $"{entry.JsonPath}.calculation",
                        $"Pole wyliczane \"{key}\" liczy się z pola, które liczy "
                        + "się z niego: obliczenie nigdy się nie skończy.");
                }

                return;
            }

            foreach (var operand in calculation.Operands)
            {
                Walk(Resolve(operand, entry.Table, byKey), visiting);
            }

            visiting.Remove(key);
            settled.Add(key);
        }
    }
}
