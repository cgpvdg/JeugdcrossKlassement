namespace jeugdcrossdata.Models;

public sealed class AppConfig
{
    public string? EncryptedPat { get; set; }

    public string SelectedRepositoryFile { get; set; } = "competitie-data.json";
}

