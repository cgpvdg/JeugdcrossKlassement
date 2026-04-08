namespace jeugdcrossdata.Models;

public sealed class AppConfig
{
    public string? GitHubOwner { get; set; }

    public string? RepositoryName { get; set; }

    public string? RepositoryPath { get; set; }

    public string Branch { get; set; } = "main";

    public string? EncryptedPat { get; set; }
}

