namespace Jeugdcross.Klassment.Core;

public record TeamRaceScore(string Race, int? Points, int Runners, string Status);
public record TeamRunnerScore(string Race, string Name, string Category, int Points, string Time, string Status);
public record TeamScoreDetails(List<TeamRaceScore> Races, List<TeamRunnerScore> Runners);

public static class TeamBreakdown
{
 public static TeamScoreDetails Create(Competition data, string poule, Standing team)
 {
  var races = Engine.Races(data, poule);
  var results = Engine.TeamResults(data, poule);
  var bestRaces = Enumerable.Range(0, races.Count).Where(i => team.Scores[i].HasValue)
   .OrderBy(i => team.Scores[i]).ThenBy(i => i).Take(2).ToHashSet();
  var summary = new List<TeamRaceScore>();
  var runners = new List<TeamRunnerScore>();
  for (int i = 0; i < races.Count; i++)
  {
   var race = races[i];
   var entries = results.Where(x => x.Race.Id == race.Id && Categories.Team(data,x.Row.Category) == team.Category && Parser.Key(x.Row.Association) == team.Key)
    .OrderBy(x => x.Row.Points).ThenBy(x => x.Row.Name).Select(x => x.Row).ToList();
   string label = $"W{i + 1} · {race.Label}";
   summary.Add(new(label, team.Scores[i], entries.Count,
    !team.Scores[i].HasValue ? "Minder dan 3 lopers" : bestRaces.Contains(i) ? "Telt mee in totaal" : "Niet in beste twee"));
   for (int j = 0; j < entries.Count; j++)
   {
    var entry = entries[j];
    runners.Add(new(label, entry.Name, entry.CategoryLabel, entry.Points, entry.Time,
     entries.Count < 3 ? "Te weinig lopers" : j < 3 ? "Telt mee in ploegscore" : "Buiten beste drie"));
   }
  }
  return new(summary, runners);
 }
}
