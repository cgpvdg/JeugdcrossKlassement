using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public MainWindow()
    {
        InitializeComponent();
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
        e.Effects = HasValidJsonDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void DropArea_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
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

            var saveDialog = new SaveFileDialog
            {
                FileName = fileName,
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                AddExtension = true,
                DefaultExt = ".json",
                Title = "Download bestand opslaan als"
            };

            if (saveDialog.ShowDialog() != true)
            {
                SetStatus("Download geannuleerd.");
                return;
            }

            await File.WriteAllTextAsync(saveDialog.FileName, content);
            SetStatus($"Bestand gedownload: {saveDialog.FileName}");
            ShowAlert("Succes", "Bestand succesvol gedownload uit de repository.", AlertType.Success);
        }
        catch (GitHubTokenInvalidException)
        {
            _config.EncryptedPat = null;
            await PersistSettingsAsync();
            SetStatus("PAT ongeldig of verlopen. Nieuwe PAT invoeren en opslaan.");
            ShowAlert("PAT verlopen", "Opgeslagen PAT is ongeldig of verlopen. Voer een nieuwe PAT in en sla op.", AlertType.Warning);
        }
        catch (FileNotFoundException ex)
        {
            SetStatus(ex.Message);
            ShowAlert("Niet gevonden", ex.Message, AlertType.Warning);
        }
        catch (Exception ex)
        {
            SetStatus($"Download mislukt: {ex.Message}");
            ShowAlert("Fout", $"Download mislukt: {ex.Message}", AlertType.Error);
        }
        finally
        {
            ToggleBusy(false);
        }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateRequiredInput(out var validationError))
        {
            ShowAlert("Onvolledige invoer", validationError, AlertType.Warning);
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
            SetStatus("Uploaden naar GitHub...");
            await PersistSettingsAsync();

            var repositoryPath = BuildRepositoryPath(GetSelectedRepositoryFileName());
            var uploadResult = await _gitHubUploader.UploadJsonAsync(
                pat,
                _uploadSettings.GitHubOwner,
                _uploadSettings.RepositoryName,
                repositoryPath,
                _uploadSettings.Branch,
                _selectedFilePath!);

            if (!uploadResult.TargetUpdated)
            {
                SetStatus("Geen update nodig: lokale JSON is gelijk aan de huidige repo-versie.");
                ShowAlert("Geen wijzigingen", "De inhoud is identiek aan de huidige repo-versie. Daarom is er niets geüpdatet.", AlertType.Info);
                return;
            }

            if (uploadResult.ArchiveCreated && !string.IsNullOrWhiteSpace(uploadResult.ArchiveRepositoryPath))
            {
                SetStatus($"Upload geslaagd met archief in repo: {uploadResult.ArchiveRepositoryPath}");
            }
            else
            {
                SetStatus("Upload geslaagd.");
            }

            ShowAlert("Succes", "JSON-bestand is succesvol geüpload naar GitHub.", AlertType.Success);
        }
        catch (GitHubTokenInvalidException)
        {
            _config.EncryptedPat = null;
            await PersistSettingsAsync();
            SetStatus("PAT ongeldig of verlopen. Nieuwe PAT invoeren en opslaan.");
            ShowAlert("PAT verlopen", "Opgeslagen PAT is ongeldig of verlopen. Voer een nieuwe PAT in en sla op.", AlertType.Warning);
        }
        catch (Exception ex)
        {
            SetStatus($"Upload mislukt: {ex.Message}");
            ShowAlert("Fout", $"Upload mislukt: {ex.Message}", AlertType.Error);
        }
        finally
        {
            ToggleBusy(false);
        }
    }

    private async Task PersistSettingsAsync()
    {
        _config.SelectedRepositoryFile = GetSelectedRepositoryFileName();
        await _configStore.SaveAsync(_config);
    }

    private void SelectFile(string filePath)
    {
        _selectedFilePath = filePath;
        SelectedFileTextBlock.Text = $"Geselecteerd: {filePath}";
        SetStatus("JSON bestand geselecteerd.");
    }

    private bool ValidateRequiredInput(out string message)
    {
        if (RepositoryFileComboBox.SelectedItem is null || string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            message = "Kies een doelbestand in de repository en selecteer een JSON bestand.";
            return false;
        }

        if (!File.Exists(_selectedFilePath))
        {
            message = "Het geselecteerde lokale bronbestand bestaat niet meer.";
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
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        return files is { Length: 1 } && string.Equals(Path.GetExtension(files[0]), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private string GetSelectedRepositoryFileName()
    {
        return (RepositoryFileComboBox.SelectedItem as string ?? "competitie-data.json").Trim();
    }

    private string BuildRepositoryPath(string repositoryFileName)
    {
        var basePath = (_uploadSettings.RepositoryBasePath ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        var fileName = repositoryFileName.Trim().TrimStart('/');
        return $"{basePath}/{fileName}";
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ConfigToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isConfigVisible = !_isConfigVisible;
        ConfigSection.Visibility = _isConfigVisible ? Visibility.Visible : Visibility.Collapsed;
        ConfigToggleButton.Background = _isConfigVisible
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 139, 230))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 45, 74));
        ConfigToggleButton.BorderBrush = _isConfigVisible
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(72, 166, 255))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(53, 87, 133));
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        RootContainer.Margin = WindowState == WindowState.Maximized
            ? new Thickness(8)
            : new Thickness(0);
        MaximizeRestoreButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void ToggleBusy(bool isBusy)
    {
        Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
        UploadButton.IsEnabled = !isBusy;
        DownloadRepositoryFileButton.IsEnabled = !isBusy;
        BrowseButton.IsEnabled = !isBusy;
        RepositoryFileComboBox.IsEnabled = !isBusy;
        SavePatButton.IsEnabled = !isBusy;
        TestPatButton.IsEnabled = !isBusy;
        ClearPatButton.IsEnabled = !isBusy;
    }

    private void ShowAlert(string title, string message, AlertType type)
    {
        ModernAlertWindow.Show(this, title, message, type);
    }
}
