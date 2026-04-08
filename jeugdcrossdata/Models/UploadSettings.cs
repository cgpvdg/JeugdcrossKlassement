namespace jeugdcrossdata.Models;

public sealed class UploadSettings
{
    public string GitHubOwner { get; set; } = "cgpvdg";

    public string RepositoryName { get; set; } = "JeugdcrossKlassement";

    public string Branch { get; set; } = "master";

    public string RepositoryBasePath { get; set; } = "JeugdcrossCompetitie/public/data/";

    public string[] AllowedRepositoryFiles { get; set; } =
    [
        "competitie-data.json",
        "site-content.json"
    ];
}
