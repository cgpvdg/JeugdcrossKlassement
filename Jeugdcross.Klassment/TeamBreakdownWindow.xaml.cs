using Jeugdcross.Klassment.Core;
namespace Jeugdcross.Klassment;

public partial class TeamBreakdownWindow : Wpf.Ui.Controls.FluentWindow
{
 private TeamScoreDetails? details;
 public TeamBreakdownWindow(Competition data, string poule, Standing team)
 {
  InitializeComponent();
  TeamHeading.Text = $"{team.Association} · {team.CategoryLabel}";
  TotalText.Text = $"Poule {poule} · Totaal: {team.Total} punten · {team.Status}";
  details = TeamBreakdown.Create(data, poule, team);
  RaceScoresGrid.ItemsSource = details.Races;
  RunnerScoresGrid.ItemsSource = Array.Empty<TeamRunnerScore>();
 }
 private void RaceSelected(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
 {
  RunnerScoresGrid.ItemsSource=RaceScoresGrid.SelectedItem is TeamRaceScore selected?details?.Runners.Where(r=>r.Race==selected.Race).ToList():[];
 }
}
