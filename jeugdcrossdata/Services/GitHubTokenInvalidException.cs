namespace jeugdcrossdata.Services;

public sealed class GitHubTokenInvalidException(string message) : Exception(message)
{
}

