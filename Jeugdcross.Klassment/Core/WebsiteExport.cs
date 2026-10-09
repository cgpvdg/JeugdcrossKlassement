using System.Text.Json;
namespace Jeugdcross.Klassment.Core;
public static class WebsiteExport
{
 public static void Save(Competition data,string[] selected,string path)
 {
  var poules=selected.Select(poule=>
  {
   var races=Engine.Races(data,poule);var results=Engine.Results(data,poule);
   var individuals=Engine.Individuals(data,poule);var teams=Engine.Teams(data,poule);
   return new {
    naam=poule,
    wedstrijden=races.Select(r=>new { id=r.Id,naam=r.Name,plaats=r.Association,vereniging=r.Organizer,datum=r.Date.ToString("yyyy-MM-dd"),resultatenAantal=r.Count }),
    individueelKlassement=Categories.All.Select(category=>new { categorie=category,rows=individuals.Where(r=>r.Category==category).Select(r=>new { plaats=r.Place,naam=r.Name,vereniging=r.Association,starts=r.Starts,bonus=r.Bonus,totaal=r.Total,geplaatstVoorFinale=r.Qualified,wedstrijdPunten=races.Select((race,i)=>new {crossId=race.Id,datum=race.Date.ToString("yyyy-MM-dd"),punten=r.Scores[i]}) }) }),
    ploegenKlassement=Categories.All.Select(c=>Categories.Team(data,c)).Distinct().Select(category=>new { categorie=category,rows=teams.Where(r=>r.Category==category).Select(r=>new { plaats=r.Place,vereniging=r.Association,starts=r.Starts,totaal=r.Total,geplaatstVoorFinale=r.Qualified,wedstrijdPunten=races.Select((race,i)=>new {crossId=race.Id,datum=race.Date.ToString("yyyy-MM-dd"),punten=r.Scores[i],deelnemers=r.Scores[i].HasValue?results.Where(x=>x.Race.Id==race.Id&&Categories.Team(data,x.Row.Category)==category&&Parser.Key(x.Row.Association)==r.Key).OrderBy(x=>x.Row.Points).ThenBy(x=>x.Row.Name).Take(3).Select(x=>new { naam=x.Row.Name+(category.Contains("/")?$" ({x.Row.Category.Split(' ').Last()})":""),bronCategorie=x.Row.Category,punten=x.Row.Points,tijd=x.Row.Time }):[]}) }) })
   };
  });
  var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new {generatedAt=DateTime.UtcNow,version=2,poules},Storage.Options));File.Move(temp,path,true);
 }
}
