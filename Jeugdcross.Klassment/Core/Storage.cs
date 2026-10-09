using System.Text.Json;
namespace Jeugdcross.Klassment.Core;
public static class Storage
{
 public static readonly JsonSerializerOptions Options=new() { WriteIndented=true,PropertyNameCaseInsensitive=true };
 public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Jeugdcross.Klassment");
 public static string DataPath => Path.Combine(Folder,"competitie.json");
 public static Competition Load(string path)
 {
  var data=JsonSerializer.Deserialize<Competition>(File.ReadAllText(path),Options) ?? throw new InvalidDataException("Leeg gegevensbestand.");
  Validate(data); return data;
 }
 public static void Validate(Competition data)
 {
  if(data.Version!=1 || data.Races==null || data.Decisions==null || data.NameOverrides==null||data.CategoryNames==null) throw new InvalidDataException("Ongeldig of niet ondersteund gegevensbestand.");
  if(data.Races.Select(r=>r.Id).Distinct().Count()!=data.Races.Count) throw new InvalidDataException("Dubbele wedstrijd-ID's.");
  foreach(var poule in Categories.Poules) { var races=data.Races.Where(r=>r.Poule==poule).ToList(); if(races.Count>3||races.Select(r=>r.Date.Date).Distinct().Count()!=races.Count) throw new InvalidDataException("Maximaal drie wedstrijden met verschillende datums per poule."); }
  foreach(var r in data.Races) if(!Categories.Poules.Contains(r.Poule)||string.IsNullOrWhiteSpace(r.Id)||string.IsNullOrWhiteSpace(r.Name)||r.Association==null||r.Organizer==null||r.Date==default||r.Results==null||r.Results.Any(x=>!Categories.All.Contains(x.Category)||x.Rank<1||x.Points<1||string.IsNullOrWhiteSpace(x.Name)||x.Association==null||x.Time==null)) throw new InvalidDataException("Ongeldige wedstrijd of uitslag.");
  foreach(var d in data.Decisions) if(!Categories.Poules.Contains(d.Poule)||!Categories.All.Contains(d.Category)||string.IsNullOrWhiteSpace(d.Left)||string.IsNullOrWhiteSpace(d.Right)||!d.Left.Contains("::")||!d.Right.Contains("::")||d.Association==null||(d.Same&&string.IsNullOrWhiteSpace(d.Name))) throw new InvalidDataException("Ongeldige validatiekeuze.");
  if(data.NameOverrides.Any(x=>string.IsNullOrWhiteSpace(x.Value)))throw new InvalidDataException("Ongeldige weergavenaam.");
  if(data.CategoryNames.Any(x=>!Categories.Poules.Any(p=>Categories.All.Any(c=>x.Key==p+"::"+c))||string.IsNullOrWhiteSpace(x.Value)||x.Value.Length>100))throw new InvalidDataException("Ongeldige categorienaam.");
  if(data.Races.Any(r=>r.Results.GroupBy(CategoryManagement.Source).Any(g=>g.Select(x=>x.Category).Distinct().Count()>1)))throw new InvalidDataException("Een importcategorie heeft meerdere koppelingen binnen dezelfde wedstrijd.");
 }
 public static void Save(Competition data,string? path=null)
 {
  Validate(data); path??=DataPath; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  var temp=path+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(data,Options));
  if(File.Exists(path)) File.Copy(path,path+".bak",true);
  File.Move(temp,path,true);
 }
}
