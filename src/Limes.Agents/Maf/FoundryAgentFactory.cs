using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Limes.Agents.Maf;

/// <summary>
/// Builds Microsoft Agent Framework <see cref="AIAgent"/> instances backed by an Azure AI
/// Foundry model deployment. One agent is created per Roman codename, each with its own
/// system-instruction persona. Authentication uses <see cref="DefaultAzureCredential"/>.
/// </summary>
public sealed class FoundryAgentFactory
{
    private readonly IChatClient _chatClient;

    public FoundryAgentFactory(FoundryConnection connection, TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var client = new AzureOpenAIClient(connection.Endpoint, credential ?? new DefaultAzureCredential());
        _chatClient = client.GetChatClient(connection.Deployment).AsIChatClient();
    }

    /// <summary>For tests / advanced wiring: build directly over any <see cref="IChatClient"/>.</summary>
    public FoundryAgentFactory(IChatClient chatClient)
        => _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));

    /// <summary>Creates a named MAF agent with the given system-instruction persona.</summary>
    public AIAgent CreateAgent(string name, string instructions)
        => new ChatClientAgent(_chatClient, instructions: instructions, name: name);
}
