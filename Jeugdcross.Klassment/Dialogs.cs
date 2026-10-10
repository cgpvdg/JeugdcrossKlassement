using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Jeugdcross.Klassment.Core;
namespace Jeugdcross.Klassment;
internal static class Dialogs
{
 private record DecisionRow(Decision Decision,string Category,string OptionA,string OptionB,string Choice,string Name,string Association,DateTime UpdatedAt);
 public static void TeamBreakdown(Window owner, Competition data, string poule, Standing team)
 {
  new TeamBreakdownWindow(data,poule,team) { Owner=owner }.ShowDialog();
 }
 private static Window Window(Window owner,string title,UIElement body,Action accept)
 {
  var panel=new StackPanel { Margin=new Thickness(24) }; panel.Children.Add(body);
  var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0) };
  var ok=new Button { Content="Toepassen",IsDefault=true }; var cancel=new Button { Content="Annuleren",IsCancel=true }; buttons.Children.Add(ok);buttons.Children.Add(cancel);panel.Children.Add(buttons);
  var w=new Wpf.Ui.Controls.FluentWindow { Owner=owner,Title=title,Width=650,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Content=panel };
  ok.Click+=(_,_)=> { accept(); }; return w;
 }
 public static string? Category(Window owner,string header,IEnumerable<CategoryOption>? options=null)
 {
  string? value=null; var panel=new StackPanel(); panel.Children.Add(new TextBlock { Text="Categorie toewijzen: "+header,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18) }); var box=new ComboBox { ItemsSource=options??Categories.All.Select(c=>new CategoryOption(c,c)),DisplayMemberPath="Name",SelectedValuePath="Key" };panel.Children.Add(box);
  Window? w=null; w=Window(owner,"Categorie toewijzen",panel,()=> { if(box.SelectedValue is not string selected) return; value=selected;w!.DialogResult=true; });w.ShowDialog();return value;
 }
 public static string? Text(Window owner,string title,string initial,string hint)
 {
  string? value=null;var panel=new StackPanel();panel.Children.Add(new TextBlock { Text=hint,Margin=new Thickness(0,0,0,16),TextWrapping=TextWrapping.Wrap });var box=new TextBox { Text=initial };panel.Children.Add(box);
  Window? w=null;w=Window(owner,title,panel,()=> { value=box.Text;w!.DialogResult=true; });w.ShowDialog();return value;
 }
 public static Decision? Decision(Window owner,List<Decision> decisions,Func<string,string>? categoryName=null)
 {
  Decision? value=null;var panel=new StackPanel();panel.Children.Add(new TextBlock { Text="Selecteer een keuze om deze ongedaan te maken.",Margin=new Thickness(0,0,0,16) });
  string Participant(string node)=>string.Join(" · ",node.Split("::",StringSplitOptions.None));
  var box=new DataGrid { Height=350,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Single,SelectionUnit=DataGridSelectionUnit.FullRow,ItemsSource=decisions.Select(d=>new DecisionRow(d,categoryName?.Invoke(d.Category)??d.Category,Participant(d.Left),Participant(d.Right),d.Same?"Dezelfde deelnemer":"Verschillend",d.Same?d.Name:"",d.Same?d.Association:"",d.UpdatedAt)).ToList() };
  void Column(string header,string property,double width,string? format=null)=>box.Columns.Add(new DataGridTextColumn { Header=header,Binding=new Binding(property) { StringFormat=format },Width=new DataGridLength(width),ElementStyle=(Style)Application.Current.FindResource("CenteredTableText") });
  Column("Categorie","Category",150);Column("Optie A · deelnemer / vereniging","OptionA",220);Column("Optie B · deelnemer / vereniging","OptionB",220);Column("Keuze","Choice",145);Column("Gekozen naam","Name",160);Column("Vereniging","Association",150);Column("Opgeslagen op","UpdatedAt",145,"dd-MM-yyyy HH:mm");
  panel.Children.Add(box);
  Window? w=null;w=Window(owner,"Validatiekeuze herstellen",panel,()=> { if(box.SelectedItem is not DecisionRow selected)return;value=selected.Decision;w!.DialogResult=true; });w.Width=1100;w.ShowDialog();return value;
 }
 public static bool EditRace(Window owner,Race race)
 {
  var panel=new StackPanel();
  TextBox Input(string label,string value) { panel.Children.Add(new TextBlock { Text=label,Margin=new Thickness(0,12,0,6) });var box=new TextBox { Text=value };panel.Children.Add(box);return box; }
  var name=Input("Wedstrijdnaam",race.Name);var place=Input("Plaats / kopregel",race.Association);var organizer=Input("Organiserende vereniging",race.Organizer);
  panel.Children.Add(new TextBlock { Text="Datum",Margin=new Thickness(0,12,0,6) });var date=new DatePicker { SelectedDate=race.Date };panel.Children.Add(date);
  Window? w=null;w=Window(owner,"Wedstrijd bewerken",panel,()=> { if(string.IsNullOrWhiteSpace(name.Text)||date.SelectedDate==null) { MessageBox.Show("Vul een wedstrijdnaam en datum in.");return; }race.Name=name.Text.Trim();race.Association=place.Text.Trim();race.Organizer=organizer.Text.Trim();race.Date=date.SelectedDate.Value.Date;w!.DialogResult=true; });return w.ShowDialog()==true;
 }
}
