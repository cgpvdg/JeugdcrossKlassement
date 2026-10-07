using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using jeugdcrossdata.Models;
using jeugdcrossdata.Services;
using jeugdcrossdata.UI;
using Microsoft.Win32;

namespace jeugdcrossdata;

public partial class MainWindow : Window
{
    private readonly AppConfigStore _configStore = new();
    private readonly UploadSettingsStore _uploadSettingsStore = new();
    private readonly SecureTokenStore _tokenStore = new();
    private readonly GitHubUploader _gitHubUploader = new();

    private AppConfig _config = new();
    private UploadSettings _uploadSettings = new();
    private string? _selectedFilePath;
    private bool _isConfigVisible;
    private bool _isBusy;
    private SiteContentEditor? _siteEditor;

    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _config = await _configStore.LoadAsync();
            _uploadSettings = await _uploadSettingsStore.LoadAsync();

            var allowedFiles = (_uploadSettings.AllowedRepositoryFiles is { Length: > 0 }
                ? _uploadSettings.AllowedRepositoryFiles
                : ["competitie-data.json", "site-content.json"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            RepositoryFileComboBox.ItemsSource = allowedFiles;
            RepositoryFileComboBox.SelectedItem = allowedFiles.Contains(_config.SelectedRepositoryFile, StringComparer.OrdinalIgnoreCase)
                ? _config.SelectedRepositoryFile
                : allowedFiles[0];

            ConfigPathTextBlock.Text = $"Configuratiebestand: {_uploadSettingsStore.GetSettingsPath()}";
            StaticSettingsTextBlock.Text =
                $"Owner: {_uploadSettings.GitHubOwner}\n" +
                $"Repository: {_uploadSettings.RepositoryName}\n" +
                $"Branch: {_uploadSettings.Branch}\n" +
                $"Basispad: {_uploadSettings.RepositoryBasePath}";

            SetStatus(string.IsNullOrWhiteSpace(_config.EncryptedPat)
                ? "Nog geen PAT opgeslagen."
                : "Versleutelde PAT gevonden.");
        }
        catch (Exception ex)
        {
            SetStatus($"Configuratie laden mislukt: {ex.Message}");
            ShowAlert("Fout", $"Configuratie laden mislukt: {ex.Message}", AlertType.Error);
        }
    }

    private async void SavePatButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PatPasswordBox.Password))
        {
            ShowAlert("PAT vereist", "Voer eerst een PAT in.", AlertType.Warning);
            return;
        }

        try
        {
            _config.EncryptedPat = _tokenStore.Encrypt(PatPasswordBox.Password.Trim());
            await PersistSettingsAsync();
            PatPasswordBox.Clear();
            SetStatus("PAT versleuteld opgeslagen.");
        }
        catch (Exception ex)
        {
            ShowAlert("Fout", $"PAT opslaan mislukt: {ex.Message}", AlertType.Error);
        }
    }

    private async void ClearPatButton_Click(object sender, RoutedEventArgs e)
    {
        _config.EncryptedPat = null;
        PatPasswordBox.Clear();
        await PersistSettingsAsync();
        SetStatus("Opgeslagen PAT verwijderd.");
    }

    private async void TestPatButton_Click(object sender, RoutedEventArgs e)
    {
        var inputPat = PatPasswordBox.Password.Trim();
        string pat;

        if (!string.IsNullOrWhiteSpace(inputPat))
        {
            pat = inputPat;
        }
        else if (!string.IsNullOrWhiteSpace(_config.EncryptedPat))
        {
            try
            {
                pat = _tokenStore.Decrypt(_config.EncryptedPat);
            }
            catch
            {
                ShowAlert("PAT fout", "Opgeslagen PAT kon niet worden gelezen. Voer een nieuwe PAT in.", AlertType.Warning);
                return;
            }
        }
        else
        {
            ShowAlert("PAT vereist", "Geen PAT beschikbaar. Vul een PAT in of sla er eerst een op.", AlertType.Warning);
            return;
        }

        ToggleBusy(true);
        try
        {
            SetStatus("PAT valideren...");
            await _gitHubUploader.ValidatePatAsync(pat);
            SetStatus("PAT is geldig.");
            ShowAlert("Succes", "PAT is geldig en bruikbaar voor GitHub API.", AlertType.Success);
        }
        catch (GitHubTokenInvalidException)
        {
            SetStatus("PAT ongeldig of verlopen.");
            ShowAlert("PAT verlopen", "PAT is ongeldig of verlopen. Voer een nieuwe PAT in.", AlertType.Warning);
        }
        catch (Exception ex)
        {
            SetStatus($"PAT test mislukt: {ex.Message}");
            ShowAlert("Fout", $"PAT test mislukt: {ex.Message}", AlertType.Error);
        }
        finally
        {
            ToggleBusy(false);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Multiselect = false,
            Title = "Selecteer een JSON bestand"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectFile(dialog.FileName);
        }
    }

    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = !IsSiteEditorMode && !_isBusy && HasValidJsonDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void DropArea_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (IsSiteEditorMode || _isBusy) return;
        if (!HasValidJsonDrop(e))
        {
            ShowAlert("Ongeldig bestand", "Sleep een .json bestand in het vak.", AlertType.Warning);
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        SelectFile(files[0]);
    }

    private async void DownloadRepositoryFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (RepositoryFileComboBox.SelectedItem is null)
        {
            ShowAlert("Onvolledige invoer", "Kies eerst een doelbestand in de repository.", AlertType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_config.EncryptedPat))
        {
            ShowAlert("PAT vereist", "Geen PAT opgeslagen. Voer en bewaar eerst een PAT.", AlertType.Warning);
            return;
        }

        string pat;
        try
        {
            pat = _tokenStore.Decrypt(_config.EncryptedPat);
        }
        catch
        {
            ShowAlert("PAT fout", "Opgeslagen PAT kon niet worden gelezen. Voer een nieuwe PAT in.", AlertType.Warning);
            _config.EncryptedPat = null;
            await PersistSettingsAsync();
            return;
        }

        ToggleBusy(true);
        try
        {
            var fileName = GetSelectedRepositoryFileName();
            var repositoryPath = BuildRepositoryPath(fileName);
            SetStatus("Bestand downloaden uit GitHub...");

            var content = await _gitHubUploader.DownloadFileContentAsync(
                pat,
                _uploadSettings.GitHubOwner,
                _uploadSettings.RepositoryName,
                repositoryPath,
                _uploadSettings.Branch);

            ShowSiteEditor(new SiteContentEditor(content));
            SetStatus("Site-inhoud opgehaald. Pas de gewenste waarden aan en klik op Wijzigingen versturen.");
        }
        catch (GitHubTokenInvalidException)
        {
            _config.EncryptedPat = null;
            await PersistSettingsAsync();
            ShowAlert("PAT verlopen", "Voer een nieuwe PAT in en sla op.", AlertType.Warning);
        }
        catch (Exception ex)
        {
            SetStatus($"Ophalen mislukt: {ex.Message}");
            ShowAlert("Fout", ex.Message, AlertType.Error);
        }
        finally { ToggleBusy(false); }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        if (!ValidateRequiredInput(out var validationError))
        {
            ShowAlert("Onvolledige invoer", validationError, AlertType.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_config.EncryptedPat))
        {
            ShowAlert("PAT vereist", "Voer en bewaar eerst een PAT.", AlertType.Warning);
            return;
        }
        string pat;
        try { pat = _tokenStore.Decrypt(_config.EncryptedPat); }
        catch
        {
            _config.EncryptedPat = null;
            await PersistSettingsAsync();
            ShowAlert("PAT fout", "Voer een nieuwe PAT in en sla op.", AlertType.Warning);
            return;
        }
        ToggleBusy(true);
        try
        {
            SetStatus(IsSiteEditorMode ? "Site-wijzigingen versturen naar GitHub..." : "Poules samenvoegen en uploaden naar GitHub...");
            await PersistSettingsAsync();
            var repositoryPath = BuildRepositoryPath(GetSelectedRepositoryFileName());
            var result = IsSiteEditorMode
                ? await _gitHubUploader.UploadSiteContentAsync(pat, _uploadSettings.GitHubOwner, _uploadSettings.RepositoryName, repositoryPath, _uploadSettings.Branch, _siteEditor!)
                : await _gitHubUploader.UploadJsonAsync(pat, _uploadSettings.GitHubOwner, _uploadSettings.RepositoryName, repositoryPath, _uploadSettings.Branch, _selectedFilePath!);
            if (IsSiteEditorMode && result.SavedContent is not null)
                ShowSiteEditor(new SiteContentEditor(result.SavedContent));
            if (!result.TargetUpdated)
            {
                SetStatus("Geen wijzigingen om te versturen.");
                ShowAlert("Geen wijzigingen", "De inhoud is al gelijk aan de huidige repo-versie.", AlertType.Info);
                return;
            }
            SetStatus(result.ArchiveCreated ? $"Versturen geslaagd met archief: {result.ArchiveRepositoryPath}" : "Versturen geslaagd.");
            ShowAlert("Succes", "De gegevens zijn verstuurd naar GitHub.", AlertType.Success);
        }
        catch (GitHubTokenInvalidException)
        {
            _config.EncryptedPat = null;
            await PersistSettingsAsync();
            ShowAlert("PAT verlopen", "Voer een nieuwe PAT in en sla op.", AlertType.Warning);
        }
        catch (Exception ex)
        {
            SetStatus($"Versturen mislukt: {ex.Message}");
            ShowAlert("Fout", ex.Message, AlertType.Error);
        }
        finally { ToggleBusy(false); }
    }

    private async Task PersistSettingsAsync()
    {
        _config.SelectedRepositoryFile = GetSelectedRepositoryFileName();
        await _configStore.SaveAsync(_config);
    }
    private void SelectFile(string filePath)
    {
        if (IsSiteEditorMode) return;
        _selectedFilePath = filePath;
        SelectedFileTextBlock.Text = $"Geselecteerd: {filePath}";
        SetStatus("JSON bestand geselecteerd.");
    }
    private bool ValidateRequiredInput(out string message)
    {
        if (IsSiteEditorMode)
        {
            message = _siteEditor is null ? "Haal eerst de site-inhoud op." : string.Empty;
            return _siteEditor is not null;
        }
        if (RepositoryFileComboBox.SelectedItem is null || string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            message = "Kies een doelbestand en selecteer een JSON bestand.";
            return false;
        }
        if (!File.Exists(_selectedFilePath))
        {
            message = "Het geselecteerde bronbestand bestaat niet meer.";
            return false;
        }
        if (!string.Equals(Path.GetExtension(_selectedFilePath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            message = "Alleen JSON bestanden worden ondersteund.";
            return false;
        }
        message = string.Empty;
        return true;
    }
    private static bool HasValidJsonDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        return files is { Length: 1 } && string.Equals(Path.GetExtension(files[0]), ".json", StringComparison.OrdinalIgnoreCase);
    }
    private string GetSelectedRepositoryFileName() => (RepositoryFileComboBox.SelectedItem as string ?? "competitie-data.json").Trim();
    private string BuildRepositoryPath(string repositoryFileName)
    {
        var basePath = (_uploadSettings.RepositoryBasePath ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        return $"{basePath}/{repositoryFileName.Trim().TrimStart('/')}";
    }
    private bool IsSiteEditorMode => GetSelectedRepositoryFileName().Equals("site-content.json", StringComparison.OrdinalIgnoreCase);
    private void RepositoryFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CompetitionFilePanel is null || SiteEditorPanel is null || UploadButton is null) return;
        CompetitionFilePanel.Visibility = IsSiteEditorMode ? Visibility.Collapsed : Visibility.Visible;
        SiteEditorPanel.Visibility = IsSiteEditorMode ? Visibility.Visible : Visibility.Collapsed;
        DownloadRepositoryFileButton.Visibility = IsSiteEditorMode ? Visibility.Visible : Visibility.Collapsed;
        UploadButton.Content = IsSiteEditorMode ? "Wijzigingen versturen" : "Upload naar GitHub";
        ToggleBusy(_isBusy);
    }
    private void ShowSiteEditor(SiteContentEditor editor)
    {
        _siteEditor = editor;
        SiteEditorGroups.ItemsSource = editor.Fields.GroupBy(field => field.Group)
            .Select(group => new { Key = group.Key, Fields = group.ToArray() }).ToArray();
    }
    private void SetStatus(string message) => StatusTextBlock.Text = message;
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleMaximizeRestore(); return; }
        DragMove();
    }
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void ConfigToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isConfigVisible = !_isConfigVisible;
        ConfigSection.Visibility = _isConfigVisible ? Visibility.Visible : Visibility.Collapsed;
        ConfigToggleButton.Background = new System.Windows.Media.SolidColorBrush(_isConfigVisible
            ? System.Windows.Media.Color.FromRgb(30, 139, 230) : System.Windows.Media.Color.FromRgb(27, 45, 74));
        ConfigToggleButton.BorderBrush = new System.Windows.Media.SolidColorBrush(_isConfigVisible
            ? System.Windows.Media.Color.FromRgb(72, 166, 255) : System.Windows.Media.Color.FromRgb(53, 87, 133));
    }
    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();
    private void Window_StateChanged(object? sender, EventArgs e)
    {
        RootContainer.Margin = WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
        MaximizeRestoreButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximizeRestore() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void ToggleBusy(bool isBusy)
    {
        _isBusy = isBusy;
        Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
        UploadButton.IsEnabled = !isBusy && (!IsSiteEditorMode || _siteEditor is not null);
        DownloadRepositoryFileButton.IsEnabled = !isBusy && IsSiteEditorMode;
        BrowseButton.IsEnabled = !isBusy;
        SiteEditorGroups.IsEnabled = !isBusy;
        TopSiteUploadButton.IsEnabled = !isBusy && _siteEditor is not null;
        RepositoryFileComboBox.IsEnabled = !isBusy;
        SavePatButton.IsEnabled = !isBusy;
        TestPatButton.IsEnabled = !isBusy;
        ClearPatButton.IsEnabled = !isBusy;
    }
    private void ShowAlert(string title, string message, AlertType type) => ModernAlertWindow.Show(this, title, message, type);
    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        if (File.Exists(iconPath)) Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
    }
}
