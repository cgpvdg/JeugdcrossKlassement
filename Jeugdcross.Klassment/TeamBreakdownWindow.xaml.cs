using Jeugdcross.Klassment.Core;
namespace Jeugdcross.Klassment;

public partial class TeamBreakdownWindow : Wpf.Ui.Controls.FluentWindow
{
 public TeamBreakdownWindow(Competition data, string poule, Standing team)
 {
  InitializeComponent();
  TeamHeading.Text = $"{team.Association} · {team.CategoryLabel}";
  TotalText.Text = $"Poule {poule} · Totaal: {team.Total} punten · {team.Status}";
  var details = TeamBreakdown.Create(data, poule, team);
  RaceScoresGrid.ItemsSource = details.Races;
  RunnerScoresGrid.ItemsSource = details.Runners;
 }
}
