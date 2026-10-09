namespace Jeugdcross.Klassment.Core;

public static class ValidationChoices
{
 public static void Apply(Competition data, string poule, IEnumerable<Conflict> selection, bool same, bool right)
 {
  var selected = selection.DistinctBy(c => (c.Category, c.Left, c.Right)).ToList();
  var now = DateTime.UtcNow;
  for (int i = 0; i < selected.Count; i++)
  {
   var c = selected[i];
   data.Decisions.RemoveAll(d => d.Poule == poule && d.Category == c.Category &&
    ((d.Left == c.Left && d.Right == c.Right) || (d.Left == c.Right && d.Right == c.Left)));
   data.Decisions.Add(new(poule, c.Category, c.Left, c.Right, same,
    right ? c.RightName : c.LeftName, right ? c.RightAssociation : c.LeftAssociation, now.AddTicks(i)));
  }
 }
}
