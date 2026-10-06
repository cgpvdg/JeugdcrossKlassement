using System.Text.Json.Nodes;

namespace jeugdcrossdata.Services;

public static class CompetitionDataMerger
{
    public static string Merge(string? existingContent, string incomingContent)
    {
        var incoming = ParseObject(incomingContent, "Export");
        var incomingPools = ReadPools(incoming, "Export", requireKnownNames: true);
        if (incomingPools.Count == 0)
            throw new InvalidOperationException("De export bevat geen poules.");

        if (existingContent is null)
            return incoming.ToJsonString(new() { WriteIndented = true });

        var existing = ParseObject(existingContent, "Bestaande competitiedata");
        var existingPools = ReadPools(existing, "Bestaande competitiedata", requireKnownNames: false);
        var merged = (JsonObject)existing.DeepClone();
        var mergedPools = (JsonArray)merged["poules"]!;
        foreach (var pool in incomingPools)
        {
            var name = pool!["naam"]!.GetValue<string>();
            var index = -1;
            for (var i = 0; i < existingPools.Count; i++)
            {
                if (existingPools[i]!["naam"]!.GetValue<string>() == name)
                {
                    index = i;
                    break;
                }
            }
            if (index >= 0)
                mergedPools[index] = pool.DeepClone();
            else
                mergedPools.Add(pool.DeepClone());
        }

        // Preserve all existing top-level metadata and omit commits for unchanged pools.
        return JsonNode.DeepEquals(existing, merged)
            ? existingContent
            : merged.ToJsonString(new() { WriteIndented = true });
    }

    private static JsonObject ParseObject(string content, string label)
    {
        return JsonNode.Parse(content) as JsonObject
            ?? throw new InvalidOperationException($"{label} moet een JSON-object zijn.");
    }

    private static JsonArray ReadPools(JsonObject data, string label, bool requireKnownNames)
    {
        if (data["poules"] is not JsonArray pools)
            throw new InvalidOperationException($"{label} mist de lijst 'poules'. Gebruik het exportformaat met poules.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in pools)
        {
            if (node is not JsonObject pool || pool["naam"] is not JsonValue value
                || !value.TryGetValue<string>(out var name) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"{label} bevat een poule zonder geldige naam.");
            if (!names.Add(name))
                throw new InvalidOperationException($"{label} bevat de poule '{name}' meerdere keren.");
            if (requireKnownNames)
            {
                if (name is not ("Noord" or "Midden" or "Zuid"))
                    throw new InvalidOperationException($"Onbekende poule in export: {name}.");
                foreach (var property in new[] { "wedstrijden", "individueelKlassement", "ploegenKlassement" })
                    if (pool[property] is not JsonArray)
                        throw new InvalidOperationException($"Poule '{name}' mist de lijst '{property}'.");
            }
        }
        return pools;
    }
}
