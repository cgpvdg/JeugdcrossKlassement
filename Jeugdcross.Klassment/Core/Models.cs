namespace Jeugdcross.Klassment.Core;

public static class Categories
{
 public static readonly string[] All = ["Mannen U20", "Mannen U18", "Vrouwen U20", "Vrouwen U18", "Jongens U16", "Meisjes U16", "Jongens U14", "Meisjes U14", "Jongens U13", "Meisjes U13", "Jongens U8", "Meisjes U8", "Jongens U9", "Meisjes U9", "Jongens U10", "Meisjes U10", "Jongens U11", "Meisjes U11", "Jongens U12", "Meisjes U12"];
 public static readonly string[] Poules = ["Noord", "Midden", "Zuid"];
 public static string Team(string category) => category is "Mannen U20" or "Mannen U18" ? "Mannen U20/U18" : category is "Vrouwen U20" or "Vrouwen U18" ? "Vrouwen U20/U18" : category;
 public static string Team(Competition data,string category) => data.Configuration.TeamGroups.FirstOrDefault(g=>g.Categories.Contains(category))?.Key??category;
 public static string Display(Competition data,string poule,string key)
 {
  if(data.CategoryNames.TryGetValue(poule+"::"+key,out var name))return name;
  var group=data.Configuration.TeamGroups.FirstOrDefault(g=>g.Key==key);
  if(group!=null)return string.Join(" / ",group.Categories.Select(c=>Display(data,poule,c)));
  if(key is "Mannen U20/U18" or "Vrouwen U20/U18")
  {
   var prefix=key.Split(' ')[0];var first=prefix+" U20";var second=prefix+" U18";
   if(data.CategoryNames.ContainsKey(poule+"::"+first)||data.CategoryNames.ContainsKey(poule+"::"+second))return Display(data,poule,first)+" / "+Display(data,poule,second);
  }
  return key;
 }
}
public record Result
{
 public string Category { get; set; } = "";
 public string SourceCategory { get; set; } = "";
 public bool SourceCategoryInferred { get; set; }
 public string CategoryLabel { get; set; } = "";
 public int Rank { get; set; }
 public int Points { get; set; }
 public string Name { get; set; } = "";
 public string Association { get; set; } = "";
 public string Time { get; set; } = "";
 public string Node => Parser.Key(Name) + "::" + Parser.Key(Association);
}
public class Race
{
 public string Id { get; set; } = Guid.NewGuid().ToString();
 public string Poule { get; set; } = "Noord";
 public string Name { get; set; } = "";
 public string Association { get; set; } = "";
 public DateTime Date { get; set; }
 public string Organizer { get; set; } = "";
 public List<Result> Results { get; set; } = [];
 public string Label => $"{Date:dd-MM-yyyy} · {Name}";
 public int Count => Results.Count;
}
public record Decision(string Poule, string Category, string Left, string Right, bool Same, string Name, string Association, DateTime UpdatedAt);
public class Competition
{
 public int Version { get; set; } = 1;
 public List<Race> Races { get; set; } = [];
 public List<Decision> Decisions { get; set; } = [];
 public Dictionary<string,string> NameOverrides { get; set; } = [];
 public Dictionary<string,string> CategoryNames { get; set; } = [];
 public CompetitionConfiguration Configuration { get; set; } = new();
}
public class CompetitionConfiguration
{
 public int BonusStarts { get; set; } = 3;
 public int BonusPoints { get; set; } = 5;
 public int SmallCategoryBonusPoints { get; set; } = 3;
 public List<TeamCategoryGroup> TeamGroups { get; set; } = [new() { Categories=["Mannen U20","Mannen U18"] },new() { Categories=["Vrouwen U20","Vrouwen U18"] }];
}
public class TeamCategoryGroup
{
 public List<string> Categories { get; set; } = [];
 public string Key => Categories.Count==2&&Categories.All(c=>c is "Mannen U20" or "Mannen U18")?"Mannen U20/U18":Categories.Count==2&&Categories.All(c=>c is "Vrouwen U20" or "Vrouwen U18")?"Vrouwen U20/U18":string.Join(" + ",Categories.OrderBy(c=>Array.IndexOf(global::Jeugdcross.Klassment.Core.Categories.All,c)));
 public string Name => Categories.Count==0?"Nieuwe groep (selecteer categorieën)":string.Join(" / ",Categories);
}
public record Conflict(string Category, string Left, string Right, string LeftName, string RightName, string LeftAssociation, string RightAssociation, double Similarity)
{
 public string CategoryLabel { get; init; } = "";
 public string Kind => Parser.Key(LeftAssociation) == Parser.Key(RightAssociation) ? "Naamvariant" : "Andere vereniging";
 public string Score => Similarity.ToString("P0");
}
public class Standing
{
 public string Category { get; set; } = "";
 public string CategoryLabel { get; set; } = "";
 public string Key { get; set; } = "";
 public string Name { get; set; } = "";
 public string Association { get; set; } = "";
 public int?[] Scores { get; set; } = new int?[3];
 public int? Race1 => Scores[0]; public int? Race2 => Scores[1]; public int? Race3 => Scores[2];
 public int Starts => Scores.Count(x => x.HasValue);
 public int Bonus { get; set; }
 public int Total { get; set; }
 public int? Place { get; set; }
 public bool Eligible { get; set; }
 public bool Qualified { get; set; }
 public string Status => Qualified ? "Finalist" : Eligible ? "Geklasseerd" : "Onvoldoende starts / uitgesloten";
 public string Breakdown { get; set; } = "";
}
