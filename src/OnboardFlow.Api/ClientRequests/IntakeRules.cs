using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OnboardFlow.Api;

public static class IntakeRules
{
    public static bool Valid(IntakeField field, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000) return false;
        if (field.Id == "yearend")
            return Regex.IsMatch(value, @"^\d{2}/\d{2}$") && DateOnly.TryParseExact("2000-" + value.Replace('/', '-'),
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        return field.Kind switch
        {
            "email" => Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"),
            "tel" => value.Count(char.IsDigit) >= 10,
            "url" => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
            "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "choice" => field.Options?.Contains(value) == true,
            "attachment" => value == field.Sample,
            "confirmation" => value == field.Sample,
            _ => true
        };
    }

    public static Dictionary<string, IntakeAnswer> ValidateDrafts(JsonArray proposals, IntakeSpecification spec,
        HashSet<string> retrieved, IReadOnlyDictionary<string, IntakeAnswer> existing)
    {
        if (proposals.Count > 8) throw new IntakeException(422, "The model returned too many drafts.");
        var result = new Dictionary<string, IntakeAnswer>();
        foreach (var proposal in proposals)
        {
            if (proposal is not JsonArray row || row.Count != 4 || row.Any(x => x is not JsonValue))
                throw new IntakeException(422, "The model returned an invalid draft.");
            var id = row[0]!.GetValue<string>();
            var value = row[1]!.GetValue<string>();
            var document = row[2]!.GetValue<string>();
            var section = row[3]!.GetValue<string>();
            var field = spec.Fields.SingleOrDefault(x => x.Id == id);
            if (field is null || field.Manual || result.ContainsKey(id) || !Valid(field, value)
                || !retrieved.Contains(document) || !field.AllowedSources.Contains(document + "." + section)
                || !spec.Documents.TryGetValue(document, out var source))
                throw new IntakeException(422, "The model returned an unsupported field or source.");
            var passage = source.Sections.SingleOrDefault(x => x.Id == section);
            if (passage is null) throw new IntakeException(422, "The source passage could not be verified.");
            if (id == "tax" && (!Regex.IsMatch(value, "\\ADEMO-[A-Z0-9-]+\\z")
                || !Regex.Matches(passage.Text, "(?<![A-Z0-9-])DEMO-[A-Z0-9-]+(?![A-Z0-9-])").Any(match => match.Value == value)))
                throw new IntakeException(422, "The fictional identifier is absent from its cited source.");
            // Preserve the actual retrieved passage, never a model-invented quote.
            if (existing.ContainsKey(id)) continue;
            result.Add(id, new(value, "draft", "ai", new(document, section, passage.Text)));
        }
        return result;
    }
}
