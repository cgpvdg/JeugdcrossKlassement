using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text;
using System.Text.Json;

namespace jeugdcrossdata.Services;

public sealed class GitHubUploader
{
    private readonly Func<HttpClient> _createClient;

    public GitHubUploader(Func<HttpClient>? createClient = null)
    {
        _createClient = createClient ?? (() => new HttpClient());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task ValidatePatAsync(string pat, CancellationToken cancellationToken = default)
    {
        using var client = _createClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("jeugdcrossdata-app");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        await ValidateTokenAsync(client, cancellationToken);
    }

    public async Task<string> DownloadFileContentAsync(
        string pat,
        string owner,
        string repository,
        string repositoryPath,
        string branch,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Path.GetFileName(repositoryPath), "site-content.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Alleen site-content.json kan in de editor worden opgehaald.");
        using var client = _createClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("jeugdcrossdata-app");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        await ValidateTokenAsync(client, cancellationToken);
        var existingFile = await GetExistingFileAsync(client, owner, repository, repositoryPath, branch, cancellationToken);
        if (existingFile is null)
        {
            throw new FileNotFoundException($"Bestand niet gevonden in repo: {repositoryPath}");
        }

        return existingFile.RawContent;
    }

    public async Task<UploadResult> UploadJsonAsync(
        string pat,
        string owner,
        string repository,
        string repositoryPath,
        string branch,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(Path.GetFileName(repositoryPath), "site-content.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bewerk site-content.json in de app; handmatige bestandsupload is niet beschikbaar.");
        using var client = _createClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("jeugdcrossdata-app");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        await ValidateTokenAsync(client, cancellationToken);

        var rawContent = await File.ReadAllTextAsync(localFilePath, cancellationToken);
        var existingFile = await GetExistingFileAsync(client, owner, repository, repositoryPath, branch, cancellationToken);

        if (string.Equals(Path.GetFileName(repositoryPath), "competitie-data.json", StringComparison.OrdinalIgnoreCase))
        {
            rawContent = CompetitionDataMerger.Merge(existingFile?.RawContent, rawContent);
        }

        return await UploadPreparedAsync(client, owner, repository, repositoryPath, branch, rawContent, existingFile, cancellationToken);
    }

    public async Task<UploadResult> UploadSiteContentAsync(string pat, string owner, string repository,
        string repositoryPath, string branch, SiteContentEditor editor, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Path.GetFileName(repositoryPath), "site-content.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("De editor kan alleen site-content.json versturen.");
        using var client = _createClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("jeugdcrossdata-app");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        await ValidateTokenAsync(client, cancellationToken);
        var existingFile = await GetExistingFileAsync(client, owner, repository, repositoryPath, branch, cancellationToken)
            ?? throw new FileNotFoundException("Site-inhoud bestaat niet meer op GitHub. Haal de inhoud opnieuw op.");
        var rawContent = editor.ApplyTo(existingFile.RawContent);
        var result = await UploadPreparedAsync(client, owner, repository, repositoryPath, branch, rawContent, existingFile, cancellationToken);
        return result with { SavedContent = rawContent };
    }

    private static async Task<UploadResult> UploadPreparedAsync(HttpClient client, string owner, string repository,
        string repositoryPath, string branch, string rawContent, ExistingFile? existingFile, CancellationToken cancellationToken)
    {
        if (existingFile is not null && string.Equals(existingFile.RawContent, rawContent, StringComparison.Ordinal))
        {
            return new UploadResult(TargetUpdated: false, ArchiveCreated: false, ArchiveRepositoryPath: null);
        }

        string? archivePath = null;

        if (existingFile is not null)
        {
            archivePath = BuildArchivePath(repositoryPath);
            await UploadContentAsync(
                client,
                owner,
                repository,
                archivePath,
                branch,
                existingFile.RawContent,
                null,
                $"Archive copy via jeugdcrossdata ({DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss})",
                cancellationToken);
        }

        await UploadContentAsync(
            client,
            owner,
            repository,
            repositoryPath,
            branch,
            rawContent,
            existingFile?.Sha,
            $"Upload JSON via jeugdcrossdata ({DateTimeOffset.Now:yyyy-MM-dd HH:mm})",
            cancellationToken);

        return new UploadResult(TargetUpdated: true, ArchiveCreated: existingFile is not null, ArchiveRepositoryPath: archivePath);
    }

    private static async Task ValidateTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("https://api.github.com/user", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new GitHubTokenInvalidException("PAT is ongeldig of verlopen. Voer een nieuwe PAT in.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"PAT-validatie mislukt ({(int)response.StatusCode}): {body}");
    }

    private static async Task<ExistingFile?> GetExistingFileAsync(
        HttpClient client,
        string owner,
        string repository,
        string repositoryPath,
        string branch,
        CancellationToken cancellationToken)
    {
        var metadataUrl = $"https://api.github.com/repos/{owner}/{repository}/contents/{Uri.EscapeDataString(repositoryPath)}?ref={Uri.EscapeDataString(branch)}";
        using var response = await client.GetAsync(metadataUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Bestandscontrole mislukt ({(int)response.StatusCode}): {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var metadata = await JsonSerializer.DeserializeAsync<ContentMetadata>(stream, JsonOptions, cancellationToken);
        if (metadata is null || string.IsNullOrWhiteSpace(metadata.Sha))
        {
            return null;
        }

        var rawContent = DecodeGitHubContent(metadata.Content, metadata.Encoding);
        return new ExistingFile(metadata.Sha, rawContent);
    }

    private static async Task UploadContentAsync(
        HttpClient client,
        string owner,
        string repository,
        string repositoryPath,
        string branch,
        string rawContent,
        string? sha,
        string commitMessage,
        CancellationToken cancellationToken)
    {
        var encodedContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawContent));
        var requestPayload = new UploadRequest(
            Message: commitMessage,
            Content: encodedContent,
            Branch: branch,
            Sha: sha);

        var uploadUrl = $"https://api.github.com/repos/{owner}/{repository}/contents/{Uri.EscapeDataString(repositoryPath)}";
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = JsonContent.Create(requestPayload)
        };

        using var response = await client.SendAsync(uploadRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new GitHubTokenInvalidException("PAT is ongeldig of verlopen. Voer een nieuwe PAT in.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("GitHub weigert de upload (403). Controleer of PAT 'Contents: Read and write' heeft en of de branch write toestaat.");
        }

        throw new InvalidOperationException($"Upload mislukt ({(int)response.StatusCode}): {body}");
    }

    private static string BuildArchivePath(string repositoryPath)
    {
        var normalizedPath = repositoryPath.Replace('\\', '/');
        var lastSlash = normalizedPath.LastIndexOf('/');
        var directory = lastSlash >= 0 ? normalizedPath[..lastSlash] : string.Empty;
        var fileName = lastSlash >= 0 ? normalizedPath[(lastSlash + 1)..] : normalizedPath;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");

        return string.IsNullOrWhiteSpace(directory)
            ? $"archived/{timestamp}_{fileName}"
            : $"{directory}/archived/{timestamp}_{fileName}";
    }

    private static string DecodeGitHubContent(string? content, string? encoding)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        if (string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            var sanitized = content.Replace("\n", string.Empty).Replace("\r", string.Empty);
            var bytes = Convert.FromBase64String(sanitized);
            return Encoding.UTF8.GetString(bytes);
        }

        return content;
    }

    private sealed record ExistingFile(string Sha, string RawContent);

    private sealed record ContentMetadata(string? Sha, string? Content, string? Encoding);

    private sealed record UploadRequest(string Message, string Content, string Branch, string? Sha);

    public sealed record UploadResult(bool TargetUpdated, bool ArchiveCreated, string? ArchiveRepositoryPath, string? SavedContent = null);
}

