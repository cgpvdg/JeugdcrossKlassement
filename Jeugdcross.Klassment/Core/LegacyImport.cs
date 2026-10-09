using System.Text.Json;
namespace Jeugdcross.Klassment.Core;
public static class LegacyImport
{
 public static Competition Load(string path)
 {
  using var doc=JsonDocument.Parse(File.ReadAllText(path)); var root=doc.RootElement;
  if(!root.TryGetProperty("data",out var source))
  {
   if(!root.TryGetProperty("Races",out _)&&!root.TryGetProperty("races",out _)) throw new InvalidDataException("Gebruik de volledige gegevensexport uit de webapp, niet competitie-data.json.");
   return Storage.Load(path);
  }
  string Get(JsonElement e,string p,string fallback="")=>e.TryGetProperty(p,out var v)?v.GetString()??fallback:fallback;
  IEnumerable<JsonElement> Items(string p)=>source.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.Array?v.EnumerateArray().ToArray():[];
  string Poule(JsonElement e) { var id=Get(e,"id"); return id.StartsWith("Midden::")?"Midden":id.StartsWith("Zuid::")?"Zuid":"Noord"; }
  var data=new Competition();
  if(!source.TryGetProperty("crosses",out _)||!source.TryGetProperty("results",out _))throw new InvalidDataException("Volledige gegevensexport ontbreekt.");
  var configs=Items("crossConfigs").ToDictionary(e=>Get(e,"crossId"),e=>Get(e,"vereniging"));
  foreach(var e in Items("crosses")) data.Races.Add(new Race { Id=Get(e,"id"),Name=Get(e,"name"),Association=Get(e,"association"),Organizer=configs.GetValueOrDefault(Get(e,"id"),""),Date=DateTime.Parse(Get(e,"date")),Poule=Get(e,"poule","Noord") });
  foreach(var e in Items("results"))
  {
   var race=data.Races.FirstOrDefault(r=>r.Id==Get(e,"crossId"))??throw new InvalidDataException("Uitslag verwijst naar onbekende wedstrijd.");
   race.Results.Add(new Result { Category=Get(e,"category"),Name=Get(e,"participantName"),Association=Get(e,"association"),Time=Get(e,"time"),Rank=e.GetProperty("rank").GetInt32(),Points=e.GetProperty("points").GetInt32() });
  }
  foreach(var e in Items("participantDecisions")) data.Decisions.Add(new(Poule(e),Get(e,"category"),Get(e,"leftKey")+"::"+Get(e,"associationKey"),Get(e,"rightKey")+"::"+Get(e,"associationKey"),Get(e,"decision")=="same",Get(e,"canonicalName"),data.Races.SelectMany(r=>r.Results).FirstOrDefault(r=>Parser.Key(r.Association)==Get(e,"associationKey"))?.Association??Get(e,"associationKey"),DateTime.Parse(Get(e,"updatedAt"))));
  foreach(var e in Items("crossAssociationDecisions")) data.Decisions.Add(new(Poule(e),Get(e,"category"),Get(e,"leftParticipantKey")+"::"+Get(e,"leftAssociationKey"),Get(e,"rightParticipantKey")+"::"+Get(e,"rightAssociationKey"),Get(e,"decision")=="same",Get(e,"canonicalParticipantName"),Get(e,"canonicalAssociationName"),DateTime.Parse(Get(e,"updatedAt"))));
  foreach(var e in Items("individualNameOverrides")) data.NameOverrides[Poule(e)+"::"+Get(e,"category")+"::"+Get(e,"rowKey")]=Get(e,"displayName");
  Storage.Validate(data);return data;
 }
}
