using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace Jeugdcross.Klassment.Core;

public static class Parser
{
 public static string Clean(string value) => Regex.Replace(value.Replace('\u00a0',' '), @"\s+", " ").Trim();
 public static string Key(string value) => Regex.Replace(new string(Clean(value).ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()), "[^a-z0-9]+", " ").Trim();
 public static string Read(string path)
 {
  var bytes = File.ReadAllBytes(path);
  try { return new UTF8Encoding(false,true).GetString(bytes).TrimStart('\ufeff'); }
  catch (DecoderFallbackException) { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return Encoding.GetEncoding(1252).GetString(bytes); }
 }
 public static string? Category(string line)
 {
  var s = Clean(line).ToUpperInvariant();
  var age = Regex.Match(s,@"U(20|18|16|14|13|12|11|10|9|8)\b");
  if (!age.Success) return null;
  var n = int.Parse(age.Groups[1].Value);
  // uitslagen.nl uses M for male and V for female at every age;
  // descriptive labels may mention several age groups. Explicit full gender labels remain supported.
  var code = Regex.Match(s,@"^U(20|18|16|14|13|12|11|10|9|8)-(M|V|J),");
  if(code.Success && !s.Contains("VROUWEN") && !s.Contains("MEISJES") && !s.Contains("DAMES") && !s.Contains("MANNEN") && !s.Contains("JONGENS"))
  {
   n=int.Parse(code.Groups[1].Value);
   var isFemale=code.Groups[2].Value=="V";
   return (isFemale ? n>=18?"Vrouwen":"Meisjes" : n>=18?"Mannen":"Jongens")+" U"+n;
  }
  // Full labels take precedence: U20-M, MANNEN U20 means men, not meisjes.
  bool female = s.Contains("VROUWEN") || s.Contains("MEISJES") || s.Contains("DAMES");
  bool male = s.Contains("MANNEN") || s.Contains("JONGENS");
  if (!female && !male)
  {
   female = Regex.IsMatch(s,@"^[VD]U\d") || (s.Contains("-M,") && n < 18);
   male = Regex.IsMatch(s,@"^MU\d") || s.Contains("-J,") || (s.Contains("-M,") && n >= 18);
  }
  if (!female && !male) return null;
  var label = (female ? n >=18 ? "Vrouwen" : "Meisjes" : n >=18 ? "Mannen" : "Jongens") + " U" + n;
  return Categories.All.Contains(label) ? label : null;
 }
 public static Race Parse(string text, Func<string,string?> map)
 {
  var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
  var nonempty = lines.Select(Clean).Where(l=>l.Length>0).ToArray();
  if (nonempty.Length<2 || !nonempty[0].StartsWith("Uitslagenlijst",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Kopregel 'Uitslagenlijst …' ontbreekt.");
  var race = new Race { Name = Regex.Replace(nonempty[0],"^Uitslagenlijst\\s*","",RegexOptions.IgnoreCase) };
  var meta = nonempty[1].Split(" - ",2);
  race.Association = meta[0];
  var dateText = meta.Last();
  var m = Regex.Match(dateText,@"\d{1,2}\s+[a-zA-Zà-ÿ]+\s+\d{4}|\d{1,2}[-/]\d{1,2}[-/]\d{4}");
  if (race.Name.Length==0 || !DateTime.TryParse(m.Value,CultureInfo.GetCultureInfo("nl-NL"),DateTimeStyles.None,out var date)) throw new InvalidDataException("Wedstrijdnaam of datum ontbreekt of is ongeldig.");
  race.Date = date.Date;
  string? current = null;
  string sourceCategory = "";
  int nameColumn = -1, clubColumn = -1, timeColumn = -1;
  var mappings = new Dictionary<string,string?>();
  for (int i=2;i<lines.Length;i++)
  {
   var line = lines[i]; var clean = Clean(line);
   if (IsTableHeader(clean))
   {
    nameColumn = line.IndexOf("Naam", StringComparison.OrdinalIgnoreCase);
    clubColumn = line.IndexOf("Woonplaats/Vereniging", StringComparison.OrdinalIgnoreCase);
    timeColumn = line.IndexOf("Tijd", StringComparison.OrdinalIgnoreCase);
    continue;
   }
   if (clean.Length == 0 || IsSeparator(clean)) continue;
   var r = Regex.Match(line,@"^\s*(\d+)\s+(.+?)(?:\s{2,}(.+?))?\s+(\d{1,2}:\d{2}(?::\d{2})?)\s*$");
   if (r.Success)
   {
    if (current==null) throw new InvalidDataException($"Uitslag zonder toegewezen categorie: {clean}");
    int rank = int.Parse(r.Groups[1].Value);
    if(rank<1) throw new InvalidDataException("Plaats moet groter dan nul zijn.");
    bool fixedColumns = nameColumn >= 0 && clubColumn > nameColumn && timeColumn > clubColumn && line.Length >= timeColumn;
    race.Results.Add(new Result { Category=current, SourceCategory=sourceCategory, Rank=rank, Points=rank, Name=fixedColumns ? Clean(line[nameColumn..clubColumn]) : Clean(r.Groups[2].Value), Association=fixedColumns ? Clean(line[clubColumn..timeColumn]) : Clean(r.Groups[3].Value), Time=r.Groups[4].Value });
    continue;
   }
   var known = Category(line);
   if(known!=null) { current=known; sourceCategory=clean; nameColumn=clubColumn=timeColumn=-1; continue; }
   // Unknown headers are detected by a following ranked result, never inherited from the previous block.
   if(!IsTableHeader(clean))
   {
    int next=i+1; while(next<lines.Length && Clean(lines[next]).Length==0) next++;
    bool hasTableHeader = next<lines.Length && IsTableHeader(Clean(lines[next]));
    while(next<lines.Length && (Clean(lines[next]).Length==0 || IsTableHeader(Clean(lines[next])) || IsSeparator(Clean(lines[next])))) next++;
    if(hasTableHeader || (next<lines.Length && Regex.IsMatch(lines[next],@"^\s*\d+\s+.*\d{1,2}:\d{2}")))
    {
     nameColumn=clubColumn=timeColumn=-1;
     sourceCategory=clean;
     if(!mappings.TryGetValue(clean,out current)) { current=map(clean); mappings[clean]=current; }
     if(current==null || !Categories.All.Contains(current)) throw new OperationCanceledException("Import geannuleerd: categorie niet toegewezen.");
    }
   }
  }
  var associations = race.Results.Where(r=>r.Association.Length>0).Select(r=>Key(r.Association)).ToHashSet();
  foreach(var r in race.Results.Where(r=>r.Association.Length==0))
  {
   var parts=r.Name.Split(' ');
   for(int n=Math.Min(4,parts.Length-1);n>=1;n--)
   {
    var tail=string.Join(" ",parts.TakeLast(n));
    if(!associations.Contains(Key(tail))) continue;
    r.Name=string.Join(" ",parts.SkipLast(n)); r.Association=tail; break;
   }
  }
  if(race.Results.Count==0) throw new InvalidDataException("Geen herkenbare uitslagregels gevonden.");
  return race;
 }
 private static bool IsTableHeader(string line) => Regex.IsMatch(line,@"^(Plaa|Plts|Plaats|Nr|Start|Naam|Deelnemer)\b",RegexOptions.IgnoreCase);
 private static bool IsSeparator(string line) => Regex.IsMatch(line,@"^[-=\s]+$");
}

