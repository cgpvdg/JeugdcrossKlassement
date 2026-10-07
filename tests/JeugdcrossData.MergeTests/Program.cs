using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using jeugdcrossdata.Services;

static JsonObject Pool(string name, int points) => new()
{
    ["naam"] = name, ["wedstrijden"] = new JsonArray(),
    ["individueelKlassement"] = new JsonArray(new JsonObject { ["totaal"] = points }),
    ["ploegenKlassement"] = new JsonArray()
};
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Reject(string input, string? existing)
{
    try { CompetitionDataMerger.Merge(existing, input); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Invalid input was accepted");
}
var existing = new JsonObject
{
    ["version"] = 2, ["generatedAt"] = "original", ["custom"] = new JsonObject { ["keep"] = true },
    ["poules"] = new JsonArray(Pool("Noord", 1), Pool("Midden", 2), Pool("Zuid", 3))
};
var incoming = new JsonObject { ["version"] = 2, ["generatedAt"] = "new", ["poules"] = new JsonArray(Pool("Midden", 20)) };
var oldText = existing.ToJsonString();
var mergedText = CompetitionDataMerger.Merge(oldText, incoming.ToJsonString());
var merged = JsonNode.Parse(mergedText)!;
Check(JsonNode.DeepEquals(merged["poules"]![0], existing["poules"]![0]), "Noord changed");
Check(JsonNode.DeepEquals(merged["poules"]![2], existing["poules"]![2]), "Zuid changed");
Check(JsonNode.DeepEquals(merged["poules"]![1], incoming["poules"]![0]), "Midden not replaced");
Check(merged["generatedAt"]!.GetValue<string>() == "original", "Metadata changed");
Check(JsonNode.DeepEquals(merged["custom"], existing["custom"]), "Custom data changed");
Check(CompetitionDataMerger.Merge(mergedText, incoming.ToJsonString()) == mergedText, "No-op changed text");
var multi = new JsonObject { ["poules"] = new JsonArray(Pool("Noord", 10), Pool("Zuid", 30)) };
var multiMerged = JsonNode.Parse(CompetitionDataMerger.Merge(oldText, multi.ToJsonString()))!;
Check(JsonNode.DeepEquals(multiMerged["poules"]![1], existing["poules"]![1]), "Multiple-pool merge changed Midden");
var partial = new JsonObject { ["poules"] = new JsonArray(Pool("Noord", 1)) };
var appended = JsonNode.Parse(CompetitionDataMerger.Merge(partial.ToJsonString(), incoming.ToJsonString()))!;
Check(appended["poules"]!.AsArray().Count == 2, "Missing pool not added");
Check(JsonNode.DeepEquals(JsonNode.Parse(CompetitionDataMerger.Merge(null, incoming.ToJsonString())), incoming), "New file incorrect");
Reject("{\"poules\":[]}", oldText);
Reject("{\"poules\":[{\"naam\":\"Noord\"}]}", oldText);
Reject(new JsonObject { ["poules"] = new JsonArray(Pool("Noord", 1), Pool("Noord", 2)) }.ToJsonString(), oldText);
Reject(new JsonObject { ["poules"] = new JsonArray(Pool("Onbekend", 1)) }.ToJsonString(), oldText);
Reject(incoming.ToJsonString(), "{}");
Console.WriteLine("PASS: merge preserves unrelated pools and metadata; selection, append, no-op and validation");

var tempFile = Path.GetTempFileName();
try
{
    await File.WriteAllTextAsync(tempFile, incoming.ToJsonString());
    var handler = new RecordingHandler(oldText);
    var uploader = new GitHubUploader(() => new HttpClient(handler, disposeHandler: false));
    var result = await uploader.UploadJsonAsync("test-only", "owner", "repo", "data/competitie-data.json", "master", tempFile);
    Check(result.TargetUpdated && result.ArchiveCreated, "Upload flags incorrect");
    Check(handler.Writes.Count == 2, "Expected archive followed by target upload");
    Check(handler.Writes[0].Uri.Contains("archived"), "Archive not first");
    Check(handler.Writes[0].Content == oldText, "Archive not original content");
    Check(handler.Writes[1].Sha == "original-sha", "Upload must use fetched SHA");
    Check(JsonNode.DeepEquals(JsonNode.Parse(handler.Writes[1].Content), merged), "Uploaded content not merged");

    var unchanged = new RecordingHandler(mergedText);
    var noOpUploader = new GitHubUploader(() => new HttpClient(unchanged, disposeHandler: false));
    var noOp = await noOpUploader.UploadJsonAsync("test-only", "owner", "repo", "data/competitie-data.json", "master", tempFile);
    Check(!noOp.TargetUpdated && unchanged.Writes.Count == 0, "No-op uploaded");

    await File.WriteAllTextAsync(tempFile, "{\"poules\":[]}");
    var invalid = new RecordingHandler(oldText);
    var invalidUploader = new GitHubUploader(() => new HttpClient(invalid, disposeHandler: false));
    var rejected = false;
    try { await invalidUploader.UploadJsonAsync("test-only", "owner", "repo", "data/competitie-data.json", "master", tempFile); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && invalid.Writes.Count == 0, "Invalid data wrote to GitHub");

    var siteText = "{\"welkomTekst\":\"Updated\"}";
    await File.WriteAllTextAsync(tempFile, siteText);
    var site = new RecordingHandler("{\"welkomTekst\":\"Old\"}");
    var siteUploader = new GitHubUploader(() => new HttpClient(site, disposeHandler: false));
    var siteEditor = new SiteContentEditor("{\"welkomTekst\":\"Old\"}");
    siteEditor.Fields.Single().Value = "Updated";
    await siteUploader.UploadSiteContentAsync("test-only", "owner", "repo", "data/site-content.json", "master", siteEditor);
    Check(JsonNode.DeepEquals(JsonNode.Parse(site.Writes[1].Content), JsonNode.Parse(siteText)), "Site values were not updated");
    var denied = false;
    try { await siteUploader.DownloadFileContentAsync("test-only", "owner", "repo", "data/competitie-data.json", "master"); }
    catch (InvalidOperationException) { denied = true; }
    Check(denied, "Competition download allowed");
    denied = false;
    try { await siteUploader.UploadJsonAsync("test-only", "owner", "repo", "data/site-content.json", "master", tempFile); }
    catch (InvalidOperationException) { denied = true; }
    Check(denied, "Manual site file upload allowed");
    Console.WriteLine("PASS: mocked GitHub upload archives original, merges before PUT, uses SHA, skips no-op and rejects invalid data");
}
finally { File.Delete(tempFile); }

var siteContent = File.ReadAllText("JeugdcrossCompetitie/public/data/site-content.json");
var editor = new SiteContentEditor(siteContent);
Check(editor.Fields.Count == 81, "Not all site properties exposed");
var date = editor.Fields.Single(item => item.PropertyPath == "wedstrijdOverzicht / poules / 1 / wedstrijden / 0 / datum");
Check(date.Group.Contains("Midden"), "Pool context missing");
date.Value = "2026-11-22";
var edited = JsonNode.Parse(editor.ApplyTo(siteContent))!;
Check(edited["wedstrijdOverzicht"]!["poules"]![1]!["wedstrijden"]![0]!["datum"]!.GetValue<string>() == date.Value, "Date not applied");
Check(edited["wedstrijdOverzicht"]!["poules"]!.AsArray().Count == 3, "Pools changed");
Check(edited["wedstrijdOverzicht"]!["poules"]![1]!["wedstrijden"]!.AsArray().Count == 3, "Race array changed");
var expected = JsonNode.Parse(siteContent)!;
expected["wedstrijdOverzicht"]!["poules"]![1]!["wedstrijden"]![0]!["datum"] = date.Value;
Check(JsonNode.DeepEquals(expected, edited), "Unedited properties changed");
var concurrent = JsonNode.Parse(siteContent)!;
concurrent["welkomTitel"] = "Changed elsewhere";
Check(JsonNode.Parse(editor.ApplyTo(concurrent.ToJsonString()))!["welkomTitel"]!.GetValue<string>() == "Changed elsewhere", "Concurrent unrelated change lost");
concurrent["wedstrijdOverzicht"]!["poules"]![1]!["wedstrijden"]![0]!["datum"] = "other";
var conflict = false;
try { editor.ApplyTo(concurrent.ToJsonString()); } catch (InvalidOperationException) { conflict = true; }
Check(conflict, "Concurrent edited-field conflict accepted");
var shape = JsonNode.Parse(siteContent)!;
shape["extra"] = "new";
conflict = false;
try { editor.ApplyTo(shape.ToJsonString()); } catch (InvalidOperationException) { conflict = true; }
Check(conflict, "Changed property structure accepted");
shape = JsonNode.Parse(siteContent)!;
shape["wedstrijdOverzicht"]!["poules"]!.AsArray().RemoveAt(0);
conflict = false;
try { editor.ApplyTo(shape.ToJsonString()); } catch (InvalidOperationException) { conflict = true; }
Check(conflict, "Changed array size accepted");
shape = JsonNode.Parse(siteContent)!;
var array = shape["wedstrijdOverzicht"]!["poules"]!.AsArray();
var first = array[0]!.DeepClone(); var second = array[1]!.DeepClone();
array[0] = second; array[1] = first;
conflict = false;
try { editor.ApplyTo(shape.ToJsonString()); } catch (InvalidOperationException) { conflict = true; }
Check(conflict, "Reordered pools accepted");
var typed = new SiteContentEditor("{\"count\":3,\"enabled\":true}");
typed.Fields.Single(item => item.PropertyPath == "enabled").Value = "false";
Check(JsonNode.Parse(typed.ApplyTo("{\"count\":3,\"enabled\":true}"))!["enabled"]!.GetValue<bool>() == false, "Boolean value cannot change");
typed.Fields.Single(item => item.PropertyPath == "count").Value = "\"wrong type\"";
conflict = false;
try { typed.ApplyTo("{\"count\":3,\"enabled\":true}"); } catch (InvalidOperationException) { conflict = true; }
Check(conflict, "Value type can change");
Console.WriteLine("PASS: editor preserves properties, arrays and value types; labels and concurrent changes checked");

sealed class RecordingHandler(string existing) : HttpMessageHandler
{
    public List<(string Uri, string Content, string? Sha)> Writes { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.Method == HttpMethod.Put)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!;
            Writes.Add((request.RequestUri!.ToString(), Encoding.UTF8.GetString(Convert.FromBase64String(body["content"]!.GetValue<string>())), body["sha"]?.GetValue<string>()));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
        var content = request.RequestUri!.AbsolutePath == "/user" ? "{}" : new JsonObject
        {
            ["sha"] = "original-sha", ["encoding"] = "base64",
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(existing))
        }.ToJsonString();
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
    }
}
