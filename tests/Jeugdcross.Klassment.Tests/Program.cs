using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using Jeugdcross.Klassment;
using Jeugdcross.Klassment.Core;

var artifact="Jeugdcross.Klassment/artifacts"; Directory.CreateDirectory(artifact);
void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); }
var suppliedText=File.ReadAllText("tests/Jeugdcross.Klassment.Tests/uitslagen-format.txt");
var supplied=Parser.Parse(suppliedText,_=>throw new Exception("Known category incorrectly asks for mapping"));
var bussum=Parser.Parse(Parser.Read("tests/Jeugdcross.Klassment.Tests/bussum-2026.txt"),header=>throw new Exception("Unexpected mapping: "+header));
Check(bussum.Results.Select(r=>r.Category).Distinct().Order().SequenceEqual(Categories.All.Order()),"Bussum import automatically recognizes all 20 categories, including MAN/VROUW at every age");
Check(bussum.Results.GroupBy(r=>r.SourceCategory).All(g=>g.Select(r=>r.Category).Distinct().Count()==1)&&bussum.Results.Select(r=>r.SourceCategory).Distinct().Count()==20,"All 20 original source categories remain separate");
Check(supplied.Results.Count==14&&supplied.Results.Count(r=>r.Category=="Mannen U20")==12&&supplied.Results.Count(r=>r.Category=="Mannen U18")==2,"Actual uitslagen.nl Plaa format: 12 men U20 and 2 men U18");
Check(supplied.Date==new DateTime(2025,11,22)&&supplied.Association=="Monnickendam","Actual format date and location");
Check(supplied.Results[10] is { Name:"A J van (Arthur) Zweeden",Association:"Startbaan",Time:"11:13" }&&supplied.Results[12].Association=="AV '23","Fixed columns preserve names, clubs and times");
string? mappedHeader=null;
var unknown=Parser.Parse(suppliedText.Replace("U18-M, MANNEN U18","ONBEKENDE CATEGORIE"),header=>{mappedHeader=header;return "Mannen U18";});
Check(mappedHeader=="ONBEKENDE CATEGORIE"&&unknown.Results.Count==14&&unknown.Results.Last().Category=="Mannen U18","Mapping uses category above Plaa, never column or separator rows");
Check(Parser.Category("U16-M, MEISJES U16")=="Meisjes U16"&&Parser.Category("U20-V, VROUWEN U20")=="Vrouwen U20","Full gender label takes precedence over shorthand");
Check(supplied.Results[0].SourceCategory=="U20-M, MANNEN U20"&&supplied.Results[12].SourceCategory=="U18-M, MANNEN U18","TXT imports preserve exact category headers");
var originalSourceData=new Competition { Races=[Parser.Parse(suppliedText,_=>throw new Exception("Unexpected mapping"))] };
var sourceRace=originalSourceData.Races[0];
CategoryManagement.Apply(originalSourceData,"Noord",sourceRace.Id,"U20-M, MANNEN U20","Mannen U18","Junioren U18");
Check(CategoryManagement.PreferredTarget(CategoryManagement.Rows(originalSourceData,"Noord"),"U20-M, MANNEN U20")=="Mannen U18","New imports reuse the saved source-to-category mapping");
var differentSourceRace=Parser.Parse(suppliedText.Replace("22 november 2025","23 november 2025"),_=>throw new Exception("Unexpected mapping"));originalSourceData.Races.Add(differentSourceRace);
Check(CategoryManagement.PreferredTarget(CategoryManagement.Rows(originalSourceData,"Noord"),"U20-M, MANNEN U20")==null&&CategoryManagement.PreferredTarget(CategoryManagement.Rows(originalSourceData,"Noord"),"U20-M, MANNEN U20",sourceRace.Id)=="Mannen U18","Conflicting import mappings require a choice while reimports prefer the same race");
var text="Uitslagenlijst Jeugdcross\nAV Test - 9 oktober 2026\nMU16, 1500 meter\n 1 José Jansen  AV Test  4:02\n 2 Piet Test  AV Test  4:20\nONBEKENDE JEUGDGROEP\nPlts Naam Vereniging Tijd\n 1 Anna Test  AV Test  4:00\n";
int mappings=0;
var parsed=Parser.Parse(text,header=>{mappings++;return "Meisjes U14";});
Check(parsed.Results.Count==3&&mappings==1&&parsed.Results.Last().Category=="Meisjes U14","Unknown category is mapped, never inherited");
Check(parsed.Date==new DateTime(2026,10,9)&&parsed.Results[0].Name=="José Jansen"&&parsed.Results[0].Association=="AV Test","Metadata and fixed-width TXT fields");
Check(Parser.Key("  José-Jansen ")=="jose jansen","Name normalization");
try { Parser.Parse(text,_=>null); throw new Exception("Cancellation failed"); } catch(OperationCanceledException) { Check(true,"Unassigned category aborts complete import"); }
var data=new Competition();
for(int i=0;i<3;i++)
{
 var race=new Race { Name="Cross "+i,Date=new DateTime(2026,10,9).AddDays(i),Poule="Noord" };
 for(int j=0;j<12;j++) race.Results.Add(new Result { Category="Jongens U16",Name=$"Loper {j}",Association=j<6?"AV A":"AV B",Rank=j+1+i,Points=j+1+i,Time="4:02" });
 data.Races.Add(race);
}
var individual=Engine.Individuals(data,"Noord");
Check(individual.First().Total==-2&&individual.First().Bonus==5&&individual.First().Place==1,"Two best results and five-point bonus for >10 participants");
Check(individual.Count(x=>x.Qualified)==6,"Individual finalist cutoff");
var teams=Engine.Teams(data,"Noord");Check(teams.Count==2&&teams[0].Total==15&&teams[0].Starts==3,"Three runners per race, two best team scores");
Check(Engine.Individuals(data,"Midden").Count==0,"Poule isolation");
var small=new Competition { Races=data.Races.Select(r=>new Race { Name=r.Name,Date=r.Date,Results=r.Results.Take(3).Select(x=>x with {}).ToList() }).ToList() };
Check(Engine.Individuals(small,"Noord")[0].Bonus==3,"Three-point bonus for small category");
var validation=new Competition { Races=[new Race { Name="A",Date=DateTime.Today,Results=[new Result { Category="Jongens U16",Name="Johan Jansen",Association="AV A",Points=1,Rank=1 },new Result { Category="Jongens U16",Name="Johann Jansen",Association="AV A",Points=2,Rank=2 },new Result { Category="Jongens U16",Name="Johan Jansen",Association="AV B",Points=3,Rank=3 }] }] };
Check(Engine.Conflicts(validation,"Noord").Any(c=>c.Kind=="Naamvariant")&&Engine.Conflicts(validation,"Noord").Any(c=>c.Kind=="Andere vereniging"),"Same-club and cross-club validation thresholds");
var c=Engine.Conflicts(validation,"Noord").First(c=>c.Kind=="Naamvariant"); validation.Decisions.Add(new("Noord",c.Category,c.Left,c.Right,true,c.RightName,c.RightAssociation,DateTime.UtcNow));
Check(Engine.Individuals(validation,"Noord").Count==2,"Same-club merge");
c=Engine.Conflicts(validation,"Noord").First();validation.Decisions.Add(new("Noord",c.Category,c.Left,c.Right,true,c.LeftName,c.LeftAssociation,DateTime.UtcNow.AddSeconds(1)));
Check(Engine.Conflicts(validation,"Noord").Count==0&&Engine.Individuals(validation,"Noord").Count==1,"Cross-club merge resolves participant and club");
var tied=new Competition { Races=Enumerable.Range(0,2).Select(i=>new Race { Name="Tie",Date=DateTime.Today.AddDays(i),Results=[new Result { Category="Jongens U8",Name="A",Rank=1,Points=1 },new Result { Category="Jongens U8",Name="B",Rank=1,Points=1 }] }).ToList() };
Check(Engine.Individuals(tied,"Noord").All(r=>r.Place==1),"Tied points share placing");
var mixed=new Competition { Races=Enumerable.Range(0,2).Select(i=>new Race { Name="Mixed",Date=DateTime.Today.AddDays(i),Results=[new Result { Category="Mannen U18",Name="A",Association="NTB",Rank=1,Points=1,Time="4:00" },new Result { Category="Mannen U20",Name="B",Association="NTB",Rank=2,Points=2,Time="4:10" },new Result { Category="Mannen U20",Name="C",Association="NTB",Rank=3,Points=3,Time="4:20" }] }).ToList() };
Check(Engine.Teams(mixed,"Noord").Single() is { Category:"Mannen U20/U18",Total:12,Eligible:false,Place:null },"Combined U18/U20 with NTB exclusion");
var combined=new Competition();
for(int raceIndex=0;raceIndex<3;raceIndex++)
{
 var race=new Race { Name="Combined "+raceIndex,Date=new DateTime(2026,1,1).AddDays(raceIndex) };
 foreach(var gender in new[]{"Mannen","Vrouwen"})
 {
  // Combined places: B1, A1, B2, A2, A3, A4. Club A scores 2+4+5=11;
  // separate category points would incorrectly score 1+2+2=5.
  var entrants=new[]{("B1",20,"B",1),("A1",18,"A",1),("B2",20,"B",2),("A2",18,"A",2),("A3",20,"A",3),("A4",18,"A",3)};
  for(int j=0;j<entrants.Length;j++)
  {
   var (name,age,club,rank)=entrants[j];
   race.Results.Add(new Result { Category=gender+" U"+age,Name=gender+name,Association="Club "+club,Rank=rank,Points=rank,Time=$"{10+raceIndex}:{j*10:00}" });
  }
 }
 combined.Races.Add(race);
}
var combinedTeams=Engine.Teams(combined,"Noord");
Check(combinedTeams.Count==2&&combinedTeams.All(r=>r.Race1==11&&r.Race2==11&&r.Race3==11&&r.Total==22&&r.Bonus==0&&r.Eligible),"Men and women combine U20/U18 by finish time across all clubs, counting best three and best two races without bonus");
Check(Engine.Results(combined,"Noord").First(r=>r.Row.Name=="MannenA1").Row.Points==1&&Engine.Individuals(combined,"Noord").First(r=>r.Name=="MannenA1").Race1==1,"Combined team points never modify separate individual results");
var combinedDetails=TeamBreakdown.Create(combined,"Noord",combinedTeams.First());
Check(combinedDetails.Runners.Take(4).Select(r=>r.Points).SequenceEqual(new[]{2,4,5,6})&&combinedDetails.Runners[3].Status=="Buiten beste drie","Breakdown uses combined places including other clubs and excludes fourth runner");
var bussumTeam=Engine.Teams(new Competition { Races=[bussum] },"Noord").Single(r=>r.Category=="Mannen U20/U18"&&r.Association=="Atos");
Check(bussumTeam.Race1==15,"Bussum Atos men: Sven 13:47 = 2, Tijmen 14:02 = 5, Luka 14:46 = 8; combined team score is 15 rather than 8");
var tiedCombined=new Competition { Races=[new Race { Name="Equal times",Date=DateTime.Today,Results=[new Result {Category="Mannen U20",Name="A",Association="A",Time="1:00:00",Rank=1,Points=1},new Result {Category="Mannen U18",Name="B",Association="B",Time="60:00",Rank=1,Points=1},new Result {Category="Mannen U18",Name="C",Association="C",Time="60:01",Rank=2,Points=2}] }] };
Check(Engine.TeamResults(tiedCombined,"Noord").Select(x=>x.Row.Points).SequenceEqual(new[]{1,1,3}),"Combined scoring handles hour times and equal finish times with shared placing");
combined.Races.RemoveAt(2);
Check(Engine.Teams(combined,"Noord").All(r=>r.Eligible&&r.Total==22),"Two complete team results suffice for classification");
combined.Races.RemoveAt(1);
Check(Engine.Teams(combined,"Noord").All(r=>!r.Eligible&&r.Place==null),"One complete team result cannot receive a place");
var backup=Path.Combine(artifact,"test-backup.json");Storage.Save(data,backup);Storage.Save(data,backup);Check(Storage.Load(backup).Races.Count==3&&File.Exists(backup+".bak"),"Atomic storage and previous backup");
var invalid=new Competition { Races=data.Races.Append(new Race { Name="Extra",Date=DateTime.Today }).ToList() };try { Storage.Validate(invalid);throw new Exception("Limit failed"); }catch(InvalidDataException){Check(true,"Maximum three races per poule");}
foreach(var conflict in Engine.Conflicts(small,"Noord")) small.Decisions.Add(new("Noord",conflict.Category,conflict.Left,conflict.Right,false,"","",DateTime.UtcNow));
var export=Path.Combine(artifact,"test-export.xlsx");ExcelExport.Save(small,"Noord",export);
using(var book=new XLWorkbook(export)) { Check(book.Worksheets.Select(x=>x.Name).SequenceEqual(new[]{"Uitslagenlijst","Individuele klassement","Ploegen klassement"}),"Excel has exactly three ordered sheets");Check(book.Worksheet(1).Cell(2,5).GetValue<int>()==1&&book.Worksheet(1).Cell(2,8).GetString()=="4:02","Excel preserves rank and time");Check(book.Worksheet(2).Cell(2,10).GetValue<int>()==0,"Excel total matches calculation"); }
using(var book=new XLWorkbook(export))Check(!book.Worksheet(3).Row(1).CellsUsed().Any(c=>c.GetString()=="Bonus")&&book.Worksheet(3).Cell(2,9).GetValue<int>()==Engine.Teams(small,"Noord").First().Total,"Team Excel omits bonus and keeps correct total");

var legacyPath=Path.Combine(artifact,"legacy.json");
File.WriteAllText(legacyPath,"""
{"version":1,"data":{"crosses":[{"id":"old","poule":"Midden","name":"Oude cross","association":"Plaats","date":"2026-10-09"}],"results":[{"crossId":"old","category":"Jongens U16","rank":1,"points":1,"participantName":"José Jansen","participantKey":"jose jansen","association":"AV A","time":"4:02"},{"crossId":"old","category":"Jongens U16","rank":2,"points":2,"participantName":"Jos Jansen","participantKey":"jos jansen","association":"AV A","time":"4:20"}],"participantDecisions":[{"id":"Midden::test","category":"Jongens U16","associationKey":"av a","leftKey":"jose jansen","rightKey":"jos jansen","decision":"same","canonicalKey":"jose jansen","canonicalName":"José Jansen","updatedAt":"2026-10-09T00:00:00Z"}],"crossAssociationDecisions":[],"crossConfigs":[{"crossId":"old","vereniging":"Organisator"}],"individualNameOverrides":[{"id":"Midden::name","category":"Jongens U16","rowKey":"jose jansen::av a","displayName":"José aangepast"}]}}
""");
var legacy=LegacyImport.Load(legacyPath);
Check(legacy.Races[0].Poule=="Midden"&&legacy.Races[0].Association=="Plaats"&&legacy.Races[0].Organizer=="Organisator"&&Engine.Individuals(legacy,"Midden").Single().Name=="José aangepast","Legacy import preserves poule, decisions, organizer and overrides");
var websitePath=Path.Combine(artifact,"website.json");WebsiteExport.Save(small,["Noord"],websitePath);
using(var website=JsonDocument.Parse(File.ReadAllText(websitePath))) Check(website.RootElement.GetProperty("version").GetInt32()==2&&website.RootElement.GetProperty("poules")[0].GetProperty("ploegenKlassement")[0].GetProperty("categorie").GetString()=="Mannen U20/U18","Compatible version-2 website export");
Check(!Engine.FinalsReady(tied,"Noord")&&Engine.Individuals(tied,"Noord").All(x=>!x.Qualified),"Final qualification only after three populated races");
var pending=Engine.Conflicts(data,"Noord");
foreach(var choice in new[] { (Same:true,Right:false), (Same:true,Right:true), (Same:false,Right:false) })
{
 var batch=JsonSerializer.Deserialize<Competition>(JsonSerializer.Serialize(data,Storage.Options),Storage.Options)!;
 var selection=pending.Take(2).ToArray();ValidationChoices.Apply(batch,"Noord",selection,choice.Same,choice.Right);
 Check(batch.Decisions.Count==selection.Length&&batch.Decisions.Zip(selection).All(x=>x.First.Same==choice.Same&&x.First.Name==(choice.Right?x.Second.RightName:x.Second.LeftName)),"Batch applies chosen A/B/different to exactly selected conflicts");
}
var details=TeamBreakdown.Create(small,"Noord",Engine.Teams(small,"Noord").First());
Check(details.Races.Count==3&&details.Runners.Count==9&&details.Races.Where(x=>x.Status=="Telt mee in totaal").Sum(x=>x.Points)==Engine.Teams(small,"Noord").First().Total,"Team breakdown table matches best two scores and counting runners");
var categoryData=JsonSerializer.Deserialize<Competition>(JsonSerializer.Serialize(small,Storage.Options),Storage.Options)!;
var categoryRow=CategoryManagement.Rows(categoryData,"Noord").First();
Check(!categoryRow.OriginalHeaderAvailable,"Older imports explicitly identify missing original headers");
int decisionsBefore=categoryData.Decisions.Count;
CategoryManagement.Apply(categoryData,"Noord",categoryRow.RaceId,categoryRow.Source,"Jongens U16","Jeugd U16");
Check(Engine.Individuals(categoryData,"Noord").All(r=>r.CategoryLabel=="Jeugd U16")&&Engine.Teams(categoryData,"Noord").All(r=>r.CategoryLabel=="Jeugd U16")&&categoryData.Decisions.Count==decisionsBefore&&Categories.Display(categoryData,"Midden","Jongens U16")=="Jongens U16","Category rename propagates throughout poule without altering validation decisions or other poules");
var renamedExcel=Path.Combine(artifact,"category-names.xlsx");ExcelExport.Save(categoryData,"Noord",renamedExcel);
using(var book=new XLWorkbook(renamedExcel))Check(book.Worksheet(1).Cell(2,4).GetString()=="Jeugd U16"&&book.Worksheet(2).Cell(2,1).GetString()=="Jeugd U16"&&book.Worksheet(3).Cell(2,1).GetString()=="Jeugd U16","All three Excel sheets use edited category name");
categoryData.NameOverrides["Noord::Jongens U16::"+categoryData.Races[0].Results[0].Node]="Eigen weergavenaam";
CategoryManagement.Apply(categoryData,"Noord",categoryRow.RaceId,categoryRow.Source,"Jongens U14","Jeugd U14");
Check(Engine.Individuals(categoryData,"Noord").Where(r=>r.Category=="Jongens U14").All(r=>r.Starts==1&&r.Place==null)&&Engine.Individuals(categoryData,"Noord").Where(r=>r.Category=="Jongens U16").All(r=>r.Starts==2&&r.Bonus==0)&&Engine.Teams(categoryData,"Noord").Single(r=>r.Category=="Jongens U14").Starts==1,"Mapping change recomputes participant starts, bonus, placing and team scores");
Check(categoryData.Decisions.Count==0&&categoryData.NameOverrides.ContainsKey("Noord::Jongens U14::"+categoryData.Races[0].Results[0].Node)&&CategoryManagement.Rows(categoryData,"Noord").First().Source==categoryRow.Source&&!CategoryManagement.Rows(categoryData,"Noord").First().OriginalHeaderAvailable,"Remapping preserves source provenance and display names while resetting affected identity checks");
CategoryManagement.Apply(categoryData,"Noord",categoryRow.RaceId,categoryRow.Source,"Jongens U16","Jeugd U16");
Check(Engine.Individuals(categoryData,"Noord").All(r=>r.Starts==3&&r.Bonus==3)&&Engine.Teams(categoryData,"Noord").Single().Total==15,"Mapping can be reverted using unchanged imported source");
var categoryFile=Path.Combine(artifact,"category-persistence.json");Storage.Save(categoryData,categoryFile);var reloadedCategories=Storage.Load(categoryFile);
Check(reloadedCategories.CategoryNames["Noord::Jongens U16"]=="Jeugd U16"&&reloadedCategories.Races[0].Results[0].SourceCategory==categoryRow.Source,"Category names and mappings persist after restart");
try { CategoryManagement.Apply(categoryData,"Noord",categoryRow.RaceId,categoryRow.Source,"Jongens U16","Mannen U20");throw new Exception("Duplicate category name accepted"); }catch(InvalidDataException){Check(true,"Category names cannot disguise two distinct categories with the same label");}
var sortRows=new List<Standing> {
 new() { Name="Empty A" }, new() { Name="Three",Place=3,Scores=[3,3,3] },
 new() { Name="One",Place=1,Scores=[1,1,1] }, new() { Name="Empty B" }, new() { Name="Two",Place=2,Scores=[2,2,2] }
};
foreach(var member in new[]{"Place","Race1","Race2","Race3"})
foreach(var direction in new[]{System.ComponentModel.ListSortDirection.Ascending,System.ComponentModel.ListSortDirection.Descending})
{
 var view=new System.Windows.Data.ListCollectionView(sortRows) { CustomSort=new StandingNumberComparer(member,direction) };
 var expectedNames=direction==System.ComponentModel.ListSortDirection.Ascending?new[]{"One","Two","Three","Empty A","Empty B"}:new[]{"Three","Two","One","Empty A","Empty B"};
 Check(view.Cast<Standing>().Select(r=>r.Name).SequenceEqual(expectedNames),$"{member} {direction}: numbers sorted with blanks last");
}

if(File.Exists(Path.Combine(artifact,"parity.json")))
{
 using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact,"parity.json")));
 foreach(var fixture in doc.RootElement.EnumerateArray())
 {
  var candidate=JsonSerializer.Deserialize<Competition>(fixture.GetProperty("input").GetRawText(),Storage.Options)!;
  var expected=fixture.GetProperty("individual").EnumerateArray().ToArray(); var actual=Engine.Individuals(candidate,"Noord");
  Check(actual.Count==expected.Length&&actual.Zip(expected).All(pair=>pair.First.Name==pair.Second.GetProperty("name").GetString()&&pair.First.Total==pair.Second.GetProperty("total").GetInt32()&&pair.First.Place==(pair.Second.GetProperty("place").ValueKind==JsonValueKind.Null?null:pair.Second.GetProperty("place").GetInt32())&&pair.First.Qualified==pair.Second.GetProperty("qualified").GetBoolean()),"Individual parity with original Vue: "+fixture.GetProperty("label").GetString());
  if(candidate.Races.SelectMany(r=>r.Results).Any(r=>r.Category is "Mannen U20" or "Mannen U18" or "Vrouwen U20" or "Vrouwen U18"))continue; // Combined scoring intentionally corrects the original Vue calculation.
  expected=fixture.GetProperty("teams").EnumerateArray().ToArray(); actual=Engine.Teams(candidate,"Noord");
  Check(actual.Count==expected.Length&&actual.Zip(expected).All(pair=>pair.First.Association==pair.Second.GetProperty("association").GetString()&&pair.First.Total==pair.Second.GetProperty("total").GetInt32()&&pair.First.Place==(pair.Second.GetProperty("place").ValueKind==JsonValueKind.Null?null:pair.Second.GetProperty("place").GetInt32())&&pair.First.Qualified==pair.Second.GetProperty("qualified").GetBoolean()),"Team parity with original Vue: "+fixture.GetProperty("label").GetString());
 }
}
Exception? failure=null;
var thread=new Thread(()=> { try {
 var app=new App();app.InitializeComponent();var uiDataPath=Path.Combine(Path.GetFullPath(artifact),"ui-category-save.json");var window=new MainWindow(uiDataPath);
 typeof(MainWindow).GetField("data",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,small);
 typeof(MainWindow).GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);
 var menu=(ListBox)window.FindName("Menu"); var poule=(ComboBox)window.FindName("PouleBox");
 var snapshot=typeof(MainWindow).GetField("pageData",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window);
 for(int i=0;i<7;i++) { menu.SelectedIndex=i;WaitForLoad(window);Check(((TextBlock)window.FindName("Heading")).Text.Length>0,"WPF menu page "+i); }
 Check(ReferenceEquals(snapshot,typeof(MainWindow).GetField("pageData",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(window)),"Navigation reuses calculated data");
 Check(((TextBlock)window.FindName("Heading")).Text=="Exporteren"&&Descendants((DependencyObject)window.FindName("ExportPanel")).OfType<Button>().Count()==1,"Exporteren only exposes Excel export");
 Check(!((DataGrid)window.FindName("ResultsGrid")).Columns.Any(c=>c.Header.ToString()=="Categorie"),"Result list omits category column");
 poule.SelectedIndex=1;WaitForLoad(window);Check(((DataGrid)window.FindName("RaceGrid")).Items.Count==0,"WPF poule switch");poule.SelectedIndex=0;menu.SelectedIndex=4;WaitForLoad(window);
 Check(((TextBlock)window.FindName("Summary")).Text.StartsWith("Poule Noord"),"Latest navigation wins when requests overlap");
 var resultsCategory=(ComboBox)window.FindName("ResultCategory"); var standingCategory=(ComboBox)window.FindName("StandingCategory");
 Check(resultsCategory.Items.Count==Categories.All.Length&&resultsCategory.SelectedIndex==0&&standingCategory.Items.Count==Categories.All.Length&&standingCategory.SelectedIndex==0,"WPF filters omit all-categories and initially select first category");
 standingCategory.SelectedValue="Jongens U16";
 Check(((DataGrid)window.FindName("StandingGrid")).Items.Count==3,"WPF standings binding for selected category");
 menu.SelectedIndex=5;WaitForLoad(window);Check(standingCategory.SelectedIndex==0&&standingCategory.SelectedValue.ToString()=="Mannen U20/U18","Team filter opens first combined category");
 Check(((DataGrid)window.FindName("StandingGrid")).Columns.Single(c=>c.Header?.ToString()=="Bonus").Visibility==Visibility.Collapsed,"Team view hides the bonus column");
 var breakdownWindow=new TeamBreakdownWindow(small,"Noord",Engine.Teams(small,"Noord").First());
 Check(((DataGrid)breakdownWindow.FindName("RaceScoresGrid")).Items.Count==3&&((DataGrid)breakdownWindow.FindName("RunnerScoresGrid")).Items.Count==9,"WPF breakdown uses two bound tables");
 Render(breakdownWindow,"team-breakdown-preview.png");breakdownWindow.Close();
 var filterData=JsonSerializer.Deserialize<Competition>(JsonSerializer.Serialize(data,Storage.Options),Storage.Options)!;
 filterData.Races[0].Results.Add(filterData.Races[0].Results[0] with { Association="AV C" });
 typeof(MainWindow).GetField("data",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,filterData);
 menu.SelectedIndex=2;WaitForLoad(window);var conflictsGrid=(DataGrid)window.FindName("ConflictGrid");conflictsGrid.SelectedItems.Add(conflictsGrid.Items[0]);conflictsGrid.SelectedItems.Add(conflictsGrid.Items[1]);
 Check(conflictsGrid.SelectionMode==DataGridSelectionMode.Extended&&((Button)window.FindName("OptionAButton")).IsEnabled&&((TextBlock)window.FindName("ConflictSelectionCount")).Text=="2 geselecteerd","WPF conflict multi-selection enables bulk choices");
 Check(conflictsGrid.Columns[0] is DataGridTemplateColumn,"WPF conflict rows have individual choice buttons");
 var kindFilter=(ComboBox)window.FindName("ConflictKindFilter");kindFilter.SelectedItem="Andere vereniging";
 Check(conflictsGrid.Items.Count>0&&conflictsGrid.Items.Cast<Conflict>().All(c=>c.Kind=="Andere vereniging")&&conflictsGrid.SelectedItems.Count==0,"Kind filter restricts checks and clears hidden selections");
 kindFilter.SelectedIndex=0;
 Render(window,"name-controls-preview.png");
 var importButton=(Wpf.Ui.Controls.Button)window.FindName("ImportResultsButton");Check(importButton.Content.ToString()=="Uitslagen.nl importeren"&&importButton.HorizontalAlignment==HorizontalAlignment.Right,"Import button renamed and aligned right");
 menu.SelectedIndex=1;WaitForLoad(window);
 Check(((ListBoxItem)menu.Items[1]).Content.ToString()=="Categorieën"&&((DataGrid)window.FindName("CategoryMappingGrid")).Items.Count==3,"Categories navigation lists imported mappings per race");
 var mappingGrid=(DataGrid)window.FindName("CategoryMappingGrid");mappingGrid.SelectedIndex=0;
 Check(((DataGrid)window.FindName("CategoryResultsGrid")).Items.Count==13,"Selecting category shows all its imported result rows");
 ((TextBox)window.FindName("CategoryDisplayName")).Text="Junioren test";
 ((Button)window.FindName("SaveCategoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitForLoad(window);
 Check(Storage.Load(uiDataPath).CategoryNames["Noord::Jongens U16"]=="Junioren test"&&((CategoryMappingRow)mappingGrid.Items[0]).Name=="Junioren test","Category editor saves display name and refreshes mapping table");
 ((ComboBox)window.FindName("CategoryTarget")).SelectedValue="Jongens U14";((TextBox)window.FindName("CategoryDisplayName")).Text="Junioren test U14";
 ((Button)window.FindName("SaveCategoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));WaitForLoad(window);
 Check(((CategoryMappingRow)mappingGrid.Items[0]).Target=="Jongens U14"&&Storage.Load(uiDataPath).Races[0].Results.All(r=>r.Category=="Jongens U14"),"Category editor saves revised result mapping");
 Render(window,"categories-preview.png");
 menu.SelectedIndex=4;WaitForLoad(window);standingCategory.SelectedValue="Jongens U14";
 Check(((CategoryOption)standingCategory.SelectedItem).Name=="Junioren test U14"&&((DataGrid)window.FindName("StandingGrid")).Items.Cast<Standing>().All(r=>r.Starts==1&&r.Place==null&&r.CategoryLabel=="Junioren test U14"),"Navigation uses updated category label and recalculated standings");
 menu.SelectedIndex=4;WaitForLoad(window);standingCategory.SelectedValue="Jongens U16";
 var statusFilter=(ComboBox)window.FindName("StandingStatusFilter");statusFilter.SelectedItem="Finalist";
 var standingGrid=(DataGrid)window.FindName("StandingGrid");
 Check(standingGrid.Items.Count==6&&standingGrid.Items.Cast<Standing>().All(s=>s.Status=="Finalist"),"Status filter restricts individual standings");
 statusFilter.SelectedItem="Geklasseerd";Check(standingGrid.Items.Count==6&&standingGrid.Items.Cast<Standing>().All(s=>s.Status=="Geklasseerd"),"Status filter changes to classified participants");statusFilter.SelectedIndex=0;
 foreach(var page in new[]{4,5})
 {
  menu.SelectedIndex=page;WaitForLoad(window);
  foreach(var member in new[]{"Place","Race1","Race2","Race3"})
  {
   standingGrid.ItemsSource=sortRows.ToList();var column=standingGrid.Columns.First(c=>c.SortMemberPath==member);column.SortDirection=null;
   for(int click=0;click<2;click++)
   {
    var sortEvent=new DataGridSortingEventArgs(column);typeof(MainWindow).GetMethod("StandingSorting",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object[]{standingGrid,sortEvent});
    Check(sortEvent.Handled&&standingGrid.Items.Cast<Standing>().Select(s=>s.Name).SequenceEqual(click==0?new[]{"One","Two","Three","Empty A","Empty B"}:new[]{"Three","Two","One","Empty A","Empty B"}),$"WPF page {page}: {member} click {click+1} keeps blanks last");
   }
  }
 }
 menu.SelectedIndex=4;WaitForLoad(window);standingCategory.SelectedValue="Jongens U16";
 window.Show();
 var root=(FrameworkElement)window.Content;root.Measure(new Size(1320,806));root.Arrange(new Rect(0,0,1320,806));root.UpdateLayout();
 System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
 root.UpdateLayout();
 var bitmap=new RenderTargetBitmap(1320,806,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(artifact,"wpf-preview.png"));encoder.Save(stream);
 typeof(MainWindow).GetField("data",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,new Competition { Races=[supplied] });
 menu.SelectedIndex=1;WaitForLoad(window);
 Check(mappingGrid.Items.Count==2&&mappingGrid.Items.Cast<CategoryMappingRow>().All(r=>r.OriginalHeaderAvailable)&&((DataGrid)window.FindName("CategoryResultsGrid")).Items.Count==12,"Categories screen displays actual imported headers and their linked participants");
 var categoryRaceFilter=(ComboBox)window.FindName("CategoryRaceFilter");
 var secondRace=Parser.Parse(suppliedText,_=>throw new Exception("Unexpected mapping"));
 typeof(MainWindow).GetField("data",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(window,new Competition { Races=[supplied,secondRace] });
 typeof(MainWindow).GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,null);
 categoryRaceFilter.SelectedValue=secondRace.Id;
 Check(mappingGrid.Items.Count==2&&mappingGrid.Items.Cast<CategoryMappingRow>().All(r=>r.RaceId==secondRace.Id),"Category race filter displays only the selected race");
 categoryRaceFilter.SelectedValue="";
 Check(mappingGrid.Items.Count==4,"All races filter restores all category mappings");
 Render(window,"categories-import-preview.png");window.Close();
 } catch(Exception ex) { failure=ex; } });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;
Console.WriteLine("All checks passed.");
void Render(Window window,string name)
{
 window.Show();window.UpdateLayout();System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);window.UpdateLayout();
 var root=(FrameworkElement)window.Content;
 var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);
 var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(artifact,name));encoder.Save(output);
}
void WaitForLoad(MainWindow window)
{
 var panel=(FrameworkElement)window.FindName("LoadingPanel");
 if(panel.Visibility!=Visibility.Visible)return;
 Check(!((FrameworkElement)window.FindName("ContentArea")).IsEnabled,"Loading indicator blocks stale page actions");
 var frame=new System.Windows.Threading.DispatcherFrame();var deadline=DateTime.UtcNow.AddSeconds(30);bool timeout=false;
 var timer=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromMilliseconds(5) };
 timer.Tick+=(_,_)=> { if(panel.Visibility!=Visibility.Visible||DateTime.UtcNow>deadline) { timeout=panel.Visibility==Visibility.Visible;frame.Continue=false; } };
 timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);timer.Stop();if(timeout)throw new Exception("Page loading timed out");
}
static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
{
 for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var item in Descendants(child))yield return item; }
}
