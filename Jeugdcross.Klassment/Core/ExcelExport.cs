using ClosedXML.Excel;
namespace Jeugdcross.Klassment.Core;
public static class ExcelExport
{
 public static void Save(Competition data,string poule,string path)
 {
  if(Engine.Conflicts(data,poule).Count>0) throw new InvalidOperationException("Rond eerst de naamcontroles voor deze poule af.");
  using var book=new XLWorkbook();
  var results=book.Worksheets.Add("Uitslagenlijst");
  Header(results,["Poule","Wedstrijd","Datum","Categorie","Plaats","Deelnemer","Vereniging","Tijd"]);
  int row=2;
  foreach(var item in Engine.Results(data,poule).OrderBy(x=>x.Race.Date).ThenBy(x=>Array.IndexOf(Categories.All,x.Row.Category)).ThenBy(x=>x.Row.Rank))
  {
   var r=item.Row; results.Cell(row,1).Value=poule; results.Cell(row,2).Value=item.Race.Name; results.Cell(row,3).Value=item.Race.Date; results.Cell(row,3).Style.DateFormat.Format="dd-mm-yyyy";
   results.Cell(row,4).Value=r.CategoryLabel; results.Cell(row,5).Value=r.Rank; results.Cell(row,6).Value=r.Name; results.Cell(row,7).Value=r.Association; results.Cell(row++,8).Value=r.Time;
  }
  Finish(results,row-1,8);
  WriteStandings(book.Worksheets.Add("Individuele klassement"),Engine.Individuals(data,poule),false,data,poule);
  WriteStandings(book.Worksheets.Add("Ploegen klassement"),Engine.Teams(data,poule),true,data,poule);
  var temp=path+".tmp";
  using(var stream=File.Create(temp)) book.SaveAs(stream);
  File.Move(temp,path,true);
 }
 private static void WriteStandings(IXLWorksheet sheet,List<Standing> standings,bool team,Competition data,string poule)
 {
  var races=Engine.Races(data,poule);
  Header(sheet,["Categorie","Plaats",team?"Ploeg":"Deelnemer","Vereniging",..Enumerable.Range(0,3).Select(i=>i<races.Count?$"W{i+1} · {races[i].Label}":$"Wedstrijd {i+1}"),"Starts","Bonus","Totaal","Status","Puntopbouw"]);
  int i=2;
  foreach(var r in standings)
  {
   sheet.Cell(i,1).Value=r.CategoryLabel; if(r.Place.HasValue) sheet.Cell(i,2).Value=r.Place.Value;
   sheet.Cell(i,3).Value=team?r.Association:r.Name; sheet.Cell(i,4).Value=r.Association;
   for(int j=0;j<3;j++) if(r.Scores[j].HasValue) sheet.Cell(i,5+j).Value=r.Scores[j]!.Value;
   sheet.Cell(i,8).Value=r.Starts; sheet.Cell(i,9).Value=r.Bonus; sheet.Cell(i,10).Value=r.Total; sheet.Cell(i,11).Value=r.Status; sheet.Cell(i,12).Value=r.Breakdown;
   if(r.Qualified) sheet.Range(i,1,i,12).Style.Fill.BackgroundColor=XLColor.FromHtml("#DCFCE7"); i++;
  }
  Finish(sheet,i-1,12);
 }
 private static void Header(IXLWorksheet s,string[] names) { for(int i=0;i<names.Length;i++) s.Cell(1,i+1).Value=names[i]; }
 private static void Finish(IXLWorksheet s,int last,int columns)
 {
  s.Range(1,1,1,columns).Style.Fill.BackgroundColor=XLColor.FromHtml("#153C46"); s.Range(1,1,1,columns).Style.Font.FontColor=XLColor.White; s.Range(1,1,1,columns).Style.Font.Bold=true;
  s.SheetView.FreezeRows(1); s.Range(1,1,Math.Max(1,last),columns).SetAutoFilter(); s.Columns().AdjustToContents(8,48);
 }
}
