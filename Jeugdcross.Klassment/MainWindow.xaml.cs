using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using Microsoft.Win32;
using Jeugdcross.Klassment.Core;
namespace Jeugdcross.Klassment;
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
 private Competition data=new(); private bool ready; private bool refreshing; private bool dataLoadFailed;
 private long refreshRevision;
 private PageData? pageData;
 private string? standingSortMember;
 private ListSortDirection standingSortDirection;
 private enum Page { Races,Categories,NameChecks,Results,Individuals,Teams,Export }
 private Page CurrentPage => (Page)Menu.SelectedIndex;
 private readonly string dataFilePath;
 private record PageData(Competition Source,string Poule,List<Race> Races,List<Conflict> Conflicts,List<(Race Race,Result Row)> Results,List<Standing> Individuals,List<Standing> Teams);
 private string Poule => PouleBox.SelectedItem as string ?? "Noord";
 public MainWindow() : this(Storage.DataPath) { }
 public MainWindow(string dataFilePath)
 {
  this.dataFilePath=dataFilePath;
  InitializeComponent(); PouleBox.ItemsSource=Categories.Poules; PouleBox.SelectedIndex=0;
  ConflictKindFilter.ItemsSource=new[]{"Alle soorten","Naamvariant","Andere vereniging"}; ConflictKindFilter.SelectedIndex=0;
  StandingStatusFilter.ItemsSource=new[]{"Alle statussen","Finalist","Geklasseerd","Onvoldoende starts / uitgesloten"}; StandingStatusFilter.SelectedIndex=0;
  if(File.Exists(dataFilePath)) try { data=Storage.Load(dataFilePath); } catch(Exception ex) { MessageBox.Show("Gegevens konden niet worden geladen. Het bestand blijft behouden. Sluit de applicatie en herstel de automatische .bak-kopie van:\n"+dataFilePath+"\n\n"+ex.Message,"Gegevens laden",MessageBoxButton.OK,MessageBoxImage.Error); dataLoadFailed=true; }
  ready=true; Refresh();
  if(dataLoadFailed) Notice.Text="Gegevens konden niet worden geladen. Herstel het lokale gegevensbestand om verder te gaan.";
 }
 private void Run(Action action) { try { if(dataLoadFailed)throw new InvalidOperationException("Herstel eerst het lokale gegevensbestand vanuit de automatische .bak-kopie.");action(); } catch(OperationCanceledException) { Notice.Text="Bewerking geannuleerd."; } catch(Exception ex) { Notice.Text="Bewerking mislukt: "+ex.Message; MessageBox.Show(ex.Message,"Jeugdcross",MessageBoxButton.OK,MessageBoxImage.Error); } }
 private void Commit(Competition next,string message)
 {
  Storage.Save(next,dataFilePath); data=next; Refresh(); Notice.Text=message;
 }
 private Competition Clone() => System.Text.Json.JsonSerializer.Deserialize<Competition>(System.Text.Json.JsonSerializer.Serialize(data,Storage.Options),Storage.Options)!;
 private async void PouleChanged(object sender,SelectionChangedEventArgs e) { if(ready) await RefreshAsync(); }
 private async void MenuChanged(object sender,SelectionChangedEventArgs e) { if(ready) await RefreshAsync(true); }
 private void FilterChanged(object sender,SelectionChangedEventArgs e) { if(ready&&!refreshing) RefreshTables(); }
 private void Refresh()
 {
  refreshRevision++; SetLoading(false);
  ApplyPageData(BuildPageData(data,Poule));
 }
 private PageData BuildPageData(Competition source,string poule)
 {
  var cached=pageData;
  if(cached?.Source==source&&cached.Poule==poule) return cached;
  return new(source,poule,Engine.Races(source,poule),Engine.Conflicts(source,poule),Engine.Results(source,poule),Engine.Individuals(source,poule),Engine.Teams(source,poule));
 }
 private async Task RefreshAsync(bool resetCategory=false,string? successMessage=null)
 {
  long revision=++refreshRevision;
  var source=data; var poule=Poule;
  SetLoading(true);
  PageData? loaded=null;
  Exception? failure=null;
  try
  {
   loaded=await Task.Run(()=>BuildPageData(source,poule)).ConfigureAwait(false);
  }
  catch(Exception ex) { failure=ex; }
  if(Dispatcher.HasShutdownStarted)return;
  try
  {
   await Dispatcher.InvokeAsync(()=>
   {
    if(revision!=refreshRevision)return;
    try { if(failure!=null) Notice.Text="Laden mislukt: "+failure.Message; else { ApplyPageData(loaded!,resetCategory);if(successMessage!=null)Notice.Text=successMessage; } }
    finally { SetLoading(false); }
   });
  }
  catch(OperationCanceledException) when(Dispatcher.HasShutdownStarted) { }
 }
 private void SetLoading(bool loading)
 {
  LoadingPanel.Visibility=loading?Visibility.Visible:Visibility.Collapsed;
  ContentArea.IsEnabled=!loading;
 }
 private void ApplyPageData(PageData loaded,bool resetCategory=false)
 {
  pageData=loaded;
  refreshing=true;
  var races=loaded.Races; var conflicts=loaded.Conflicts;
  Heading.Text=((ListBoxItem)Menu.SelectedItem).Content.ToString();
  Summary.Text=$"Poule {Poule} · {races.Count}/3 wedstrijden · {races.Sum(r=>r.Count)} uitslagen · {conflicts.Count} openstaande naamcontroles";
  Notice.Text=conflicts.Count>0 ? "Controleer de mogelijke dubbele deelnemers. Klassementen zijn voorlopig zolang controles openstaan." : races.Count==0 ? "Begin met het importeren van een TXT-bestand van uitslagen.nl." : "Alle naamcontroles zijn afgerond. Je kunt de uitslagen bekijken en exporteren.";
  RaceGrid.ItemsSource=races; RefreshConflictFilter();
  var selected=(ResultRace.SelectedItem as Race)?.Id; ResultRace.ItemsSource=races; ResultRace.SelectedItem=races.FirstOrDefault(r=>r.Id==selected)??races.FirstOrDefault();
  var resultCategory=ResultCategory.SelectedValue as string;
  ResultCategory.ItemsSource=CategoryOptions(loaded.Source,loaded.Poule);ResultCategory.SelectedValue=resultCategory;if(ResultCategory.SelectedIndex<0)ResultCategory.SelectedIndex=0;
  bool team=CurrentPage==Page.Teams;
  var category=resetCategory?null:StandingCategory.SelectedValue as string;
  if(resetCategory) { StandingStatusFilter.SelectedIndex=0; standingSortMember=null; foreach(var column in StandingGrid.Columns) column.SortDirection=null; }
  StandingCategory.ItemsSource=CategoryOptions(loaded.Source,loaded.Poule,team);
  StandingCategory.SelectedValue=category; if(StandingCategory.SelectedIndex<0) StandingCategory.SelectedIndex=0;
  var oldMapping=CategoryMappingGrid.SelectedItem as CategoryMappingRow;
  var categoryRace=CategoryRaceFilter.SelectedValue as string;
  CategoryRaceFilter.ItemsSource=new[]{new CategoryOption("","Alle wedstrijden")}.Concat(races.Select(r=>new CategoryOption(r.Id,r.Label))).ToArray();
  CategoryRaceFilter.SelectedValue=categoryRace??"";if(CategoryRaceFilter.SelectedIndex<0)CategoryRaceFilter.SelectedIndex=0;
  CategoryMappingGrid.ItemsSource=FilteredCategoryMappings();
  CategoryTarget.ItemsSource=CategoryOptions(loaded.Source,loaded.Poule);
  CategoryMappingGrid.SelectedItem=CategoryMappingGrid.Items.OfType<CategoryMappingRow>().FirstOrDefault(r=>r.RaceId==oldMapping?.RaceId&&r.Source==oldMapping.Source);
  if(CategoryMappingGrid.SelectedIndex<0&&CategoryMappingGrid.Items.Count>0)CategoryMappingGrid.SelectedIndex=0;
  ShowSelectedCategory();
  RacePanel.Visibility=CurrentPage==Page.Races?Visibility.Visible:Visibility.Collapsed;
  CategoriesPanel.Visibility=CurrentPage==Page.Categories?Visibility.Visible:Visibility.Collapsed;
  ConflictPanel.Visibility=CurrentPage==Page.NameChecks?Visibility.Visible:Visibility.Collapsed;
  ResultsPanel.Visibility=CurrentPage==Page.Results?Visibility.Visible:Visibility.Collapsed;
  StandingPanel.Visibility=CurrentPage is Page.Individuals or Page.Teams?Visibility.Visible:Visibility.Collapsed;
  ExportPanel.Visibility=CurrentPage==Page.Export?Visibility.Visible:Visibility.Collapsed;
  EditNameButton.Visibility=team?Visibility.Collapsed:Visibility.Visible; BreakdownButton.Visibility=team?Visibility.Visible:Visibility.Collapsed; NameColumn.Visibility=team?Visibility.Collapsed:Visibility.Visible;
  StatusFilterPanel.Visibility=team?Visibility.Collapsed:Visibility.Visible;
  Rules.Text=(team?"De drie beste lopers per wedstrijd; de twee beste ploeguitslagen tellen. U18/U20 worden gecombineerd. Minimaal twee ploegstarts; NTB is uitgesloten van plaatsing.":"De twee beste uitslagen tellen. Drie starts: 3 bonuspunten bij maximaal 10 deelnemers, anders 5. Minimaal twee starts voor een plaats.")+" Groen = geplaatst voor de finale. W1–W3 staan op datum: "+string.Join("; ",races.Select((r,i)=>$"W{i+1}: {r.Label}"));
  refreshing=false; RefreshTables();
 }
 private void RefreshTables()
 {
  if(pageData==null) return;
  string? race=(ResultRace.SelectedItem as Race)?.Id; string? cat=ResultCategory.SelectedValue as string;
  ResultsGrid.ItemsSource=pageData.Results.Where(x=>x.Race.Id==race&&x.Row.Category==cat).OrderBy(x=>x.Row.Rank).Select(x=>x.Row).ToList();
  cat=StandingCategory.SelectedValue as string;
  bool team=CurrentPage==Page.Teams;
  string? status=StandingStatusFilter.SelectedItem as string;
  StandingGrid.ItemsSource=(team?pageData.Teams:pageData.Individuals).Where(x=>x.Category==cat&&(team||status=="Alle statussen"||x.Status==status)).ToList();
  ApplyStandingSort();
 }
 private void ConflictKindChanged(object sender,SelectionChangedEventArgs e) { if(ready&&!refreshing) RefreshConflictFilter(); }
 private static CategoryOption[] CategoryOptions(Competition source,string poule,bool team=false) =>
  (team?Categories.All.Select(Categories.Team).Distinct():Categories.All).Select(key=>new CategoryOption(key,Categories.Display(source,poule,key))).ToArray();
 private void CategoryMappingSelected(object sender,SelectionChangedEventArgs e) { if(ready&&!refreshing) ShowSelectedCategory(); }
 private List<CategoryMappingRow> FilteredCategoryMappings() => pageData==null?[]:CategoryManagement.Rows(pageData.Source,pageData.Poule).Where(r=>string.IsNullOrEmpty(CategoryRaceFilter.SelectedValue as string)||r.RaceId==(string)CategoryRaceFilter.SelectedValue).ToList();
 private void CategoryRaceChanged(object sender,SelectionChangedEventArgs e)
 {
  if(!ready||refreshing)return;
  CategoryMappingGrid.ItemsSource=FilteredCategoryMappings();
  if(CategoryMappingGrid.Items.Count>0)CategoryMappingGrid.SelectedIndex=0;
  ShowSelectedCategory();
 }
 private void ShowSelectedCategory()
 {
  var mapping=CategoryMappingGrid.SelectedItem as CategoryMappingRow;
  CategoryTarget.IsEnabled=CategoryDisplayName.IsEnabled=SaveCategoryButton.IsEnabled=mapping!=null;
  CategoryTarget.SelectedValue=mapping?.Target;
  CategoryDisplayName.Text=mapping?.Name??"";
  CategorySourceNote.Text=mapping==null?"Selecteer een geïmporteerde categorie.":$"{mapping.Source} · {mapping.SourceNote}";
  CategoryResultsGrid.ItemsSource=mapping==null?null:pageData?.Source.Races.Single(r=>r.Id==mapping.RaceId).Results.Where(r=>CategoryManagement.Source(r)==mapping.Source).OrderBy(r=>r.Rank).ToList();
 }
 private void CategoryTargetChanged(object sender,SelectionChangedEventArgs e)
 {
  if(ready&&!refreshing&&CategoryTarget.SelectedItem is CategoryOption choice)CategoryDisplayName.Text=choice.Name;
 }
 private async void SaveCategory(object sender,RoutedEventArgs e)
 {
  if(CategoryMappingGrid.SelectedItem is not CategoryMappingRow mapping||CategoryTarget.SelectedValue is not string target)return;
  Competition? next=null;
  Run(()=> { next=Clone();CategoryManagement.Apply(next,Poule,mapping.RaceId,mapping.Source,target,CategoryDisplayName.Text);Storage.Save(next,dataFilePath);data=next; });
  if(next!=null&&ReferenceEquals(data,next))
  {
   await RefreshAsync(false,"Categorie opgeslagen. Deelnemers, naamcontroles en klassementen zijn opnieuw berekend.");
  }
 }
 private void RefreshConflictFilter()
 {
  string? kind=ConflictKindFilter.SelectedItem as string;
  ConflictGrid.UnselectAll();
  ConflictGrid.ItemsSource=pageData?.Conflicts.Where(c=>kind=="Alle soorten"||c.Kind==kind).ToList();
  UpdateConflictSelection();
 }
 private void StandingSorting(object sender,DataGridSortingEventArgs e)
 {
  var member=e.Column.SortMemberPath;
  if(!StandingNumberComparer.Supports(member)) { standingSortMember=null; return; }
  e.Handled=true;
  standingSortMember=member;
  standingSortDirection=e.Column.SortDirection==ListSortDirection.Ascending?ListSortDirection.Descending:ListSortDirection.Ascending;
  foreach(var column in StandingGrid.Columns) column.SortDirection=null;
  e.Column.SortDirection=standingSortDirection;
  ApplyStandingSort();
 }
 private void ApplyStandingSort()
 {
  if(standingSortMember==null||StandingGrid.ItemsSource==null) return;
  if(CollectionViewSource.GetDefaultView(StandingGrid.ItemsSource) is ListCollectionView view)
  {
   view.SortDescriptions.Clear();
   view.CustomSort=new StandingNumberComparer(standingSortMember,standingSortDirection);
  }
 }
 private void ImportTxt(object sender,RoutedEventArgs e) => Run(()=>
 {
  var dialog=new OpenFileDialog { Filter="Uitslagen.nl tekstbestand (*.txt)|*.txt" }; if(dialog.ShowDialog()!=true) return;
  var savedMappings=CategoryManagement.Rows(data,Poule);
  string? ChooseCategory(string header,IEnumerable<CategoryMappingRow> mappings)
  {
   return CategoryManagement.PreferredTarget(mappings,header)??Dialogs.Category(this,header,CategoryOptions(data,Poule));
  }
  var race=Parser.Parse(Parser.Read(dialog.FileName),header=>ChooseCategory(header,savedMappings)); race.Poule=Poule;
  var next=Clone(); var existing=next.Races.FirstOrDefault(r=>r.Poule==Poule&&r.Date==race.Date);
  foreach(var group in race.Results.GroupBy(CategoryManagement.Source))
  {
   var candidates=savedMappings.Where(m=>string.Equals(m.Source,group.Key,StringComparison.OrdinalIgnoreCase)).ToList();
   var sameRace=candidates.Where(m=>m.RaceId==existing?.Id).ToList();
   if(sameRace.Count>0)candidates=sameRace;
   if(candidates.Count==0)continue;
   var target=ChooseCategory(group.Key,candidates)??throw new OperationCanceledException("Import geannuleerd: categorie niet toegewezen.");
   foreach(var entry in group)entry.Category=target;
  }
  if(existing!=null) { if(MessageBox.Show($"{existing.Label} bestaat al. De uitslagen vervangen?","Wedstrijd vervangen",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return; race.Id=existing.Id; race.Organizer=existing.Organizer; next.Races.Remove(existing); }
  else if(Engine.Races(next,Poule).Count>=3) throw new InvalidOperationException("Deze poule bevat al drie wedstrijden.");
  next.Races.Add(race); Commit(next,$"{race.Count} uitslagen geïmporteerd voor {race.Label}.");
 });
 private void DeleteRace(object sender,RoutedEventArgs e) => Run(()=> { if(RaceGrid.SelectedItem is not Race race) { Notice.Text="Selecteer eerst een wedstrijd."; return; } if(MessageBox.Show($"{race.Label} en de uitslagen verwijderen?","Wedstrijd verwijderen",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return; var next=Clone(); next.Races.RemoveAll(r=>r.Id==race.Id); Commit(next,"Wedstrijd verwijderd."); });
 private void StartClean(object sender,RoutedEventArgs e) => Run(()=>
 {
  if(MessageBox.Show(this,"Alle wedstrijden, uitslagen, categoriekoppelingen, aangepaste namen en naamcontrolekeuzes uit alle poules verwijderen?","Schoon beginnen",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  Commit(new Competition(),"Alle gegevens zijn verwijderd. Je kunt schoon beginnen.");
 });
 private void EditRace(object sender,RoutedEventArgs e) => Run(()=> { if(RaceGrid.SelectedItem is not Race race) { Notice.Text="Selecteer eerst een wedstrijd.";return; } var next=Clone();var changed=next.Races.Single(r=>r.Id==race.Id);if(!Dialogs.EditRace(this,changed))return;Commit(next,"Wedstrijdgegevens bijgewerkt."); });
 private void Decide(bool same,bool right,Conflict? row=null) => Run(()=>
 {
  var selection=row==null?ConflictGrid.SelectedItems.OfType<Conflict>().ToList():new List<Conflict> { row };
  if(selection.Count==0) { Notice.Text="Selecteer eerst een of meer naamcontroles."; return; }
  var next=Clone(); ValidationChoices.Apply(next,Poule,selection,same,right); Commit(next,$"Keuze opgeslagen voor {selection.Count} naamcontrole(s).");
 });
 private void SameA(object s,RoutedEventArgs e)=>Decide(true,false); private void SameB(object s,RoutedEventArgs e)=>Decide(true,true); private void Different(object s,RoutedEventArgs e)=>Decide(false,false);
 private void RowSameA(object s,RoutedEventArgs e) { if(s is FrameworkElement { DataContext:Conflict c }) Decide(true,false,c); }
 private void RowSameB(object s,RoutedEventArgs e) { if(s is FrameworkElement { DataContext:Conflict c }) Decide(true,true,c); }
 private void RowDifferent(object s,RoutedEventArgs e) { if(s is FrameworkElement { DataContext:Conflict c }) Decide(false,false,c); }
 private void ConflictSelectionChanged(object s,SelectionChangedEventArgs e) { if(ready) UpdateConflictSelection(); }
 private void UpdateConflictSelection()
 {
  int count=ConflictGrid.SelectedItems.Count;
  OptionAButton.IsEnabled=OptionBButton.IsEnabled=DifferentButton.IsEnabled=count>0;
  ConflictSelectionCount.Text=$"{count} geselecteerd";
 }
 private void ReviewDecisions(object s,RoutedEventArgs e)=>Run(()=> { var choice=Dialogs.Decision(this,data.Decisions.Where(d=>d.Poule==Poule).ToList(),category=>Categories.Display(data,Poule,category)); if(choice==null)return; var next=Clone(); next.Decisions.Remove(choice); Commit(next,"Keuze hersteld; de naamcontrole is opnieuw beschikbaar."); });
 private void EditName(object s,RoutedEventArgs e)=>Run(()=> { if(StandingGrid.SelectedItem is not Standing r) { Notice.Text="Selecteer eerst een deelnemer.";return; } var name=Dialogs.Text(this,"Weergavenaam",r.Name,"Een lege waarde herstelt de oorspronkelijke naam."); if(name==null)return; var next=Clone(); var key=Poule+"::"+r.Category+"::"+r.Key; if(string.IsNullOrWhiteSpace(name))next.NameOverrides.Remove(key);else next.NameOverrides[key]=name.Trim(); Commit(next,"Weergavenaam opgeslagen."); });
 private void ShowBreakdown(object s,RoutedEventArgs e) { if(StandingGrid.SelectedItem is Standing r) Dialogs.TeamBreakdown(this,data,Poule,r);else Notice.Text="Selecteer eerst een ploeg."; }
 private void ExportExcel(object s,RoutedEventArgs e)=>Run(()=> { if(Engine.Races(data,Poule).Count==0)throw new InvalidOperationException("Importeer eerst een wedstrijd."); if(Engine.Conflicts(data,Poule).Count>0)throw new InvalidOperationException("Rond eerst de naamcontroles af."); var dialog=new SaveFileDialog { Filter="Excel werkmap (*.xlsx)|*.xlsx",FileName=$"Jeugdcross-{Poule}.xlsx" }; if(dialog.ShowDialog()!=true)return; ExcelExport.Save(data,Poule,dialog.FileName); Notice.Text="Excel-export opgeslagen: "+dialog.FileName; });
}
