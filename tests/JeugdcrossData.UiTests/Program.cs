using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using jeugdcrossdata;
using jeugdcrossdata.Services;

Exception? failure = null;
var thread = new Thread(() => {
  try {
    var window = new MainWindow();
    var combo = (ComboBox)window.FindName("RepositoryFileComboBox");
    combo.ItemsSource = new[] { "competitie-data.json", "site-content.json" };
    combo.SelectedIndex = 0;
    Check(((Button)window.FindName("DownloadRepositoryFileButton")).Visibility == Visibility.Collapsed, "Competition has download button");
    combo.SelectedIndex = 1;
    Check(((FrameworkElement)window.FindName("CompetitionFilePanel")).Visibility == Visibility.Collapsed, "Site exposes file upload");
    Check(((Button)window.FindName("DownloadRepositoryFileButton")).FontFamily.Source != "Segoe MDL2 Assets", "Load button uses icon font");
    Check(!((Button)window.FindName("UploadButton")).IsEnabled, "Site upload before load enabled");
    var editor = new SiteContentEditor(File.ReadAllText("JeugdcrossCompetitie/public/data/site-content.json"));
    typeof(MainWindow).GetMethod("ShowSiteEditor", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { editor });
    typeof(MainWindow).GetMethod("ToggleBusy", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { false });
    Check(((Button)window.FindName("UploadButton")).IsEnabled, "Site upload after load disabled");
    var root = (FrameworkElement)window.Content;
    root.Measure(new Size(960, 1000)); root.Arrange(new Rect(0, 0, 960, 1000)); root.UpdateLayout();
    var expanders = Descendants(root).OfType<Expander>().ToArray();
    Check(expanders.Length == 14, "Expected general, three pool names, nine races and final groups");
    expanders[0].IsExpanded = true;
    root.UpdateLayout();
    var input = Descendants(expanders[0]).OfType<TextBox>().First();
    input.Text = "Gewijzigde titel";
    Check(editor.Fields.First().Value == input.Text, "Text input not bound to value");
    combo.SelectedIndex = 0;
    Check(((FrameworkElement)window.FindName("CompetitionFilePanel")).Visibility == Visibility.Visible, "Competition file upload hidden");
    combo.SelectedIndex = 1;
    root.UpdateLayout();
    var bitmap = new RenderTargetBitmap(960, 1000, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(root);
    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
    Directory.CreateDirectory("jeugdcrossdata/artifacts");
    using var output = File.Create("jeugdcrossdata/artifacts/site-editor-preview.png"); encoder.Save(output);
    window.Close();
    Console.WriteLine("PASS: WPF editor bindings, modes, fixed groups and upload enablement");
  } catch(Exception ex) { failure = ex; }
});
thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
if (failure is not null) throw failure;
static void Check(bool condition, string message) { if(!condition) throw new Exception(message); }
static IEnumerable<DependencyObject> Descendants(DependencyObject parent) {
  for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) {
    var child=VisualTreeHelper.GetChild(parent,i); yield return child;
    foreach(var descendant in Descendants(child)) yield return descendant;
  }
}
