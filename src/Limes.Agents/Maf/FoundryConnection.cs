namespace Limes.Agents.Maf;

/// <summary>
/// Connection settings for the Azure AI Foundry model deployment that backs the MAF agents.
/// Resolved from environment variables so no secrets live in source; uses
/// <c>DefaultAzureCredential</c> (managed identity / az login) rather than keys.
/// </summary>
public sealed record FoundryConnection
{
    /// <summary>Foundry/Azure OpenAI endpoint, e.g. https://&lt;resource&gt;.openai.azure.com/.</summary>
    public required Uri Endpoint { get; init; }

    /// <summary>The model deployment name (e.g. "gpt-4.1" or "gpt-4.1-mini").</summary>
    public required string Deployment { get; init; }

    public const string EndpointEnvVar = "LIMES_FOUNDRY_ENDPOINT";
    public const string DeploymentEnvVar = "LIMES_FOUNDRY_DEPLOYMENT";

    /// <summary>
    /// Reads the connection from environment variables. Returns <c>null</c> (with a reason) when
    /// configuration is absent, so the orchestrator can fall back or error clearly.
    /// </summary>
    public static FoundryConnection? FromEnvironment(out string? reason)
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointEnvVar)?.Trim();
        var deployment = Environment.GetEnvironmentVariable(DeploymentEnvVar)?.Trim();

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            reason = $"{EndpointEnvVar} is not set.";
            return null;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            reason = $"{EndpointEnvVar} is not a valid absolute URI: '{endpoint}'.";
            return null;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            reason = $"{EndpointEnvVar} must use HTTPS: '{endpoint}'.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(deployment))
        {
            reason = $"{DeploymentEnvVar} is not set.";
            return null;
        }

        reason = null;
        return new FoundryConnection { Endpoint = uri, Deployment = deployment };
    }
}
