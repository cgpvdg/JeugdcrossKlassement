namespace Jeugdcross.Klassment.Core;

public static class Engine
{
 public static double Similarity(string left,string right)
 {
  var a=Parser.Key(left).Replace(" ",""); var b=Parser.Key(right).Replace(" ","");
  if(a==b) return 1;
  var prev=Enumerable.Range(0,b.Length+1).ToArray();
  for(int i=1;i<=a.Length;i++) { var row=new int[b.Length+1]; row[0]=i; for(int j=1;j<=b.Length;j++) row[j]=Math.Min(Math.Min(row[j-1]+1,prev[j]+1),prev[j-1]+(a[i-1]==b[j-1]?0:1)); prev=row; }
  return 1-(double)prev[b.Length]/Math.Max(a.Length,b.Length);
 }
 public static List<Race> Races(Competition data,string poule) => data.Races.Where(r=>r.Poule==poule).OrderBy(r=>r.Date).ThenBy(r=>r.Id).ToList();
 private static List<(Race Race,Result Row)> Resolve(Competition data,string poule,bool across=true)
 {
  var rows=Races(data,poule).SelectMany(r=>r.Results.Select(x=>(Race:r,Row:x with { CategoryLabel=Categories.Display(data,poule,x.Category) }))).ToList();
  // Two union-find passes match the original: within clubs first, then across clubs.
  foreach(bool cross in across ? new[]{false,true} : new[]{false})
  foreach(var group in rows.GroupBy(x=>x.Row.Category))
  {
   var nodes=group.Select(x=>x.Row).GroupBy(x=>x.Node).ToDictionary(x=>x.Key,x=>x.GroupBy(y=>y.Name).OrderByDescending(y=>y.Count()).First().First());
   var parent=nodes.Keys.ToDictionary(x=>x,x=>x);
   string Find(string n) { if(parent[n]!=n) parent[n]=Find(parent[n]); return parent[n]; }
   var decisions=data.Decisions.Where(d=>d.Poule==poule && d.Category==group.Key && d.Same && (d.Left.Split("::").Last()!=d.Right.Split("::").Last())==cross && nodes.ContainsKey(d.Left) && nodes.ContainsKey(d.Right)).OrderBy(d=>d.UpdatedAt).ToList();
   foreach(var d in decisions) parent[Find(d.Right)]=Find(d.Left);
   foreach(var component in nodes.Keys.GroupBy(Find).ToList())
   {
    var keys=component.ToHashSet(); var latest=decisions.LastOrDefault(d=>keys.Contains(d.Left)&&keys.Contains(d.Right));
    var canonical=nodes[component.Order(StringComparer.Ordinal).First()];
    var name=latest?.Name ?? canonical.Name; var club=latest?.Association ?? canonical.Association;
    foreach(var item in group.Where(x=>keys.Contains(x.Row.Node))) { item.Row.Name=name; item.Row.Association=club; }
   }
  }
  return rows;
 }
 public static List<(Race Race,Result Row)> Results(Competition data,string poule) => Resolve(data,poule);
 public static List<Conflict> Conflicts(Competition data,string poule)
 {
  var pending=new List<Conflict>();
  foreach(bool cross in new[]{false,true})
  foreach(var category in Resolve(data,poule,!cross).GroupBy(x=>x.Row.Category))
  {
   var participants=category.Select(x=>x.Row).DistinctBy(x=>x.Node).Where(x=>Parser.Key(x.Name).Length>=4).OrderBy(x=>x.Name,StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("nl-NL"),false)).ToArray();
   var parent=participants.ToDictionary(x=>x.Node,x=>x.Node);
   string Find(string n) { if(parent[n]!=n)parent[n]=Find(parent[n]);return parent[n]; }
   foreach(var d in data.Decisions.Where(d=>d.Poule==poule&&d.Category==category.Key&&d.Same&&parent.ContainsKey(d.Left)&&parent.ContainsKey(d.Right))) parent[Find(d.Right)]=Find(d.Left);
   for(int i=0;i<participants.Length;i++) for(int j=i+1;j<participants.Length;j++)
   {
    var a=participants[i]; var b=participants[j];
    if((Parser.Key(a.Association)!=Parser.Key(b.Association))!=cross) continue;
    var similarity=Similarity(a.Name,b.Name); if(similarity<(cross?.88:.75)) continue;
    if(data.Decisions.Any(d=>d.Poule==poule && d.Category==category.Key && ((d.Left==a.Node&&d.Right==b.Node)||(d.Left==b.Node&&d.Right==a.Node)))) continue;
    if(Find(a.Node)==Find(b.Node)) continue;
    pending.Add(new(category.Key,a.Node,b.Node,a.Name,b.Name,a.Association,b.Association,similarity) { CategoryLabel=Categories.Display(data,poule,category.Key) });
   }
  }
  return pending.OrderByDescending(x=>x.Similarity).ToList();
 }
 public static List<Standing> Individuals(Competition data,string poule)
 {
  var races=Races(data,poule); var output=new List<Standing>();
  foreach(var category in Resolve(data,poule).GroupBy(x=>x.Row.Category))
  {
   var rows=new List<Standing>();
   foreach(var participant in category.GroupBy(x=>x.Row.Node))
   {
    var first=participant.First().Row;
    var row=new Standing { Category=category.Key,CategoryLabel=Categories.Display(data,poule,category.Key),Key=participant.Key,Name=first.Name,Association=first.Association };
    foreach(var entry in participant) row.Scores[races.FindIndex(r=>r.Id==entry.Race.Id)]=entry.Row.Points;
    if(data.NameOverrides.TryGetValue(poule+"::"+category.Key+"::"+row.Key,out var name)) row.Name=name;
    row.Bonus=row.Starts>=3 ? category.Select(x=>x.Row.Node).Distinct().Count()<=10 ? 3:5 :0;
    row.Total=row.Scores.Where(x=>x.HasValue).Select(x=>x!.Value).Order().Take(2).Sum()-row.Bonus;
    row.Eligible=row.Starts>=2; rows.Add(row);
   }
   Rank(rows,false); if(!FinalsReady(data,poule)) foreach(var row in rows) row.Qualified=false; output.AddRange(rows);
  }
  return output.OrderBy(x=>Array.IndexOf(Categories.All,x.Category)).ToList();
 }
 public static List<Standing> Teams(Competition data,string poule)
 {
  var races=Races(data,poule); var output=new List<Standing>();
  foreach(var category in TeamResults(data,poule).GroupBy(x=>Categories.Team(x.Row.Category)))
  {
   var rows=new List<Standing>();
   foreach(var club in category.GroupBy(x=>Parser.Key(x.Row.Association)))
   {
    var row=new Standing { Category=category.Key,CategoryLabel=Categories.Display(data,poule,category.Key),Key=club.Key,Association=club.First().Row.Association };
    foreach(var race in club.GroupBy(x=>x.Race.Id))
    {
     var best=race.OrderBy(x=>x.Row.Points).ThenBy(x=>x.Row.Name).Take(3).ToList();
     if(best.Count<3) continue;
     var i=races.FindIndex(r=>r.Id==race.Key); row.Scores[i]=best.Sum(x=>x.Row.Points);
     row.Breakdown+=$"Wedstrijd {i+1}: "+string.Join(" + ",best.Select(x=>$"{x.Row.Name} ({x.Row.CategoryLabel}, {x.Row.Points}, {x.Row.Time})"))+Environment.NewLine;
    }
    row.Total=row.Scores.Where(x=>x.HasValue).Select(x=>x!.Value).Order().Take(2).Sum();
    row.Eligible=row.Starts>=2 && club.Key!="ntb" && !club.Key.Contains("nederlandse triathlon bon");
    if(row.Total>0) rows.Add(row);
   }
   Rank(rows,true); if(!FinalsReady(data,poule)) foreach(var row in rows) row.Qualified=false; output.AddRange(rows);
  }
  var order=Categories.All.Select(Categories.Team).Distinct().ToArray();
  return output.OrderBy(x=>Array.IndexOf(order,x.Category)).ToList();
 }
 public static bool FinalsReady(Competition data,string poule) => Races(data,poule).Count==3 && Races(data,poule).All(r=>r.Count>0);
 public static List<(Race Race,Result Row)> TeamResults(Competition data,string poule)
 {
  var results=Resolve(data,poule);
  foreach(var group in results.Where(x=>x.Row.Category is "Mannen U20" or "Mannen U18" or "Vrouwen U20" or "Vrouwen U18").GroupBy(x=>(x.Race.Id,Category:Categories.Team(x.Row.Category))))
  {
   var ordered=group.Select(x=>(x.Row,Seconds:FinishSeconds(x.Row.Time))).OrderBy(x=>x.Seconds).ThenBy(x=>x.Row.Name).ToList();
   int place=0;int previous=-1;
   for(int i=0;i<ordered.Count;i++)
   {
    if(ordered[i].Seconds!=previous)place=i+1;
    previous=ordered[i].Seconds;
    ordered[i].Row.Points=place;
   }
  }
  return results;
 }
 private static int FinishSeconds(string time)
 {
  var parts=time.Split(':');
  if(parts.Length is <2 or >3||parts.Any(p=>!int.TryParse(p,out var n)||n<0)||parts.Skip(1).Any(p=>int.Parse(p)>59))throw new InvalidDataException("Een geldige tijd is nodig voor het gecombineerde U20/U18-ploegenklassement: "+time);
  return parts.Aggregate(0,(total,p)=>checked(total*60+int.Parse(p)));
 }
 private static void Rank(List<Standing> rows,bool team)
 {
  rows.Sort((a,b)=> { int n=b.Eligible.CompareTo(a.Eligible); if(n==0)n=a.Total.CompareTo(b.Total); if(n==0)n=b.Starts.CompareTo(a.Starts); return n!=0?n:string.Compare(team?a.Association:a.Name,team?b.Association:b.Name,System.Globalization.CultureInfo.GetCultureInfo("nl-NL"),System.Globalization.CompareOptions.None); });
  var ranked=rows.Where(x=>x.Eligible).ToArray();
  for(int i=0;i<ranked.Length;i++) ranked[i].Place=i>0&&ranked[i].Total==ranked[i-1].Total?ranked[i-1].Place:i+1;
  int count=ranked.Length;
  int finalists=team ? count==0?0:count==1?1:count<=3?2:count==4?3:count<=6?4:count<=8?5:count<=10?6:7 : count<=3?count:count==4?3:count<=6?4:(int)Math.Ceiling(count*.5);
  if(finalists>0) foreach(var row in ranked) row.Qualified=row.Total<=ranked[Math.Min(finalists,count)-1].Total;
 }
}
