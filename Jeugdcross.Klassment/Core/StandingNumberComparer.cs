using System.Collections;
using System.ComponentModel;

namespace Jeugdcross.Klassment.Core;

public sealed class StandingNumberComparer(string member, ListSortDirection direction) : IComparer
{
 public static bool Supports(string member) => member is nameof(Standing.Place) or nameof(Standing.Race1) or nameof(Standing.Race2) or nameof(Standing.Race3);
 public int Compare(object? x, object? y)
 {
  if(x is not Standing a || y is not Standing b) return 0;
  int? Value(Standing row) => member switch
  {
   nameof(Standing.Place) => row.Place,
   nameof(Standing.Race1) => row.Race1,
   nameof(Standing.Race2) => row.Race2,
   nameof(Standing.Race3) => row.Race3,
   _ => throw new ArgumentOutOfRangeException(nameof(member))
  };
  var left=Value(a); var right=Value(b);
  // Missing values stay last, independently of the numeric sort direction.
  if(left.HasValue!=right.HasValue) return left.HasValue ? -1 : 1;
  int result=left.HasValue ? left.Value.CompareTo(right!.Value) : 0;
  if(direction==ListSortDirection.Descending) result=-result;
  return result!=0 ? result : string.Compare(a.Name.Length>0?a.Name:a.Association,b.Name.Length>0?b.Name:b.Association,StringComparison.CurrentCulture);
 }
}
