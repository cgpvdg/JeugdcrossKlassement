namespace Jeugdcross.Klassment.Core;

public record CategoryOption(string Key,string Name);
public record CategoryMappingRow(string RaceId,string Race,string Source,string Target,string Name,int ResultsCount,bool OriginalHeaderAvailable)
{
 public string SourceNote => OriginalHeaderAvailable ? "Kop uit TXT-bestand" : "Oude import: oorspronkelijke kop niet bewaard";
}

public static class CategoryManagement
{
 public static string Source(Result row) => string.IsNullOrWhiteSpace(row.SourceCategory)?row.Category:row.SourceCategory;
 public static List<CategoryMappingRow> Rows(Competition data,string poule) => Engine.Races(data,poule)
  .SelectMany(r=>r.Results.GroupBy(Source).Select(g=>new CategoryMappingRow(r.Id,r.Label,g.Key,g.First().Category,Categories.Display(data,poule,g.First().Category),g.Count(),g.All(x=>!string.IsNullOrWhiteSpace(x.SourceCategory)&&!x.SourceCategoryInferred))))
  .ToList();
 public static string? PreferredTarget(IEnumerable<CategoryMappingRow> mappings,string source,string? raceId=null)
 {
  var matches=mappings.Where(m=>string.Equals(m.Source,source,StringComparison.OrdinalIgnoreCase)).ToList();
  var sameRace=matches.Where(m=>m.RaceId==raceId).ToList();
  if(sameRace.Count>0)matches=sameRace;
  var targets=matches.Select(m=>m.Target).Distinct().ToList();
  return targets.Count==1?targets[0]:null;
 }

 public static void Apply(Competition data,string poule,string raceId,string source,string target,string name)
 {
  name=name.Trim();
  if(!Categories.All.Contains(target))throw new InvalidDataException("Kies een geldige categorie.");
  if(name.Length==0||name.Length>100)throw new InvalidDataException("Vul een categorienaam van 1 tot 100 tekens in.");
  if(Categories.All.Any(c=>c!=target&&string.Equals(Categories.Display(data,poule,c),name,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Deze naam wordt al door een andere categorie gebruikt.");
  var race=data.Races.SingleOrDefault(r=>r.Id==raceId&&r.Poule==poule)??throw new InvalidDataException("Wedstrijd niet gevonden in deze poule.");
  var entries=race.Results.Where(r=>Source(r)==source).ToList();
  if(entries.Count==0)throw new InvalidDataException("Geen uitslagen gevonden voor deze geïmporteerde categorie.");
  var affected=entries.Where(r=>r.Category!=target).Select(r=>r.Category).ToHashSet();
  if(affected.Count>0)
  {
   var resolved=Engine.Results(data,poule).Where(x=>x.Race.Id==race.Id).Select(x=>x.Row).ToList();
   foreach(var entry in entries.Where(r=>r.Category!=target))
   {
    var previous=resolved[race.Results.IndexOf(entry)];
    var oldKey=poule+"::"+entry.Category+"::"+previous.Node;
    var newKey=poule+"::"+target+"::"+entry.Node;
    if(data.NameOverrides.TryGetValue(oldKey,out var displayName))data.NameOverrides.TryAdd(newKey,displayName);
   }
   affected.Add(target);
   // Identity choices are tied to a category population. Revalidate both affected populations.
   data.Decisions.RemoveAll(d=>d.Poule==poule&&affected.Contains(d.Category));
  }
  foreach(var entry in entries) { if(string.IsNullOrWhiteSpace(entry.SourceCategory))entry.SourceCategoryInferred=true;entry.SourceCategory=source;entry.Category=target; }
  var nameKey=poule+"::"+target;
  if(name==target)data.CategoryNames.Remove(nameKey);else data.CategoryNames[nameKey]=name;
 }
}
