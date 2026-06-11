using Limes.Agents.Maf;

namespace Limes.Agents.Tests;

/// <summary>
/// Collection that disables parallelization for tests which mutate process-wide environment
/// variables, so their env-var state can't race with other tests.
/// </summary>
[CollectionDefinition("Environment variables", DisableParallelization = true)]
public sealed class EnvironmentVariableCollection;

[Collection("Environment variables")]
public class FoundryConnectionTests
{
    [Theory]
    [InlineData("http://contoso.openai.azure.com/")]
    [InlineData("ftp://contoso.openai.azure.com/")]
    public void FromEnvironment_RejectsNonHttpsEndpoint(string endpoint)
    {
        using var _ = new EnvScope(endpoint, "gpt-4.1");

        var connection = FoundryConnection.FromEnvironment(out var reason);

        Assert.Null(connection);
        Assert.NotNull(reason);
        Assert.Contains("HTTPS", reason);
    }

    [Fact]
    public void FromEnvironment_AcceptsHttpsEndpoint()
    {
        using var _ = new EnvScope("https://contoso.openai.azure.com/", "gpt-4.1");

        var connection = FoundryConnection.FromEnvironment(out var reason);

        Assert.NotNull(connection);
        Assert.Null(reason);
        Assert.Equal("gpt-4.1", connection!.Deployment);
    }

    /// <summary>Sets the Foundry env vars for the test and restores the prior values on dispose.</summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly string? _prevEndpoint;
        private readonly string? _prevDeployment;

        public EnvScope(string endpoint, string deployment)
        {
            _prevEndpoint = Environment.GetEnvironmentVariable(FoundryConnection.EndpointEnvVar);
            _prevDeployment = Environment.GetEnvironmentVariable(FoundryConnection.DeploymentEnvVar);
            Environment.SetEnvironmentVariable(FoundryConnection.EndpointEnvVar, endpoint);
            Environment.SetEnvironmentVariable(FoundryConnection.DeploymentEnvVar, deployment);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(FoundryConnection.EndpointEnvVar, _prevEndpoint);
            Environment.SetEnvironmentVariable(FoundryConnection.DeploymentEnvVar, _prevDeployment);
        }
    }
}
