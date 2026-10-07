using System.Text.Json;
using System.Text.Json.Nodes;

namespace jeugdcrossdata.Services;

public sealed class SiteContentEditor
{
    private readonly JsonObject _original;
    public IReadOnlyList<SiteContentField> Fields { get; }
    public bool HasChanges => Fields.Any(inputField => inputField.IsChanged);

    public SiteContentEditor(string content)
    {
        _original = JsonNode.Parse(content) as JsonObject
            ?? throw new InvalidOperationException("Site-inhoud moet een JSON-object zijn.");
        var fields = new List<SiteContentField>();
        Walk(_original, [], "Algemeen", fields);
        Fields = fields.AsReadOnly();
    }

    public string ApplyTo(string currentContent)
    {
        var current = JsonNode.Parse(currentContent) as JsonObject
            ?? throw new InvalidOperationException("Site-inhoud moet een JSON-object zijn.");
        if (!SameShape(_original, current))
            throw new InvalidOperationException("De structuur op GitHub is gewijzigd. Haal de site-inhoud opnieuw op.");
        foreach (var inputField in Fields.Where(inputField => inputField.IsChanged))
        {
            JsonNode originalParent = _original;
            JsonNode currentParent = current;
            foreach (var segment in inputField.Path.Take(inputField.Path.Count - 1))
            {
                originalParent = Child(originalParent, segment)!;
                currentParent = Child(currentParent, segment)!;
            }
            var last = inputField.Path[^1];
            if (!JsonNode.DeepEquals(Child(originalParent, last), Child(currentParent, last)))
                throw new InvalidOperationException($"'{inputField.Group} / {inputField.Label}' is ondertussen gewijzigd op GitHub. Haal de inhoud opnieuw op.");
            var next = inputField.ParseValue();
            if (currentParent is JsonArray array) array[int.Parse(last)] = next;
            else currentParent[last] = next;
        }
        return JsonNode.DeepEquals(current, JsonNode.Parse(currentContent))
            ? currentContent : current.ToJsonString(new() { WriteIndented = true });
    }

    private static JsonNode? Child(JsonNode parent, string segment) => parent is JsonArray array
        ? array[int.Parse(segment)] : parent[segment];

    private static bool SameShape(JsonNode? left, JsonNode? right)
    {
        if (left is JsonObject obj)
            return right is JsonObject other && obj.Count == other.Count
                && obj.All(pair => other.ContainsKey(pair.Key) && SameShape(pair.Value, other[pair.Key]));
        if (left is JsonArray array)
            return right is JsonArray other && array.Count == other.Count
                && array.Select((item, index) => SameArrayItem(item, other[index])).All(value => value);
        return left?.GetValueKind() == right?.GetValueKind()
            || (left?.GetValueKind() is JsonValueKind.True or JsonValueKind.False && right?.GetValueKind() is JsonValueKind.True or JsonValueKind.False);
    }

    private static bool SameArrayItem(JsonNode? original, JsonNode? current)
    {
        // Pool/race identities keep positional edits attached to the loaded item.
        if (original is JsonObject left && current is JsonObject right)
            foreach (var identity in new[] { "naam", "titel" })
                if (left.ContainsKey(identity) && !JsonNode.DeepEquals(left[identity], right[identity]))
                    return false;
        return SameShape(original, current);
    }

    private static void Walk(JsonNode? node, string[] path, string group, List<SiteContentField> fields)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                var childPath = path.Append(pair.Key).ToArray();
                var childGroup = pair.Value is JsonObject or JsonArray
                    ? (path.Length == 0 ? Label(pair.Key) : group + " / " + Label(pair.Key)) : group;
                Walk(pair.Value, childPath, childGroup, fields);
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var title = item is JsonObject itemObject
                    ? itemObject["naam"]?.ToString() ?? itemObject["titel"]?.ToString() : null;
                Walk(item, path.Append(i.ToString()).ToArray(), group + " / " + (i + 1) + (title is null ? "" : " · " + title), fields);
            }
        }
        else fields.Add(new SiteContentField(path, group, Label(path[^1]), node));
    }

    private static string Label(string key) => key switch
    {
        "welkomTitel" => "Welkomsttitel", "welkomTekst" => "Welkomsttekst",
        "reglementUrl" => "Link naar reglement", "reglementLabel" => "Tekst reglementlink",
        "seizoenTitel" => "Seizoentitel", "seizoenTekst" => "Seizoentekst",
        "inschrijfformulierUrl" => "Link naar inschrijfformulier", "wedstrijdOverzicht" => "Wedstrijdoverzicht",
        "poules" => "Poules", "wedstrijden" => "Wedstrijden", "finale" => "Gezamenlijke finale",
        "naam" => "Poulenaam", "titel" => "Titel", "datum" => "Datum (jjjj-mm-dd)",
        "vereniging" => "Vereniging", "plaats" => "Plaats", "verenigingUrl" => "Verenigingswebsite",
        "tijdschemaUrl" => "Link naar tijdschema", "uitslagUrl" => "Link naar uitslag",
        "ploegenUitslagUrl" => "Link naar ploegenuitslag", _ => key
    };
}

public sealed class SiteContentField
{
    private readonly JsonValueKind _kind;
    internal IReadOnlyList<string> Path { get; }
    public string PropertyPath => string.Join(" / ", Path);
    public string Group { get; }
    public string Label { get; }
    public string Value { get; set; }
    public string OriginalValue { get; }
    public bool IsReadOnly => _kind == JsonValueKind.Null;
    public bool IsChanged => Value != OriginalValue;
    internal SiteContentField(string[] path, string group, string label, JsonNode? node)
    {
        Path = Array.AsReadOnly(path);
        Group = group;
        Label = label;
        _kind = node?.GetValueKind() ?? JsonValueKind.Null;
        Value = OriginalValue = node?.ToString() ?? "null";
    }
    internal JsonNode? ParseValue()
    {
        if (_kind == JsonValueKind.String) return JsonValue.Create(Value);
        if (_kind == JsonValueKind.Null)
            throw new InvalidOperationException($"'{PropertyPath}' heeft geen bewerkbare waarde.");
        JsonNode? node;
        try { node = JsonNode.Parse(Value); }
        catch (JsonException) { throw new InvalidOperationException($"Ongeldige waarde voor '{PropertyPath}'."); }
        if (node?.GetValueKind() != _kind && !(_kind is JsonValueKind.True or JsonValueKind.False && node?.GetValueKind() is JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException($"Het waardetype van '{PropertyPath}' mag niet veranderen.");
        return node;
    }
}
